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
// any motion), the racket sweeps horizontally on the ball's side, and balls
// above reach height simply fly over its head.
//
// Difficulty (same playtest: "make it a bit easier, or add difficulty
// options"): Easy/Normal/Hard presets over speed, reaction, reach, shot
// pace, and a per-turn whiff chance (the AI deliberately lines up slightly
// off and swings through the miss — a believable whiff, not a freeze).
// Cycled with the right controller's A button, persisted to PlayerPrefs,
// announced on the Scoreboard. Defaults to Easy.
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
    private Quaternion racketRestRotation;

    private void Awake()
    {
        homePosition = new Vector3(0f, baselineY, -1.6f);
        if (racketVisual != null) racketRestRotation = racketVisual.localRotation;

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

    public void CycleDifficulty()
    {
        difficulty = (AIDifficulty)(((int)difficulty + 1) % 3);
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
        homePosition = new Vector3(0f, baselineY, netZ - halfDepth * 0.6f);
        transform.position = homePosition;
    }

    private bool MyTurn => ballFaultTracker != null
        && ballFaultTracker.RallyLive
        && ballFaultTracker.LastTouch == Side.Player;

    private void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            CycleDifficulty();
        }

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
            Random.Range(-halfWidth * 0.55f, halfWidth * 0.55f),
            0f,
            netZ + Random.Range(halfDepth * 0.3f, halfDepth * 0.75f));

        float flightTime = shotFlightTime * Random.Range(0.85f, 1.15f);
        Vector3 velocity = (target - ball.position) / flightTime
            - 0.5f * Physics.gravity * flightTime;
        velocity = Vector3.ClampMagnitude(velocity, maxShotSpeed);

        ball.linearVelocity = velocity;
        lastShotTime = Time.time;
        BeginSwing(swingDirection);
        interceptX = null;

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
        target.x = Mathf.Clamp(target.x, -halfWidth, halfWidth);
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

        interceptX = found ? Mathf.Clamp(predicted.x, -halfWidth, halfWidth) : (float?)null;
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

    // Backswing then follow-through, sweeping the racket horizontally around
    // the BODY's vertical axis (pre-multiplied, i.e. parent space). The first
    // version post-multiplied about the racket's own local up — which is its
    // handle axis, so the racket just twirled invisibly in place (the user's
    // "still shows no animation" report). Eases back to rest afterwards.
    private void UpdateSwingAnimation()
    {
        if (racketVisual == null) return;

        if (swingTimer <= 0f)
        {
            racketVisual.localRotation = Quaternion.Slerp(
                racketVisual.localRotation, racketRestRotation, 8f * Time.deltaTime);
            return;
        }

        swingTimer -= Time.deltaTime;
        float progress = 1f - Mathf.Max(swingTimer, 0f) / SwingDuration;
        float angle = progress < 0.3f
            ? Mathf.Lerp(0f, -70f, progress / 0.3f)          // wind back
            : Mathf.Lerp(-70f, 110f, (progress - 0.3f) / 0.7f); // sweep through
        racketVisual.localRotation =
            Quaternion.AngleAxis(angle * swingDirection, Vector3.up) * racketRestRotation;
    }
}
