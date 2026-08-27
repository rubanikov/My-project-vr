using UnityEngine;

// Physics escape hatch (2026-08-27 playtest: "the ball disappears some
// times"): when the ball tunnels out of the court shell, nothing it touches
// is the CourtFloor any more, so no fault can ever fire — the ball is gone
// AND the rally hangs. Watch for the ball sitting outside the built court
// (or under the floor) for a short continuous window, then hand it back to
// MatchController as a void rally: re-serve, no point.
[RequireComponent(typeof(Rigidbody))]
public class BallEscapeWatchdog : MonoBehaviour
{
    [SerializeField] private CourtBuilder courtBuilder;
    [SerializeField] private MatchController matchController;
    [Tooltip("How far outside the court shell still counts as inside — contacts poke through thin walls briefly.")]
    [SerializeField] private float margin = 0.75f;
    [SerializeField] private float escapeSeconds = 0.6f;

    private float escapedTime;

    private void Awake()
    {
        if (courtBuilder == null) courtBuilder = FindFirstObjectByType<CourtBuilder>();
        if (matchController == null) matchController = FindFirstObjectByType<MatchController>();
    }

    private void Update()
    {
        if (courtBuilder == null || matchController == null) return;
        if (courtBuilder.HalfDepthPerSide <= 0f) return; // court not built yet

        if (!IsOutside())
        {
            escapedTime = 0f;
            return;
        }

        escapedTime += Time.deltaTime;
        if (escapedTime < escapeSeconds) return;

        escapedTime = 0f;
        matchController.RecoverEscapedBall();
    }

    private bool IsOutside()
    {
        Vector3 p = transform.position;
        if (p.y < -0.5f) return true;
        if (Mathf.Abs(p.x - courtBuilder.CenterX) > courtBuilder.HalfExtents.x + margin) return true;
        return p.z < courtBuilder.CourtMinZ - margin || p.z > courtBuilder.CourtMaxZ + margin;
    }
}
