using System.Collections;
using UnityEngine;

public enum AIDifficulty
{
    Easy,
    Normal,
    Hard,
}

// Court Clash's AI padel opponent, v2 (2026-08-26 padel conversion — see
// CONTEXT.md for Turn/Dodge/Body Hit/Serve vocabulary):
//   - Roams its own half in 2D with a capped speed and a bounce-scaled
//     reaction delay; its half spans [netZ - halfDepth, netZ].
//   - Carries a Tron racket; the swing is presentation, the shot is computed
//     (a ballistic arc to a random target on the player's half).
//   - Dodges the ball when it is NOT its turn (Body Hit = point to player).
//   - Serves when MatchController says so.
//
// Readability pass (2026-08-26, after the user's playtest "the AI does not
// show animations... it hits without moving the hands"): the body now turns
// to face the ball, the swing STARTS while the ball is still approaching
// (wind-up, so contact lands mid-swing instead of the ball leaving before
// any motion), and balls above reach height simply fly over its head.
// Extended 2026-08-27 (rules grill, "the racket is not snapped to hand...
// no hip and arm, wrist movement"): the racket now sits in a HandSocket at
// the end of a runtime-built procedural arm (torso→shoulder→wrist chain,
// the robot mesh is unrigged), and a swing carries hip yaw, shoulder arc,
// and a late wrist snap — see CONTEXT.md "AI Swing".
//
// Difficulty (same playtest: "make it a bit easier, or add difficulty
// options"): Easy/Normal/Hard presets over speed, reaction, reach, shot
// pace, and a per-turn whiff chance (the AI deliberately lines up slightly
// off and swings through the miss — a believable whiff, not a freeze).
// Stepped from the Game Menu's DIFFICULTY row (2026-08-27 — supersedes the
// in-game A cycle, whose double-booking with the menu's A made changes
// silent), persisted to PlayerPrefs, announced on the Scoreboard. Defaults
// to Easy.
public class AIOpponent : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody ball;
    [SerializeField] private BallBounceEvents ballBounceEvents;
    [SerializeField] private BallFaultTracker ballFaultTracker;
    [SerializeField] private CourtBuilder courtBuilder;
    [SerializeField] private Transform racketVisual;

    [Header("Difficulty")]
    [SerializeField] private AIDifficulty difficulty = AIDifficulty.Easy;

    [Header("Positioning")]
    [SerializeField] private float baselineY = 1f;
    [Tooltip("Balls above this height fly over the AI's head — it cannot reach them.")]
    [SerializeField] private float reachHeight = 2.2f;
    [Tooltip("Shoulder joint of the runtime-built swing arm, local to the robot root.")]
    [SerializeField] private Vector3 shoulderLocalPosition = new Vector3(0.22f, 0.35f, 0.05f);

    [Header("Movement")]
    [SerializeField] private float dodgeRadius = 0.7f;
    [SerializeField] private float turnDegreesPerSecond = 300f;

    [Header("Reaction delay shape (base/max come from the difficulty preset)")]
    [SerializeField] private float extraDelayPerDegree = 0.004f; // ~0.36s at a 90-degree carom

    [Header("Shot")]
    [SerializeField] private float maxShotSpeed = 12f;
    [SerializeField] private float serveWindupSeconds = 1f;
    [Tooltip("A slower ball can't score a Body Hit — a dead ball rolling into the AI is not a point.")]
    [SerializeField] private float minBodyHitBallSpeed = 1f;

    public event System.Action<AIDifficulty> DifficultyChanged;
    public AIDifficulty Difficulty => difficulty;

    private const string DifficultyPrefKey = "CourtClash.AIDifficulty";

    // Set by ApplyDifficulty.
    private float maxMoveSpeed;
    private float baseReactionDelaySeconds;
    private float maxReactionDelaySeconds;
    private float strikeRange;
    private float shotFlightTime;
    private float missChance;

    // Court geometry from CourtBuilder (the net sits at the front edge of
    // the player's Guardian area, so netZ is negative, not zero).
    private float halfWidth = 2.2f;
    private float halfDepth = 2.5f;
    private float netZ;
    private float centerX;
    private Vector3 homePosition;

    private float? interceptX;
    private float currentMissOffset; // lateral whiff offset for this turn (0 = clean)
    private Coroutine reactCoroutine;
    private Side? previousLastTouch;
    private bool serving;
    private float lastShotTime = float.NegativeInfinity;

    private float swingTimer;
    private float swingDirection = 1f;
    private const float SwingDuration = 0.45f;

    // The procedural swing arm (BuildArm): torso yaw carries the robot
    // visual, shoulder sweeps the arm, the hand socket snaps the wrist. The
    // torso and shoulder rest at identity; the socket's rest is captured at
    // build time (it adopts the racket's authored scene pose).
    private Transform torsoPivot;
    private Transform shoulderPivot;
    private Transform handSocket;
    private Quaternion handSocketRestRotation;

    // Rigged path (2026-08-27): the robot model was auto-rigged (Unity AI /
    // Tripo Rigging Biped), so when its skeleton is present the swing drives
    // the REAL bones — hips, shoulder, elbow, wrist — and the racket rides in
    // the actual hand. The socket chain above stays as the fallback for an
    // unrigged robot visual.
    private bool rigged;
    private Transform waistBone, shoulderBone, elbowBone, wristBone;
    private Quaternion waistRest, shoulderRestRotation, elbowRest, wristRest;

    private BallContactSounds ballSounds;

    private void Awake()
    {
        homePosition = new Vector3(0f, baselineY, -1.6f);
        BuildArm();
        if (ball != null) ballSounds = ball.GetComponent<BallContactSounds>();

        difficulty = (AIDifficulty)PlayerPrefs.GetInt(DifficultyPrefKey, (int)AIDifficulty.Easy);
        ApplyDifficulty();
    }

    private void OnEnable()
    {
        if (ballBounceEvents != null) ballBounceEvents.Bounced += OnBallBounced;
        if (courtBuilder != null) courtBuilder.CourtBuilt += OnCourtBuilt;
    }

    private void OnDisable()
    {
        if (ballBounceEvents != null) ballBounceEvents.Bounced -= OnBallBounced;
        if (courtBuilder != null) courtBuilder.CourtBuilt -= OnCourtBuilt;
    }

    private void ApplyDifficulty()
    {
        switch (difficulty)
        {
            case AIDifficulty.Easy:
                maxMoveSpeed = 1.6f;
                baseReactionDelaySeconds = 0.35f;
                maxReactionDelaySeconds = 0.9f;
                strikeRange = 0.5f;
                shotFlightTime = 1.25f;
                missChance = 0.25f;
                break;
            case AIDifficulty.Normal:
                maxMoveSpeed = 2.5f;
                baseReactionDelaySeconds = 0.2f;
                maxReactionDelaySeconds = 0.6f;
                strikeRange = 0.6f;
                shotFlightTime = 1f;
                missChance = 0.1f;
                break;
            case AIDifficulty.Hard:
                maxMoveSpeed = 3.4f;
                baseReactionDelaySeconds = 0.1f;
                maxReactionDelaySeconds = 0.35f;
                strikeRange = 0.7f;
                shotFlightTime = 0.8f;
                missChance = 0.02f;
                break;
        }
    }

    // Stepped by the Game Menu's DIFFICULTY row (stick up/down). Takes
    // effect on the AI's next turn — whiff and reaction are rolled per turn.
    public void StepDifficulty(int step)
    {
        difficulty = (AIDifficulty)(((int)difficulty + step % 3 + 3) % 3);
        ApplyDifficulty();
        PlayerPrefs.SetInt(DifficultyPrefKey, (int)difficulty);
        PlayerPrefs.Save();
        DifficultyChanged?.Invoke(difficulty);
    }

    private void OnCourtBuilt(Vector3 halfExtents)
    {
        halfWidth = Mathf.Max(halfExtents.x - 0.3f, 0.5f);
        halfDepth = courtBuilder.HalfDepthPerSide;
        netZ = courtBuilder.NetZ;
        centerX = courtBuilder.CenterX;
        homePosition = new Vector3(centerX, baselineY, netZ - halfDepth * 0.6f);
        transform.position = homePosition;
    }

    private bool MyTurn => ballFaultTracker != null
        && ballFaultTracker.RallyLive
        && ballFaultTracker.LastTouch == Side.Player;

    private void Update()
    {
        UpdateSwingAnimation();
        if (ball == null || serving) return;

        // The player just hit — roll this turn's whiff and schedule a
        // re-prediction after the base reaction delay (bounces add their own,
        // angle-scaled, below).
        Side? lastTouch = ballFaultTracker != null ? ballFaultTracker.LastTouch : null;
        if (lastTouch != previousLastTouch)
        {
            previousLastTouch = lastTouch;
            if (lastTouch == Side.Player)
            {
                currentMissOffset = Random.value < missChance
                    ? (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.5f, 0.8f)
                    : 0f;
                ScheduleReaction(baseReactionDelaySeconds);
            }
        }

        if (MyTurn)
        {
            MoveToIntercept();
            FaceBall();
            TryStrike();
        }
        else
        {
            DodgeOrGoHome();
            FacePlayerSide();
        }
    }

    private void MoveToIntercept()
    {
        Vector3 target = interceptX.HasValue
            ? new Vector3(interceptX.Value + currentMissOffset, baselineY, homePosition.z)
            : homePosition;
        MoveTowards(target);
    }

    // The whole body turns toward the ball while playing it — the biggest
    // single readability win for an unrigged robot mesh.
    private void FaceBall()
    {
        TurnTowards(ball.position - transform.position);
    }

    private void FacePlayerSide()
    {
        TurnTowards(Vector3.forward);
    }

    private void TurnTowards(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, Quaternion.LookRotation(direction), turnDegreesPerSecond * Time.deltaTime);
    }

    private void TryStrike()
    {
        if (Time.time - lastShotTime < 0.5f) return;
        if (ball.position.z > netZ - 0.1f) return; // not on the AI's half yet

        Vector3 toBall = ball.position - transform.position;
        float horizontal = new Vector2(toBall.x, toBall.z).magnitude;
        float ballSpeed = ball.linearVelocity.magnitude;

        // Wind-up: start the swing while the ball is still on its way in, so
        // contact happens mid-swing instead of before any visible motion.
        if (horizontal < strikeRange + ballSpeed * 0.3f && ball.position.y < reachHeight + 0.5f)
        {
            BeginSwing(Vector3.Dot(toBall, transform.right) >= 0f ? 1f : -1f);
        }

        bool canReach = horizontal <= strikeRange && ball.position.y <= reachHeight;
        if (canReach && currentMissOffset == 0f)
        {
            PlayShot();
        }
        // With a whiff offset active the AI stands slightly off-line and the
        // started swing simply cuts through air — a believable miss.
    }

    // The computed shot: a ballistic arc to a random floor target on the
    // player's half. Solving v from target/flight-time gives natural-looking
    // lobs that clear the net without inverse-physics aiming.
    private void PlayShot()
    {
        Vector3 target = new Vector3(
            centerX + Random.Range(-halfWidth * 0.55f, halfWidth * 0.55f),
            0f,
            netZ + Random.Range(halfDepth * 0.3f, halfDepth * 0.75f));

        // Flight time is in game-seconds — Game Speed dilation slows the
        // whole world uniformly, so the solved arc is identical at every
        // speed setting (ADR 0001; the old per-shot flight-time stretch
        // solved ceiling-high arcs at low speeds and looped AI serve lets).
        float flightTime = shotFlightTime * Random.Range(0.85f, 1.15f);
        Vector3 velocity = (target - ball.position) / flightTime
            - 0.5f * Physics.gravity * flightTime;
        velocity = Vector3.ClampMagnitude(velocity, maxShotSpeed);

        ball.linearVelocity = velocity;
        lastShotTime = Time.time;
        BeginSwing(swingDirection);
        interceptX = null;

        // The AI's Hit is computed, not collided — voice it explicitly,
        // scaled the way the player's hits scale. Serves come through here
        // too (ServeRoutine ends in PlayShot).
        ballSounds?.PlayRacketHit(velocity.magnitude / maxShotSpeed);

        ballFaultTracker?.NotifyTouched(Side.AI);
    }

    // Serve flow, driven by MatchController when it's the AI's serve: float
    // the ball at the racket, wind up, then play a normal computed shot.
    public void BeginServe()
    {
        StartCoroutine(ServeRoutine());
    }

    private IEnumerator ServeRoutine()
    {
        if (ball == null) yield break;
        serving = true;

        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        ball.isKinematic = true;

        float elapsed = 0f;
        while (elapsed < serveWindupSeconds)
        {
            ball.position = transform.position + transform.rotation * new Vector3(0.35f, 0.15f, 0.35f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        ball.isKinematic = false;
        PlayShot();
        serving = false;
    }

    // Not my turn: stay out of the ball's way (a Body Hit is always a point
    // to the player), otherwise drift home.
    private void DodgeOrGoHome()
    {
        if (BallPathThreatensMe(out float closestX))
        {
            float away = transform.position.x >= closestX ? 1f : -1f;
            Vector3 target = new Vector3(
                transform.position.x + away * dodgeRadius * 2f, baselineY, homePosition.z);
            MoveTowards(target);
        }
        else
        {
            MoveTowards(homePosition);
        }
    }

    // Coarse ballistic sampling of the ball's next ~0.8s: does its path pass
    // within dodgeRadius of me horizontally?
    private bool BallPathThreatensMe(out float closestX)
    {
        closestX = 0f;
        if (ball.isKinematic) return false;

        Vector3 p = ball.position;
        Vector3 v = ball.linearVelocity;
        if (v.sqrMagnitude < 1f) return false;

        float bestDistance = float.MaxValue;
        for (float t = 0f; t <= 0.8f; t += 0.1f)
        {
            Vector3 sample = p + v * t + 0.5f * Physics.gravity * t * t;
            Vector3 offset = sample - transform.position;
            float horizontal = new Vector2(offset.x, offset.z).magnitude;
            if (horizontal < bestDistance)
            {
                bestDistance = horizontal;
                closestX = sample.x;
            }
        }
        return bestDistance < dodgeRadius;
    }

    private void MoveTowards(Vector3 target)
    {
        target.x = Mathf.Clamp(target.x, centerX - halfWidth, centerX + halfWidth);
        target.z = Mathf.Clamp(target.z, netZ - halfDepth + 0.4f, netZ - 0.6f);
        target.y = baselineY;
        transform.position = Vector3.MoveTowards(
            transform.position, target, maxMoveSpeed * Time.deltaTime);
    }

    private void OnBallBounced(Vector3 velocityBefore, Vector3 velocityAfter)
    {
        float deviationDegrees = TrajectoryPredictor.BounceDeviationDegrees(velocityBefore, velocityAfter);
        float delay = Mathf.Min(
            baseReactionDelaySeconds + deviationDegrees * extraDelayPerDegree,
            maxReactionDelaySeconds);
        ScheduleReaction(delay);
    }

    private void ScheduleReaction(float delaySeconds)
    {
        if (reactCoroutine != null) StopCoroutine(reactCoroutine);
        reactCoroutine = StartCoroutine(ReactAfterDelay(delaySeconds));
    }

    private IEnumerator ReactAfterDelay(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        RePredictIntercept();
    }

    private void RePredictIntercept()
    {
        if (ball == null || ball.isKinematic) return;

        bool found = TrajectoryPredictor.TryPredictPlaneCrossing(
            ball.position, ball.linearVelocity, Physics.gravity.y, homePosition.z,
            out Vector3 predicted, out _);

        interceptX = found
            ? Mathf.Clamp(predicted.x, centerX - halfWidth, centerX + halfWidth)
            : (float?)null;
    }

    // The Body Hit sensor: this GameObject's trigger capsule. Any live-rally
    // ball contact is a point to the player — no matter who hit it last.
    private void OnTriggerEnter(Collider other)
    {
        if (ball == null || other.attachedRigidbody != ball) return;
        if (serving) return;
        if (Time.time - lastShotTime < 0.3f) return; // my own shot leaving
        if (ball.linearVelocity.magnitude < minBodyHitBallSpeed) return;

        ballFaultTracker?.NotifyBodyHit(Side.AI);
    }

    private void BeginSwing(float direction)
    {
        if (swingTimer > 0f) return;
        swingTimer = SwingDuration;
        swingDirection = direction;
    }

    // --- Procedural swing arm (2026-08-27; see CONTEXT.md "AI Swing") ---

    // The robot mesh is a single unrigged blob, so the arm is built in code:
    // TorsoPivot (hip yaw, carries the whole robot visual) → ShoulderPivot →
    // HandSocket (wrist), with the racket parented INTO the socket so it is
    // genuinely held. The socket is created at the racket's authored scene
    // pose, so nothing visibly moves on startup and every pivot rotation
    // swings the racket around the hand, not around thin air.
    private void BuildArm()
    {
        if (racketVisual == null) return;

        Transform robotVisual = null;
        foreach (Transform child in transform)
        {
            if (child != racketVisual)
            {
                robotVisual = child;
                break;
            }
        }

        // Rigged robot: bind to its skeleton and put the racket in the hand.
        if (robotVisual != null)
        {
            waistBone = FindDeep(robotVisual, "Waist");

            // The glTF handedness flip mirrors the rig's L_/R_ names relative
            // to the world, so trust geometry over labels: take the hand bone
            // physically closest to the racket's authored rest position, and
            // its side's arm chain with it.
            Transform rHand = FindDeep(robotVisual, "R_Hand");
            Transform lHand = FindDeep(robotVisual, "L_Hand");
            Transform hand = rHand;
            if (lHand != null && (rHand == null
                || (lHand.position - racketVisual.position).sqrMagnitude
                    < (rHand.position - racketVisual.position).sqrMagnitude))
            {
                hand = lHand;
            }
            if (hand != null)
            {
                string side = hand.name.StartsWith("L_") ? "L_" : "R_";
                shoulderBone = FindDeep(robotVisual, side + "Upperarm");
                elbowBone = FindDeep(robotVisual, side + "Forearm");
                wristBone = hand;
            }

            if (wristBone != null && shoulderBone != null)
            {
                racketVisual.position = wristBone.position; // snap to the hand, keep the authored tilt
                racketVisual.SetParent(wristBone, true);
                if (waistBone != null) waistRest = waistBone.localRotation;
                shoulderRestRotation = shoulderBone.localRotation;
                if (elbowBone != null) elbowRest = elbowBone.localRotation;
                wristRest = wristBone.localRotation;
                rigged = true;
                return;
            }
        }

        torsoPivot = new GameObject("TorsoPivot").transform;
        torsoPivot.SetParent(transform, false);
        if (robotVisual != null) robotVisual.SetParent(torsoPivot, true);

        shoulderPivot = new GameObject("ShoulderPivot").transform;
        shoulderPivot.SetParent(torsoPivot, false);
        shoulderPivot.localPosition = shoulderLocalPosition;

        handSocket = new GameObject("HandSocket").transform;
        handSocket.SetParent(shoulderPivot, true);
        handSocket.position = racketVisual.position;
        handSocket.rotation = racketVisual.rotation;
        racketVisual.SetParent(handSocket, true);
        handSocketRestRotation = handSocket.localRotation;

        // No capsule arm mesh here on purpose (2026-08-27 playtest: the
        // robot model turned out to HAVE arms — a built one overlapped its
        // right arm as a third limb). The pivots stay invisible until the
        // robot itself is rigged; the racket still swings from the socket.
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
    }

    private void UpdateSwingAnimation()
    {
        if (rigged) UpdateRiggedSwing();
        else if (handSocket != null) UpdateSocketSwing();
    }

    // Bone axis conventions vary per rig, so every joint sweeps about the
    // WORLD vertical converted into its parent's space each frame — a
    // horizontal swing regardless of how the rig authored its local axes.
    // Applied parent-first (waist → shoulder → elbow → wrist) so each
    // conversion sees the joints above it already posed this frame.
    private static void ApplyBoneYaw(Transform bone, Quaternion rest, float degrees)
    {
        if (bone == null) return;
        Vector3 axis = bone.parent != null
            ? bone.parent.InverseTransformDirection(Vector3.up)
            : Vector3.up;
        bone.localRotation = Quaternion.AngleAxis(degrees, axis) * rest;
    }

    // The kinetic chain on the real skeleton: hips coil and lead, shoulder
    // carries the arc, elbow follows, wrist lags then snaps through contact.
    private void UpdateRiggedSwing()
    {
        if (swingTimer <= 0f)
        {
            float ease = 8f * Time.deltaTime;
            if (waistBone != null)
                waistBone.localRotation = Quaternion.Slerp(waistBone.localRotation, waistRest, ease);
            if (shoulderBone != null)
                shoulderBone.localRotation = Quaternion.Slerp(shoulderBone.localRotation, shoulderRestRotation, ease);
            if (elbowBone != null)
                elbowBone.localRotation = Quaternion.Slerp(elbowBone.localRotation, elbowRest, ease);
            if (wristBone != null)
                wristBone.localRotation = Quaternion.Slerp(wristBone.localRotation, wristRest, ease);
            return;
        }

        swingTimer -= Time.deltaTime;
        float progress = 1f - Mathf.Max(swingTimer, 0f) / SwingDuration;

        float waist, shoulder, elbow, wrist;
        if (progress < 0.3f)
        {
            float p = progress / 0.3f;
            waist = Mathf.Lerp(0f, -25f, p);
            shoulder = Mathf.Lerp(0f, -60f, p);
            elbow = Mathf.Lerp(0f, -15f, p);
            wrist = Mathf.Lerp(0f, -30f, p);
        }
        else
        {
            float p = (progress - 0.3f) / 0.7f;
            waist = Mathf.Lerp(-25f, 30f, Mathf.Sin(p * Mathf.PI * 0.5f)); // hips lead: eased out
            shoulder = Mathf.Lerp(-60f, 100f, p);
            elbow = Mathf.Lerp(-15f, 35f, p);
            wrist = Mathf.Lerp(-30f, 45f, p * p);                          // wrist lags, snaps late
        }

        ApplyBoneYaw(waistBone, waistRest, waist * swingDirection);
        ApplyBoneYaw(shoulderBone, shoulderRestRotation, shoulder * swingDirection);
        ApplyBoneYaw(elbowBone, elbowRest, elbow * swingDirection);
        ApplyBoneYaw(wristBone, wristRest, wrist * swingDirection);
    }

    // Wind-up, sweep, follow-through across three joints, all pre-multiplied
    // about the parent's vertical axis (the old single-pivot version's
    // lesson: post-multiplying about the racket's own up — its handle axis —
    // just twirled it invisibly in place). The hips coil first and lead the
    // sweep, the shoulder carries the arc, and the wrist lags then snaps
    // through contact — the standard kinetic-chain read. Eases back to rest.
    private void UpdateSocketSwing()
    {
        if (swingTimer <= 0f)
        {
            float ease = 8f * Time.deltaTime;
            torsoPivot.localRotation = Quaternion.Slerp(
                torsoPivot.localRotation, Quaternion.identity, ease);
            shoulderPivot.localRotation = Quaternion.Slerp(
                shoulderPivot.localRotation, Quaternion.identity, ease);
            handSocket.localRotation = Quaternion.Slerp(
                handSocket.localRotation, handSocketRestRotation, ease);
            return;
        }

        swingTimer -= Time.deltaTime;
        float progress = 1f - Mathf.Max(swingTimer, 0f) / SwingDuration;

        float torso, shoulder, wrist;
        if (progress < 0.3f)
        {
            float p = progress / 0.3f;
            torso = Mathf.Lerp(0f, -25f, p);
            shoulder = Mathf.Lerp(0f, -70f, p);
            wrist = Mathf.Lerp(0f, -30f, p);
        }
        else
        {
            float p = (progress - 0.3f) / 0.7f;
            torso = Mathf.Lerp(-25f, 30f, Mathf.Sin(p * Mathf.PI * 0.5f)); // hips lead: eased out
            shoulder = Mathf.Lerp(-70f, 110f, p);
            wrist = Mathf.Lerp(-30f, 45f, p * p);                          // wrist lags, snaps late
        }

        torsoPivot.localRotation = Quaternion.AngleAxis(torso * swingDirection, Vector3.up);
        shoulderPivot.localRotation = Quaternion.AngleAxis(shoulder * swingDirection, Vector3.up);
        handSocket.localRotation =
            Quaternion.AngleAxis(wrist * swingDirection, Vector3.up) * handSocketRestRotation;
    }
}
