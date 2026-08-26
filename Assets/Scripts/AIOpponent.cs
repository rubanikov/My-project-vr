using System.Collections;
using UnityEngine;

// Court Clash's AI opponent, v1 scope per wayfinder ticket 03
// (.scratch/court-clash/issues/03-ai-opponent-v1-scope.md):
//   - Single fixed difficulty, no selector.
//   - Capped lateral movement speed + a bounce-angle-scaled reaction delay
//     make it beatable — it does not always reach the intercept point.
//   - Random aim within a cone on throw-back (gap-aiming deferred past v1).
//
// Movement is lateral (X) only at a fixed baseline Z/Y — a standing AI
// stepping sideways to meet the ball, not a full 3D chase. Matches ticket
// 03's "fast human lateral step" framing.
//
// Catching is a pure distance check (TryCatchBall), not a physics
// collision/trigger — this script has no dependency on a Collider. The
// placeholder capsule's own CapsuleCollider must stay a trigger (or be
// removed), never solid: confirmed by testing that a solid collider here
// violently ejects the ball via depenetration the instant it's placed
// within catch range (the catch radius sits inside the capsule's own
// radius), corrupting the catch before the script logic can run.
public class AIOpponent : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody ball;
    [SerializeField] private BallBounceEvents ballBounceEvents;
    [SerializeField] private BallFaultTracker ballFaultTracker;
    [Tooltip("Optional. When wired, the AI repositions itself proportionally once the court's real " +
        "size is known (see OnCourtBuilt) instead of trusting its authored scene position, which was " +
        "only ever tuned against CourtBuilder's fixed 2.5m fallback.")]
    [SerializeField] private CourtBuilder courtBuilder;

    // Every positioning/tuning value below was authored against CourtBuilder's
    // fixed 2.5m fallback half-extent (the only size ever seen in Editor/
    // Simulator testing). On a real headset, CourtBuilder instead sizes the
    // court to the player's actual Guardian boundary, which can be smaller —
    // confirmed 2026-08-26: the AI "did not show up" on real hardware
    // because its hardcoded start position sat outside the real, smaller
    // court's walls. OnCourtBuilt rescales all of these proportionally once
    // the real size is known; the values below just stay as a reasonable
    // starting point for however long that takes (or if courtBuilder isn't wired).
    private const float TunedHalfExtent = 2.5f;

    [Header("Positioning (tuned for a 2.5m court — see OnCourtBuilt)")]
    [Tooltip("World-space Z the AI predicts the ball crossing to decide where to stand.")]
    [SerializeField] private float catchPlaneZ = -2f;
    [SerializeField] private float baselineY = 1f;
    [SerializeField] private float catchRadius = 0.4f;
    [Tooltip("Clamps the predicted target X to the court's playable width. A chaotic multi-bounce " +
        "trajectory off the faceted walls can otherwise extrapolate a target far outside the court " +
        "(observed: -1.75 in a 1m-half-extent test court), walking the AI off the play area entirely. " +
        "Kept a margin inside CourtBuilder's fallbackHalfWidth (2.5m) rather than matching it exactly.")]
    [SerializeField] private float courtHalfWidth = 2.2f;

    [Header("Movement cap")]
    [Tooltip("A fast human lateral step, per ticket 03 — tune by feel during playtesting.")]
    [SerializeField] private float maxMoveSpeed = 2.5f;

    [Header("Reaction delay (stacks with the movement cap, doesn't replace it)")]
    [SerializeField] private float baseReactionDelaySeconds = 0.175f;
    [SerializeField] private float extraDelayPerDegree = 0.004f; // ~0.36s at a 90-degree carom
    [SerializeField] private float maxReactionDelaySeconds = 0.6f;

    [Header("Throw-back")]
    [SerializeField] private float throwSpeed = 4f;
    [SerializeField] private float aimConeHalfAngleDegrees = 15f;

    private float? targetX;
    private Coroutine reactCoroutine;

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
        float scaleX = halfExtents.x / TunedHalfExtent;
        float scaleZ = halfExtents.z / TunedHalfExtent;

        transform.position = new Vector3(transform.position.x, baselineY, -1.6f * scaleZ);
        catchPlaneZ = -2f * scaleZ;
        courtHalfWidth = 2.2f * scaleX;
    }

    private void OnBallBounced(Vector3 velocityBefore, Vector3 velocityAfter)
    {
        float deviationDegrees = TrajectoryPredictor.BounceDeviationDegrees(velocityBefore, velocityAfter);
        float delay = Mathf.Min(
            baseReactionDelaySeconds + deviationDegrees * extraDelayPerDegree,
            maxReactionDelaySeconds);

        if (reactCoroutine != null) StopCoroutine(reactCoroutine);
        reactCoroutine = StartCoroutine(ReactAfterDelay(delay));
    }

    private IEnumerator ReactAfterDelay(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        RePredictTarget();
    }

    private void RePredictTarget()
    {
        if (ball == null) return;

        bool found = TrajectoryPredictor.TryPredictPlaneCrossing(
            ball.position, ball.linearVelocity, Physics.gravity.y, catchPlaneZ,
            out Vector3 predicted, out _);

        targetX = found ? Mathf.Clamp(predicted.x, -courtHalfWidth, courtHalfWidth) : (float?)null;
    }

    private void Update()
    {
        if (targetX.HasValue)
        {
            float newX = Mathf.MoveTowards(transform.position.x, targetX.Value, maxMoveSpeed * Time.deltaTime);
            transform.position = new Vector3(newX, baselineY, transform.position.z);
        }

        TryCatchBall();
    }

    // Horizontal (XZ) distance only. The AI's catch point stays at a fixed
    // baselineY (it doesn't crouch/reach), but the ball's real height varies
    // a lot as it bounces and settles — a 3D-distance check would mean the
    // AI can basically never catch anything once the ball nears the floor
    // (confirmed by a scripted throw during testing: ball ended up ~1m away
    // in 3D despite landing right next to the AI horizontally). Treat any
    // horizontal proximity as within reach, representing the AI bending/
    // reaching for it.
    private void TryCatchBall()
    {
        if (ball == null) return;
        Vector3 toBall = ball.position - transform.position;
        float horizontalDistance = new Vector2(toBall.x, toBall.z).magnitude;
        if (horizontalDistance > catchRadius) return;

        ballFaultTracker?.NotifyTouched(Side.AI);
        ThrowBack();
    }

    private void ThrowBack()
    {
        // Random aim within a cone pointed back toward the player's side
        // (-Z from the AI's perspective, since the AI stands at the +Z end
        // of the court per catchPlaneZ being negative-of-the-player-side).
        Vector3 aimDirection = Quaternion.Euler(
            Random.Range(-aimConeHalfAngleDegrees, aimConeHalfAngleDegrees),
            Random.Range(-aimConeHalfAngleDegrees, aimConeHalfAngleDegrees),
            0f) * Vector3.back;

        ball.linearVelocity = aimDirection.normalized * throwSpeed;

        targetX = null;
        if (reactCoroutine != null)
        {
            StopCoroutine(reactCoroutine);
            reactCoroutine = null;
        }
    }
}
