using System;
using System.Collections;
using UnityEngine;

// Builds Court Clash's faceted arena at runtime, sized to the player's real
// Quest Guardian play area (OVRBoundary) so it always fits their room.
public class CourtBuilder : MonoBehaviour
{
    // Fires once the court's real size is known (real Guardian boundary or
    // the fallback), with the final half-extents used. AIOpponent listens to
    // this to reposition itself proportionally — its own tuned position was
    // authored against the fixed 2.5m fallback and would otherwise end up
    // outside a smaller real room's walls (found 2026-08-26: the AI "did not
    // show up" on real hardware, root cause was exactly this mismatch).
    public event Action<Vector3> CourtBuilt;
    public Vector3 HalfExtents { get; private set; }

    [Header("Fallback size (used when no Guardian boundary is available, e.g. Editor Play mode)")]
    [Tooltip("Was 1m — too small relative to wallHeight (2.5m), reading as a tall narrow " +
        "cone/tower instead of a room, and leaving too little margin for AI positioning near " +
        "the court edge (confirmed by testing: the AI's clamped edge position plus a small " +
        "offset landed outside the floor collider entirely). 2.5m matches a modest real room.")]
    [SerializeField] private float fallbackHalfWidth = 2.5f;
    [SerializeField] private float fallbackHalfDepth = 2.5f;

    [Header("Shape")]
    [Tooltip("Was 8 individually-tilted panels arranged in a ring — each one leaning inward by " +
        "a random amount left visible gaps between neighbors (confirmed in playtesting). " +
        "Switched to a plain 4-wall rectangular prism: flush corners, no gaps. Varied bounce " +
        "geometry (angled panels, polygon inserts) can be added later as objects attached to " +
        "these flat walls, rather than baked into the walls themselves.")]
    [SerializeField] private float wallHeight = 2.5f;
    [SerializeField] private float wallThickness = 0.15f;

    [Header("Materials (optional — defaults to Unity's built-in material if left empty)")]
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Material floorMaterial;

    // How long to wait for the OpenXR session to report real boundary data
    // before giving up and using the fallback size. Discovered empirically:
    // OVRManager's XR session isn't "running" yet on the very first frame
    // (GetBoundaryDimensions logs "isSessionRunning == false" if queried in
    // Start()), so a single Start()-time read always misses real boundary
    // data even when a Simulator/headset is present.
    [SerializeField] private float boundaryWaitTimeoutSeconds = 2f;

    private void Start()
    {
        StartCoroutine(BuildWhenBoundaryReady());
    }

    private IEnumerator BuildWhenBoundaryReady()
    {
        Vector3 halfExtents = GetFallbackHalfExtents();
        float deadline = Time.unscaledTime + boundaryWaitTimeoutSeconds;

        while (Time.unscaledTime < deadline)
        {
            if (TryGetPlayAreaHalfExtents(out Vector3 realHalfExtents))
            {
                halfExtents = realHalfExtents;
                break;
            }
            yield return null;
        }

        BuildFloor(halfExtents);
        BuildWalls(halfExtents);

        HalfExtents = halfExtents;
        CourtBuilt?.Invoke(halfExtents);
    }

    private Vector3 GetFallbackHalfExtents() => new Vector3(fallbackHalfWidth, 0f, fallbackHalfDepth);

    // OVRBoundary reports 0 (or is unavailable) until the OpenXR session is
    // fully running — true for both "no headset/Simulator at all" (plain
    // Editor Play mode) and "headset present but session still starting up".
    // Either way, treat it as "not ready yet" and let the caller keep polling
    // or fall back to a safe default.
    private bool TryGetPlayAreaHalfExtents(out Vector3 halfExtents)
    {
        const float minSaneDimension = 0.5f;
        if (OVRManager.boundary != null)
        {
            Vector3 dimensions = OVRManager.boundary.GetDimensions(OVRBoundary.BoundaryType.PlayArea);
            if (dimensions.x > minSaneDimension && dimensions.z > minSaneDimension)
            {
                halfExtents = new Vector3(dimensions.x * 0.5f, 0f, dimensions.z * 0.5f);
                return true;
            }
        }
        halfExtents = default;
        return false;
    }

    private void BuildFloor(Vector3 halfExtents)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "CourtFloor";
        floor.transform.SetParent(transform, false);
        floor.transform.localScale = new Vector3(halfExtents.x * 2f, 0.1f, halfExtents.z * 2f);
        floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        ApplyMaterial(floor, floorMaterial);
    }

    // Four flat walls forming a rectangular room, flush at the corners.
    private void BuildWalls(Vector3 halfExtents)
    {
        float fullWidth = halfExtents.x * 2f;
        float fullDepth = halfExtents.z * 2f;
        float halfHeight = wallHeight * 0.5f;

        BuildWall("CourtWall_North", new Vector3(0f, halfHeight, halfExtents.z),
            Quaternion.identity, new Vector3(fullWidth, wallHeight, wallThickness));
        BuildWall("CourtWall_South", new Vector3(0f, halfHeight, -halfExtents.z),
            Quaternion.Euler(0f, 180f, 0f), new Vector3(fullWidth, wallHeight, wallThickness));
        BuildWall("CourtWall_East", new Vector3(halfExtents.x, halfHeight, 0f),
            Quaternion.Euler(0f, 90f, 0f), new Vector3(fullDepth, wallHeight, wallThickness));
        BuildWall("CourtWall_West", new Vector3(-halfExtents.x, halfHeight, 0f),
            Quaternion.Euler(0f, -90f, 0f), new Vector3(fullDepth, wallHeight, wallThickness));
    }

    private void BuildWall(string name, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(transform, false);
        wall.transform.localPosition = localPosition;
        wall.transform.localRotation = localRotation;
        wall.transform.localScale = localScale;
        ApplyMaterial(wall, wallMaterial);
    }

    private void ApplyMaterial(GameObject target, Material material)
    {
        if (material == null) return;
        target.GetComponent<Renderer>().sharedMaterial = material;
    }
}
