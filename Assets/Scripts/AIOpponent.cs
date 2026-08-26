using System.Collections;
using UnityEngine;

// Court Clash's AI padel opponent, v2 (2026-08-26 padel conversion — see
// CONTEXT.md for Turn/Dodge/Body Hit/Serve vocabulary; supersedes the v1
// catch-and-throw opponent from wayfinder ticket 03):
//   - Roams its own half in 2D (X and Z) with a capped speed and the same
//     bounce-scaled reaction delay that made v1 beatable.
//   - Carries a Tron racket and swings it on its Turn; the swing is
//     presentation — the shot itself is computed (a ballistic arc to a
//     random target on the player's half), which keeps aiming reliable and
//     difficulty tunable.
//   - Dodges the ball when it is NOT its turn: any ball-body contact is a
//     point to the player (the trigger capsule on this GameObject is the
//     Body Hit sensor).
//   - Serves when MatchController says so: floats the ball at its racket,
//     winds up, then plays a computed serve shot.
public class AIOpponent : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody ball;
    [SerializeField] private BallBounceEvents ballBounceEvents;
    [SerializeField] private BallFaultTracker ballFaultTracker;
    [SerializeField] private CourtBuilder courtBuilder;
    [SerializeField] private Transform racketVisual;

    [Header("Positioning (rescaled to the real court by OnCourtBuilt)")]
    [SerializeField] private float baselineY = 1f;
    [Tooltip("Horizontal reach for a swing. Deliberately larger than the body capsule so the " +
        "racket connects before the ball can reach the Body Hit sensor.")]
    [SerializeField] private float strikeRange = 0.6f;

    [Header("Movement")]
    [SerializeField] private float maxMoveSpeed = 2.5f;
    [SerializeField] private float dodgeRadius = 0.7f;

    [Header("Reaction delay (stacks with the movement cap, doesn't replace it)")]
    [SerializeField] private float baseReactionDelaySeconds = 0.175f;
    [SerializeField] private float extraDelayPerDegree = 0.004f; // ~0.36s at a 90-degree carom
    [SerializeField] private float maxReactionDelaySeconds = 0.6f;

    [Header("Shot")]
    [SerializeField] private float shotFlightTime = 0.9f;
    [SerializeField] private float maxShotSpeed = 12f;
    [SerializeField] private float serveWindupSeconds = 1f;
    [Tooltip("A slower ball can't score a Body Hit — a dead ball rolling into the AI is not a point.")]
    [SerializeField] private float minBodyHitBallSpeed = 1f;

    // Court geometry, taken from CourtBuilder once the real size is known
    // (the AI once failed to show up on-device because its authored position
    // sat outside a smaller room). The AI's half spans [netZ - halfDepth,
    // netZ] — the net sits at the front edge of the player's Guardian area,
    // so netZ is negative, not zero.
    private float halfWidth = 2.2f;
    private float halfDepth = 2.5f;
    private float netZ;
    private Vector3 homePosition;

    private float? interceptX;
    private Coroutine reactCoroutine;
    private Side? previousLastTouch;
    private bool serving;
    private float lastShotTime = float.NegativeInfinity;

    private float swingTimer;
    private const float SwingDuration = 0.3f;
    private Quaternion racketRestRotation;

    private void Awake()
    {
        homePosition = new Vector3(0f, baselineY, -1.6f);
        if (racketVisual != null) racketRestRotation = racketVisual.localRotation;
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
        UpdateSwingAnimation();
        if (ball == null || serving) return;

        // The player just hit — schedule a re-prediction after the base
        // reaction delay (bounces add their own, angle-scaled, below).
        Side? lastTouch = ballFaultTracker != null ? ballFaultTracker.LastTouch : null;
        if (lastTouch != previousLastTouch)
        {
            previousLastTouch = lastTouch;
            if (lastTouch == Side.Player) ScheduleReaction(baseReactionDelaySeconds);
        }

        if (MyTurn)
        {
            MoveToIntercept();
            TryStrike();
        }
        else
        {
            DodgeOrGoHome();
        }
    }

    private void MoveToIntercept()
    {
        Vector3 target = interceptX.HasValue
            ? new Vector3(interceptX.Value, baselineY, homePosition.z)
            : homePosition;
        MoveTowards(target);
    }

    private void TryStrike()
    {
        if (Time.time - lastShotTime < 0.5f) return;
        if (ball.position.z > netZ - 0.1f) return; // not on the AI's half yet

        Vector3 toBall = ball.position - transform.position;
        if (new Vector2(toBall.x, toBall.z).magnitude > strikeRange) return;

        PlayShot();
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
        swingTimer = SwingDuration;
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
            ball.position = transform.position + new Vector3(0.35f, 0.15f, 0.35f);
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

    private void UpdateSwingAnimation()
    {
        if (racketVisual == null || swingTimer <= 0f) return;

        swingTimer -= Time.deltaTime;
        float progress = 1f - Mathf.Max(swingTimer, 0f) / SwingDuration;
        float angle = Mathf.Sin(progress * Mathf.PI) * -90f;
        racketVisual.localRotation = racketRestRotation * Quaternion.AngleAxis(angle, Vector3.right);
    }
}
