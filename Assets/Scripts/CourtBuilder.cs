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

    // Court geometry (2026-08-26, "fit the player part to the boundary"):
    // the PLAYER'S HALF is the entire Guardian play area — the net sits at
    // the front edge of the physical room (z = -HalfExtents.z) and the AI's
    // half mirrors it virtually beyond the wall. Court depth is therefore
    // twice the Guardian depth, centered on the net.
    public float NetZ { get; private set; }
    public float CenterX { get; private set; } // court x-center = play area x-center
    public float HalfDepthPerSide { get; private set; } // depth of each half (= full Guardian depth)
    public float CourtMinZ => NetZ - HalfDepthPerSide;  // far wall behind the AI
    public float CourtMaxZ => NetZ + HalfDepthPerSide;  // wall behind the player

    [Header("Fallback size (used when no Guardian boundary is available, e.g. Editor Play mode)")]
    [Tooltip("Was 1m — too small relative to wallHeight (2.5m), reading as a tall narrow " +
        "cone/tower instead of a room, and leaving too little margin for AI positioning near " +
        "the court edge (confirmed by testing: the AI's clamped edge position plus a small " +
        "offset landed outside the floor collider entirely). 2.5m matches a modest real room.")]
    [SerializeField] private float fallbackHalfWidth = 2.5f;
    [SerializeField] private float fallbackHalfDepth = 2.5f;

    [Header("Padel additions (2026-08-26 conversion — net splits the halves, tall ceiling contains lobs)")]
    [SerializeField] private float netHeight = 0.9f;
    [Tooltip("Well above the 2.5m walls so it's rarely hit; the band between wall top and " +
        "ceiling is closed with invisible collider panels so the ball can never leave.")]
    [SerializeField] private float ceilingHeight = 4f;
    [SerializeField] private Material netMaterial;
    [SerializeField] private Material ceilingMaterial;

    [Header("Shape")]
    [Tooltip("Was 8 individually-tilted panels arranged in a ring — each one leaning inward by " +
        "a random amount left visible gaps between neighbors (confirmed in playtesting). " +
        "Switched to a plain 4-wall rectangular prism: flush corners, no gaps. Varied bounce " +
        "geometry (angled panels, polygon inserts) can be added later as objects attached to " +
        "these flat walls, rather than baked into the walls themselves.")]
    [SerializeField] private float wallHeight = 4f; // raised to ceilingHeight 2026-08-26 (user: "move the walls up to the ceiling") — the invisible extension band auto-skips when the band is zero
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
        Vector3 playAreaCenter = Vector3.zero;
        bool usedRealBoundary = false;
        float deadline = Time.unscaledTime + boundaryWaitTimeoutSeconds;

        while (Time.unscaledTime < deadline)
        {
            if (TryGetPlayArea(out Vector3 realCenter, out Vector3 realHalfExtents))
            {
                playAreaCenter = realCenter;
                halfExtents = realHalfExtents;
                usedRealBoundary = true;
                break;
            }
            yield return null;
        }

        Debug.Log($"[CourtBuilder] Building court. realBoundary={usedRealBoundary}, " +
            $"playAreaCenter={playAreaCenter}, halfExtents={halfExtents}");

        // The court is CENTERED ON THE PLAY AREA, not on where the player
        // happened to stand at launch (2026-08-26 fix: the world origin is
        // the headset's recenter point, so a player starting near the corner
        // of a large room previously got a court planted around themselves
        // with most of their play area outside it). The net sits at the play
        // area's front edge; the AI's half mirrors the player's beyond it.
        HalfExtents = halfExtents;
        CenterX = playAreaCenter.x;
        NetZ = playAreaCenter.z - halfExtents.z;
        HalfDepthPerSide = halfExtents.z * 2f;
        var courtHalf = new Vector3(halfExtents.x, 0f, HalfDepthPerSide);

        BuildFloor(courtHalf, NetZ);
        BuildWalls(courtHalf, NetZ);
        BuildNet(courtHalf, NetZ);
        BuildCeiling(courtHalf, NetZ);
        BuildWallExtensions(courtHalf, NetZ);

        CourtBuilt?.Invoke(halfExtents);
    }

    private Vector3 GetFallbackHalfExtents() => new Vector3(fallbackHalfWidth, 0f, fallbackHalfDepth);

    // Primary source: the play area's corner GEOMETRY, which carries both its
    // size AND its position in tracking space (Meta docs: GetGeometry returns
    // floor-level points "in local tracking space"). GetDimensions alone —
    // the previous implementation — only gives size, silently centering the
    // court on the player instead of the room. Falls back to dimensions-only
    // (centered on origin) if geometry is unavailable. OVRBoundary reports
    // nothing until the OpenXR session is fully running, so the caller polls.
    private bool TryGetPlayArea(out Vector3 center, out Vector3 halfExtents)
    {
        const float minSaneDimension = 0.5f;
        center = Vector3.zero;
        halfExtents = default;
        if (OVRManager.boundary == null) return false;

        Vector3[] points = OVRManager.boundary.GetGeometry(OVRBoundary.BoundaryType.PlayArea);
        if (points != null && points.Length >= 3)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (Vector3 p in points)
            {
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.z);
                maxZ = Mathf.Max(maxZ, p.z);
            }
            float halfX = (maxX - minX) * 0.5f;
            float halfZ = (maxZ - minZ) * 0.5f;
            if (halfX * 2f > minSaneDimension && halfZ * 2f > minSaneDimension)
            {
                center = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
                halfExtents = new Vector3(halfX, 0f, halfZ);
                return true;
            }
        }

        Vector3 dimensions = OVRManager.boundary.GetDimensions(OVRBoundary.BoundaryType.PlayArea);
        if (dimensions.x > minSaneDimension && dimensions.z > minSaneDimension)
        {
            Debug.Log("[CourtBuilder] Play area geometry unavailable; using dimensions only (court centered on player).");
            halfExtents = new Vector3(dimensions.x * 0.5f, 0f, dimensions.z * 0.5f);
            return true;
        }
        return false;
    }

    private void BuildFloor(Vector3 courtHalf, float centerZ)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "CourtFloor";
        floor.transform.SetParent(transform, false);
        floor.transform.localScale = new Vector3(courtHalf.x * 2f, 0.1f, courtHalf.z * 2f);
        floor.transform.localPosition = new Vector3(CenterX, -0.05f, centerZ);
        ApplyMaterial(floor, floorMaterial);
    }

    // Four flat walls forming a rectangular room, flush at the corners.
    private void BuildWalls(Vector3 courtHalf, float centerZ)
    {
        float fullWidth = courtHalf.x * 2f;
        float fullDepth = courtHalf.z * 2f;
        float halfHeight = wallHeight * 0.5f;

        BuildWall("CourtWall_North", new Vector3(CenterX, halfHeight, centerZ + courtHalf.z),
            Quaternion.identity, new Vector3(fullWidth, wallHeight, wallThickness));
        BuildWall("CourtWall_South", new Vector3(CenterX, halfHeight, centerZ - courtHalf.z),
            Quaternion.Euler(0f, 180f, 0f), new Vector3(fullWidth, wallHeight, wallThickness));
        BuildWall("CourtWall_East", new Vector3(CenterX + courtHalf.x, halfHeight, centerZ),
            Quaternion.Euler(0f, 90f, 0f), new Vector3(fullDepth, wallHeight, wallThickness));
        BuildWall("CourtWall_West", new Vector3(CenterX - courtHalf.x, halfHeight, centerZ),
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

    // Mid-court net at z=0, spanning the full width. A solid obstacle, not a
    // rule surface — a ball that fails to clear it dies on the hitter's half
    // and the floor-bounce rules award the point.
    private void BuildNet(Vector3 courtHalf, float netZ)
    {
        GameObject net = GameObject.CreatePrimitive(PrimitiveType.Cube);
        net.name = "CourtNet";
        net.transform.SetParent(transform, false);
        net.transform.localScale = new Vector3(courtHalf.x * 2f, netHeight, 0.04f);
        net.transform.localPosition = new Vector3(CenterX, netHeight * 0.5f, netZ);
        ApplyMaterial(net, netMaterial != null ? netMaterial : wallMaterial);
    }

    // Visible lid well above the walls. Free ricochet surface — purely
    // containment (user decision 2026-08-26).
    private void BuildCeiling(Vector3 courtHalf, float centerZ)
    {
        GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "CourtCeiling";
        ceiling.transform.SetParent(transform, false);
        ceiling.transform.localScale = new Vector3(courtHalf.x * 2f, 0.1f, courtHalf.z * 2f);
        ceiling.transform.localPosition = new Vector3(CenterX, ceilingHeight + 0.05f, centerZ);
        ApplyMaterial(ceiling, ceilingMaterial != null ? ceilingMaterial : wallMaterial);
    }

    // Invisible collider panels closing the band between the wall tops and
    // the ceiling, so the ball physically cannot leave the court sideways.
    private void BuildWallExtensions(Vector3 courtHalf, float centerZ)
    {
        float bandHeight = ceilingHeight - wallHeight;
        if (bandHeight <= 0f) return;
        float bandCenterY = wallHeight + bandHeight * 0.5f;
        float fullWidth = courtHalf.x * 2f;
        float fullDepth = courtHalf.z * 2f;

        BuildInvisiblePanel("CourtWallExt_North", new Vector3(CenterX, bandCenterY, centerZ + courtHalf.z),
            new Vector3(fullWidth, bandHeight, wallThickness));
        BuildInvisiblePanel("CourtWallExt_South", new Vector3(CenterX, bandCenterY, centerZ - courtHalf.z),
            new Vector3(fullWidth, bandHeight, wallThickness));
        BuildInvisiblePanel("CourtWallExt_East", new Vector3(CenterX + courtHalf.x, bandCenterY, centerZ),
            new Vector3(wallThickness, bandHeight, fullDepth));
        BuildInvisiblePanel("CourtWallExt_West", new Vector3(CenterX - courtHalf.x, bandCenterY, centerZ),
            new Vector3(wallThickness, bandHeight, fullDepth));
    }

    private void BuildInvisiblePanel(string name, Vector3 localPosition, Vector3 size)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(transform, false);
        panel.transform.localPosition = localPosition;
        BoxCollider box = panel.AddComponent<BoxCollider>();
        box.size = size;
    }

    private void ApplyMaterial(GameObject target, Material material)
    {
        if (material == null) return;
        target.GetComponent<Renderer>().sharedMaterial = material;
    }
}
