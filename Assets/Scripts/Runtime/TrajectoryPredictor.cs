using UnityEngine;

// Standard projectile-motion extrapolation for the AI opponent's ball
// prediction (PRD.md: "predicts the ball's trajectory ... standard physics
// extrapolation — not exotic"). Pure math, no MonoBehaviour/scene dependency,
// so it's directly unit-testable.
//
// Known simplification (v1, matches "not exotic"): this extrapolates the
// CURRENT velocity segment only — it does not account for a wall bounce
// that might happen between now and the predicted crossing. The AI opponent
// re-predicts on every bounce (see AIOpponent.cs), so it self-corrects a
// moment after any bounce actually happens; it just can't see one coming in
// advance. Good enough for v1 per the resolved AI-opponent-scope decision.
public static class TrajectoryPredictor
{
    // Predicts where/when a projectile starting at `position` with `velocity`
    // under `gravity` (a negative Y acceleration, e.g. Physics.gravity.y)
    // crosses the world-space plane z == targetZ. Returns false if the ball
    // is moving away from/parallel to that plane (never reaches it).
    public static bool TryPredictPlaneCrossing(
        Vector3 position, Vector3 velocity, float gravity, float targetZ,
        out Vector3 predictedPosition, out float timeToReach)
    {
        predictedPosition = default;
        timeToReach = 0f;

        // Z motion has no acceleration (gravity is Y-only), so this is linear:
        // targetZ = position.z + velocity.z * t  =>  t = (targetZ - position.z) / velocity.z
        const float minSpeed = 0.0001f;
        if (Mathf.Abs(velocity.z) < minSpeed) return false;

        float t = (targetZ - position.z) / velocity.z;
        if (t < 0f) return false;

        timeToReach = t;
        predictedPosition = new Vector3(
            position.x + velocity.x * t,
            position.y + velocity.y * t + 0.5f * gravity * t * t,
            targetZ);
        return true;
    }

    // Angle in degrees between a ball's velocity just before and just after
    // a bounce — used to scale the AI's reaction delay (a sharper, more
    // unexpected carom costs more reaction time; see AIOpponent.cs).
    public static float BounceDeviationDegrees(Vector3 velocityBefore, Vector3 velocityAfter)
    {
        if (velocityBefore.sqrMagnitude < 0.0001f || velocityAfter.sqrMagnitude < 0.0001f) return 0f;
        return Vector3.Angle(velocityBefore, velocityAfter);
    }
}
