using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TronRacketBuilder
{
    private const string MaterialsFolder = "Assets/Materials/Tron";
    private const string ModelsFolder = "Assets/Models/Tron";
    private const string PrefabsFolder = "Assets/Prefabs";

    [MenuItem("Court Clash/Build Tron Padel Racket")]
    public static void BuildTronRacket()
    {
        EnsureFoldersExist();

        // 1. Create Materials
        var frameMat = CreateOrGetMaterial("M_Tron_Frame", new Color(0.05f, 0.07f, 0.10f), 0.85f, 0.75f, Color.black, 0f);
        var faceMat = CreateOrGetMaterial("M_Tron_Face", new Color(0.08f, 0.10f, 0.14f), 0.60f, 0.50f, Color.black, 0f);
        var neonMat = CreateNeonMaterial("M_Tron_NeonLight", new Color(0f, 0.92f, 1f), 4.5f);
        var gripMat = CreateOrGetMaterial("M_Tron_Grip", new Color(0.04f, 0.05f, 0.07f), 0.10f, 0.25f, Color.black, 0f);
        var accentMat = CreateOrGetMaterial("M_Tron_Accents", new Color(0.25f, 0.30f, 0.38f), 0.95f, 0.88f, Color.black, 0f);

        // 2. Build Procedural 3D Meshes
        Mesh frameMesh = BuildHeadFrameMesh();
        Mesh neonRimMesh = BuildNeonEdgeMesh();
        Mesh faceMesh = BuildStrikingFaceMesh();
        Mesh bridgeMesh = BuildBridgeMesh();
        Mesh neonBridgeMesh = BuildBridgeNeonMesh();
        Mesh handleMesh = BuildHandleMesh();
        Mesh gripRingsMesh = BuildGripRingsNeonMesh();
        Mesh pommelMesh = BuildPommelMesh();
        Mesh holeTubesMesh = BuildHoleTubesMesh();
        Mesh circuitTracesMesh = BuildCircuitTracesMesh();

        // Save Meshes as Assets
        SaveMeshAsset(frameMesh, "Tron_HeadFrame.asset");
        SaveMeshAsset(neonRimMesh, "Tron_NeonRim.asset");
        SaveMeshAsset(faceMesh, "Tron_StrikingFaces.asset");
        SaveMeshAsset(bridgeMesh, "Tron_Bridge.asset");
        SaveMeshAsset(neonBridgeMesh, "Tron_BridgeNeon.asset");
        SaveMeshAsset(handleMesh, "Tron_Handle.asset");
        SaveMeshAsset(gripRingsMesh, "Tron_GripNeonRings.asset");
        SaveMeshAsset(pommelMesh, "Tron_Pommel.asset");
        SaveMeshAsset(holeTubesMesh, "Tron_HoleTubes.asset");
        SaveMeshAsset(circuitTracesMesh, "Tron_CircuitTraces.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 3. Assemble Prefab GameObject Hierarchy
        GameObject root = new GameObject("TronPadelRacket");

        // Rigidbody & PlayerRacket
        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        // BoxCollider matching padel head & sweet spot
        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.26f, 0f);
        col.size = new Vector3(0.26f, 0.30f, 0.045f);

        // Handle Collider
        var handleCol = root.AddComponent<CapsuleCollider>();
        handleCol.center = new Vector3(0f, -0.01f, 0f);
        handleCol.radius = 0.018f;
        handleCol.height = 0.16f;
        handleCol.direction = 1; // Y-axis

        // Visuals Container
        GameObject visuals = new GameObject("Visuals");
        visuals.transform.SetParent(root.transform, false);

        var neonRenderers = new List<Renderer>();

        // Part: Frame
        var frameGO = CreateVisualPart("HeadFrame", frameMesh, frameMat, visuals.transform);

        // Part: Neon Rim (Outer glowing edge)
        var neonRimGO = CreateVisualPart("NeonEdgeLight", neonRimMesh, neonMat, visuals.transform);
        neonRenderers.Add(neonRimGO.GetComponent<Renderer>());

        // Part: Striking Faces
        var faceGO = CreateVisualPart("StrikingFaces", faceMesh, faceMat, visuals.transform);

        // Part: Hole Tubes
        var holesGO = CreateVisualPart("HoleTubes", holeTubesMesh, accentMat, visuals.transform);

        // Part: Circuit Traces (Neon face insets)
        var circuitsGO = CreateVisualPart("FaceCircuits", circuitTracesMesh, neonMat, visuals.transform);
        neonRenderers.Add(circuitsGO.GetComponent<Renderer>());

        // Part: Bridge / Throat
        var bridgeGO = CreateVisualPart("BridgeThroat", bridgeMesh, frameMat, visuals.transform);

        // Part: Bridge Neon
        var bridgeNeonGO = CreateVisualPart("BridgeNeon", neonBridgeMesh, neonMat, visuals.transform);
        neonRenderers.Add(bridgeNeonGO.GetComponent<Renderer>());

        // Part: Handle & Grip
        var handleGO = CreateVisualPart("HandleGrip", handleMesh, gripMat, visuals.transform);

        // Part: Grip Neon Accent Rings
        var gripRingsGO = CreateVisualPart("GripNeonRings", gripRingsMesh, neonMat, visuals.transform);
        neonRenderers.Add(gripRingsGO.GetComponent<Renderer>());

        // Part: Pommel / End Cap
        var pommelGO = CreateVisualPart("PommelEndCap", pommelMesh, accentMat, visuals.transform);

        // Dynamic Rim Point Light for realistic illumination
        GameObject lightObj = new GameObject("NeonGlowLight");
        lightObj.transform.SetParent(visuals.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 0.26f, 0f);
        var rimLight = lightObj.AddComponent<Light>();
        rimLight.type = LightType.Point;
        rimLight.color = new Color(0f, 0.92f, 1f);
        rimLight.range = 0.8f;
        rimLight.intensity = 0.8f;

        // Trail Renderer for swing light ribbon
        GameObject trailObj = new GameObject("SwingTrail");
        trailObj.transform.SetParent(visuals.transform, false);
        trailObj.transform.localPosition = new Vector3(0f, 0.40f, 0f); // Top edge of head
        var trail = trailObj.AddComponent<TrailRenderer>();
        trail.time = 0.12f;
        trail.startWidth = 0.04f;
        trail.endWidth = 0.001f;
        trail.material = neonMat;
        trail.startColor = new Color(0f, 0.92f, 1f, 0.7f);
        trail.endColor = new Color(0f, 0.92f, 1f, 0f);
        trail.minVertexDistance = 0.01f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;

        // TronRacketVisuals component
        var tronVisuals = root.AddComponent<TronRacketVisuals>();
        var fieldTrail = typeof(TronRacketVisuals).GetField("swingTrail", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var fieldLight = typeof(TronRacketVisuals).GetField("rimPointLight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var fieldRenderers = typeof(TronRacketVisuals).GetField("neonRenderers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        fieldTrail?.SetValue(tronVisuals, trail);
        fieldLight?.SetValue(tronVisuals, rimLight);
        fieldRenderers?.SetValue(tronVisuals, neonRenderers);

        // Save Prefab
        string prefabPath = Path.Combine(PrefabsFolder, "TronPadelRacket.prefab");
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Debug.Log($"Created Tron Padel Racket prefab at: {prefabPath}");

        // Update Scene's "Racket" if it exists
        UpdateSceneRacket(root);

        Object.DestroyImmediate(root);
    }

    private static void UpdateSceneRacket(GameObject sourceTemplate)
    {
        var activeRacket = GameObject.Find("Racket");
        if (activeRacket == null) return;

        Undo.RegisterFullObjectHierarchyUndo(activeRacket, "Upgrade Racket to Tron Padel Racket");

        // Remove old primitive visual children (Handle, PaddleHead)
        for (int i = activeRacket.transform.childCount - 1; i >= 0; i--)
        {
            var child = activeRacket.transform.GetChild(i).gameObject;
            if (child.name == "Handle" || child.name == "PaddleHead" || child.name == "Visuals")
            {
                Undo.DestroyObjectImmediate(child);
            }
        }

        // Copy visuals container into active racket
        var sourceVisuals = sourceTemplate.transform.Find("Visuals");
        if (sourceVisuals != null)
        {
            var newVisuals = Object.Instantiate(sourceVisuals.gameObject, activeRacket.transform);
            newVisuals.name = "Visuals";
            newVisuals.transform.localPosition = Vector3.zero;
            newVisuals.transform.localRotation = Quaternion.identity;
            newVisuals.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(newVisuals, "Instantiate Tron Visuals");

            // Update BoxCollider
            var box = activeRacket.GetComponent<BoxCollider>();
            if (box != null)
            {
                box.center = new Vector3(0f, 0.26f, 0f);
                box.size = new Vector3(0.26f, 0.30f, 0.045f);
            }

            // Ensure TronRacketVisuals is on active racket
            var tronVisuals = activeRacket.GetComponent<TronRacketVisuals>();
            if (tronVisuals == null)
            {
                tronVisuals = Undo.AddComponent<TronRacketVisuals>(activeRacket);
            }

            // Wire up TronRacketVisuals
            var neonRenderers = new List<Renderer>();
            foreach (var r in newVisuals.GetComponentsInChildren<Renderer>())
            {
                if (r.sharedMaterial != null && r.sharedMaterial.name.Contains("Neon"))
                {
                    neonRenderers.Add(r);
                }
            }

            var trail = newVisuals.GetComponentInChildren<TrailRenderer>();
            var rimLight = newVisuals.GetComponentInChildren<Light>();

            var fieldTrail = typeof(TronRacketVisuals).GetField("swingTrail", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fieldLight = typeof(TronRacketVisuals).GetField("rimPointLight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fieldRenderers = typeof(TronRacketVisuals).GetField("neonRenderers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            fieldTrail?.SetValue(tronVisuals, trail);
            fieldLight?.SetValue(tronVisuals, rimLight);
            fieldRenderers?.SetValue(tronVisuals, neonRenderers);

            EditorUtility.SetDirty(activeRacket);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Successfully upgraded Scene Racket to futuristic Tron Padel Racket!");
        }
    }

    private static GameObject CreateVisualPart(string name, Mesh mesh, Material mat, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        return go;
    }

    private static void EnsureFoldersExist()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            AssetDatabase.CreateFolder("Assets/Materials", "Tron");

        if (!AssetDatabase.IsValidFolder("Assets/Models"))
            AssetDatabase.CreateFolder("Assets", "Models");
        if (!AssetDatabase.IsValidFolder(ModelsFolder))
            AssetDatabase.CreateFolder("Assets/Models", "Tron");

        if (!AssetDatabase.IsValidFolder(PrefabsFolder))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
    }

    private static Material CreateOrGetMaterial(string name, Color color, float metallic, float smoothness, Color emission, float emissionMultiplier)
    {
        string path = $"{MaterialsFolder}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);

        if (emissionMultiplier > 0f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission * emissionMultiplier);
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material CreateNeonMaterial(string name, Color neonColor, float intensity)
    {
        string path = $"{MaterialsFolder}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetColor("_BaseColor", neonColor);
        mat.SetFloat("_Metallic", 0.1f);
        mat.SetFloat("_Smoothness", 0.95f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", neonColor * intensity);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void SaveMeshAsset(Mesh mesh, string filename)
    {
        string path = $"{ModelsFolder}/{filename}";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, path);
        }
    }

    // -------------------------------------------------------------
    // PROCEDURAL 3D MESH GENERATION FOR TRON PADEL RACKET
    // -------------------------------------------------------------

    // Head profile curve definition (Padel shape: teardrop/diamond rounded)
    private static Vector2 GetHeadOutlinePoint(float t)
    {
        // t in [0, 1) around circumference
        float angle = t * Mathf.PI * 2f;
        // Padel head shape: width ~0.25m (half-width ~0.125m), height ~0.28m, centered at y = 0.26m
        float sinA = Mathf.Sin(angle);
        float cosA = Mathf.Cos(angle);

        // Teardrop / rounded hexagon shaping
        float yRel = -cosA; // from -1 (bottom throat) to +1 (top)
        float widthFactor = Mathf.Lerp(0.65f, 1.0f, Mathf.Sin(Mathf.Clamp01((yRel + 1f) * 0.5f) * Mathf.PI));
        if (yRel < -0.6f)
        {
            // Taper inward into throat
            float taperT = (yRel - (-1f)) / 0.4f;
            widthFactor = Mathf.Lerp(0.35f, widthFactor, taperT);
        }

        float rx = 0.125f * widthFactor;
        float ry = 0.140f;

        float x = sinA * rx;
        float y = 0.26f + yRel * ry;

        return new Vector2(x, y);
    }

    // Outer Head Frame (Carbon Fiber aerodynamic chassis)
    private static Mesh BuildHeadFrameMesh()
    {
        Mesh mesh = new Mesh { name = "Tron_HeadFrame" };
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        int segments = 48;
        float halfThickness = 0.019f; // 38mm total thickness
        float frameWidth = 0.014f;    // Frame rim width

        // Build a chamfered cross section around the head loop
        // Ring 0: Outer-most edge (z=0, x/y = outer)
        // Ring 1: Outer-Front chamfer (z = halfThickness, x/y = outer - 0.003)
        // Ring 2: Inner-Front bevel (z = halfThickness, x/y = inner)
        // Ring 3: Inner Face ledge (z = halfThickness - 0.003, x/y = inner)
        // Ring 4: Inner-Back bevel (z = -halfThickness, x/y = inner)
        // Ring 5: Outer-Back chamfer (z = -halfThickness, x/y = outer - 0.003)

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)(i % segments) / segments;
            Vector2 p = GetHeadOutlinePoint(t);
            Vector2 pNext = GetHeadOutlinePoint(((float)((i + 1) % segments)) / segments);
            Vector2 pPrev = GetHeadOutlinePoint(((float)((i - 1 + segments) % segments)) / segments);

            Vector2 tangent = (pNext - pPrev).normalized;
            Vector2 outwardNormal = new Vector2(tangent.y, -tangent.x);

            Vector2 outerP = p + outwardNormal * 0.008f;
            Vector2 innerP = p - outwardNormal * (frameWidth);

            // 6 cross-section vertices per segment
            vertices.Add(new Vector3(outerP.x, outerP.y, 0f)); // 0: Outer ridge
            vertices.Add(new Vector3(outerP.x - outwardNormal.x * 0.003f, outerP.y - outwardNormal.y * 0.003f, halfThickness)); // 1: Front Outer Chamfer
            vertices.Add(new Vector3(innerP.x, innerP.y, halfThickness)); // 2: Front Inner Bevel
            vertices.Add(new Vector3(innerP.x, innerP.y, halfThickness - 0.004f)); // 3: Front Inset Ledge
            vertices.Add(new Vector3(innerP.x, innerP.y, -halfThickness)); // 4: Back Inner Bevel
            vertices.Add(new Vector3(outerP.x - outwardNormal.x * 0.003f, outerP.y - outwardNormal.y * 0.003f, -halfThickness)); // 5: Back Outer Chamfer

            float u = (float)i / segments;
            for (int k = 0; k < 6; k++)
            {
                uvs.Add(new Vector2(u, (float)k / 5f));
            }

            if (i < segments)
            {
                int cur = i * 6;
                int next = (i + 1) * 6;

                // Quads between rings:
                // 0 -> 1 (Outer to Front Chamfer)
                AddQuad(triangles, cur + 0, next + 0, next + 1, cur + 1);
                // 1 -> 2 (Front Chamfer to Front Inner)
                AddQuad(triangles, cur + 1, next + 1, next + 2, cur + 2);
                // 2 -> 3 (Front Inner to Inset Ledge)
                AddQuad(triangles, cur + 2, next + 2, next + 3, cur + 3);
                // 4 -> 5 (Back Inner to Back Chamfer)
                AddQuad(triangles, cur + 4, next + 4, next + 5, cur + 5);
                // 5 -> 0 (Back Chamfer to Outer)
                AddQuad(triangles, cur + 5, next + 5, next + 0, cur + 0);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Neon Edge Light Ribbon (Continuous Glowing Tron Perimeter Rail)
    private static Mesh BuildNeonEdgeMesh()
    {
        Mesh mesh = new Mesh { name = "Tron_NeonRim" };
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        int segments = 48;
        float ribbonHalfWidth = 0.0035f; // 7mm wide neon conduit strip
        float lightOffset = 0.0078f;     // Embedded right into outer channel

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)(i % segments) / segments;
            Vector2 p = GetHeadOutlinePoint(t);
            Vector2 pNext = GetHeadOutlinePoint(((float)((i + 1) % segments)) / segments);
            Vector2 pPrev = GetHeadOutlinePoint(((float)((i - 1 + segments) % segments)) / segments);

            Vector2 tangent = (pNext - pPrev).normalized;
            Vector2 outwardNormal = new Vector2(tangent.y, -tangent.x);

            Vector2 outerP = p + outwardNormal * lightOffset;

            // 4 vertices around the glowing ribbon pipe
            vertices.Add(new Vector3(outerP.x, outerP.y, -ribbonHalfWidth));
            vertices.Add(new Vector3(outerP.x + outwardNormal.x * 0.002f, outerP.y + outwardNormal.y * 0.002f, -ribbonHalfWidth * 0.5f));
            vertices.Add(new Vector3(outerP.x + outwardNormal.x * 0.002f, outerP.y + outwardNormal.y * 0.002f, ribbonHalfWidth * 0.5f));
            vertices.Add(new Vector3(outerP.x, outerP.y, ribbonHalfWidth));

            float u = (float)i / segments;
            uvs.Add(new Vector2(u, 0f));
            uvs.Add(new Vector2(u, 0.33f));
            uvs.Add(new Vector2(u, 0.66f));
            uvs.Add(new Vector2(u, 1f));

            if (i < segments)
            {
                int cur = i * 4;
                int next = (i + 1) * 4;
                AddQuad(triangles, cur + 0, next + 0, next + 1, cur + 1);
                AddQuad(triangles, cur + 1, next + 1, next + 2, cur + 2);
                AddQuad(triangles, cur + 2, next + 2, next + 3, cur + 3);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Striking Face Mesh (Front and Back inset plates)
    private static Mesh BuildStrikingFaceMesh()
    {
        Mesh mesh = new Mesh { name = "Tron_StrikingFaces" };
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        int segments = 48;
        float zOffset = 0.015f; // Slightly inset from outer frame (0.019m)

        // Center vertex for fan triangulation
        int frontCenterIdx = vertices.Count;
        vertices.Add(new Vector3(0f, 0.26f, zOffset));
        normals.Add(Vector3.forward);
        uvs.Add(new Vector2(0.5f, 0.5f));

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)(i % segments) / segments;
            Vector2 p = GetHeadOutlinePoint(t);
            Vector2 pNext = GetHeadOutlinePoint(((float)((i + 1) % segments)) / segments);
            Vector2 pPrev = GetHeadOutlinePoint(((float)((i - 1 + segments) % segments)) / segments);
            Vector2 tangent = (pNext - pPrev).normalized;
            Vector2 outwardNormal = new Vector2(tangent.y, -tangent.x);

            Vector2 innerP = p - outwardNormal * 0.013f; // Inside frame edge
            vertices.Add(new Vector3(innerP.x, innerP.y, zOffset));
            normals.Add(Vector3.forward);
            uvs.Add(new Vector2(innerP.x * 4f + 0.5f, (innerP.y - 0.26f) * 4f + 0.5f));

            if (i < segments)
            {
                triangles.Add(frontCenterIdx);
                triangles.Add(frontCenterIdx + 1 + i);
                triangles.Add(frontCenterIdx + 1 + (i + 1));
            }
        }

        // Back Face (-Z)
        int backCenterIdx = vertices.Count;
        vertices.Add(new Vector3(0f, 0.26f, -zOffset));
        normals.Add(Vector3.back);
        uvs.Add(new Vector2(0.5f, 0.5f));

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)(i % segments) / segments;
            Vector2 p = GetHeadOutlinePoint(t);
            Vector2 pNext = GetHeadOutlinePoint(((float)((i + 1) % segments)) / segments);
            Vector2 pPrev = GetHeadOutlinePoint(((float)((i - 1 + segments) % segments)) / segments);
            Vector2 tangent = (pNext - pPrev).normalized;
            Vector2 outwardNormal = new Vector2(tangent.y, -tangent.x);

            Vector2 innerP = p - outwardNormal * 0.013f;
            vertices.Add(new Vector3(innerP.x, innerP.y, -zOffset));
            normals.Add(Vector3.back);
            uvs.Add(new Vector2(innerP.x * 4f + 0.5f, (innerP.y - 0.26f) * 4f + 0.5f));

            if (i < segments)
            {
                triangles.Add(backCenterIdx);
                triangles.Add(backCenterIdx + 1 + (i + 1));
                triangles.Add(backCenterIdx + 1 + i);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Authentic Padel Hole Pattern positions
    private static List<Vector2> GetPadelHolePositions()
    {
        var holes = new List<Vector2>();
        // Center around (0, 0.26)
        // Array of concentric rings / cybernetic pattern
        float[] ringRadii = { 0.028f, 0.052f, 0.076f };
        int[] holeCounts = { 6, 12, 16 };

        for (int r = 0; r < ringRadii.Length; r++)
        {
            float radius = ringRadii[r];
            int count = holeCounts[r];
            for (int i = 0; i < count; i++)
            {
                float angle = (i * Mathf.PI * 2f / count) + (r * 0.25f);
                float hx = Mathf.Cos(angle) * radius * 0.88f; // slightly oval padel sweet spot
                float hy = 0.26f + Mathf.Sin(angle) * radius;
                holes.Add(new Vector2(hx, hy));
            }
        }

        // Add 4 diagonal outer accent holes
        holes.Add(new Vector2(-0.065f, 0.35f));
        holes.Add(new Vector2(0.065f, 0.35f));
        holes.Add(new Vector2(-0.065f, 0.18f));
        holes.Add(new Vector2(0.065f, 0.18f));

        return holes;
    }

    // 3D Hole Inset Tubes connecting front to back face
    private static Mesh BuildHoleTubesMesh()
    {
        Mesh mesh = new Mesh { name = "Tron_HoleTubes" };
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        var holes = GetPadelHolePositions();
        float holeRadius = 0.0055f; // 11mm padel hole diameter
        float zDepth = 0.0152f;
        int circleSegs = 10;

        foreach (var pos in holes)
        {
            int baseIdx = vertices.Count;
            for (int i = 0; i <= circleSegs; i++)
            {
                float angle = (float)(i % circleSegs) / circleSegs * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                float vx = pos.x + cos * holeRadius;
                float vy = pos.y + sin * holeRadius;

                // Front bevel ring
                vertices.Add(new Vector3(vx, vy, zDepth));
                // Back bevel ring
                vertices.Add(new Vector3(vx, vy, -zDepth));

                float u = (float)i / circleSegs;
                uvs.Add(new Vector2(u, 0f));
                uvs.Add(new Vector2(u, 1f));

                if (i < circleSegs)
                {
                    int c = baseIdx + i * 2;
                    int n = baseIdx + (i + 1) * 2;
                    // Inward cylinder quad (pointing into hole)
                    AddQuad(triangles, c + 0, c + 1, n + 1, n + 0);
                }
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Glowing Tron Circuit Traces on the striking face
    private static Mesh BuildCircuitTracesMesh()
    {
        Mesh mesh = new Mesh { name = "Tron_CircuitTraces" };
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        var holes = GetPadelHolePositions();
        float traceWidth = 0.0018f; // 1.8mm glowing circuit lines
        float zFront = 0.0152f;
        float zBack = -0.0152f;

        // Trace 1: Glowing rings around the inner 6 holes
        for (int h = 0; h < 6; h++)
        {
            Vector2 center = holes[h];
            float ringR = 0.0072f;
            int ringSegs = 12;

            for (int face = 0; face < 2; face++)
            {
                float z = face == 0 ? zFront : zBack;
                int baseIdx = vertices.Count;

                for (int i = 0; i <= ringSegs; i++)
                {
                    float angle = (float)(i % ringSegs) / ringSegs * Mathf.PI * 2f;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);

                    Vector3 pInner = new Vector3(center.x + cos * (ringR - traceWidth), center.y + sin * (ringR - traceWidth), z);
                    Vector3 pOuter = new Vector3(center.x + cos * (ringR + traceWidth), center.y + sin * (ringR + traceWidth), z);

                    vertices.Add(pInner);
                    vertices.Add(pOuter);

                    float u = (float)i / ringSegs;
                    uvs.Add(new Vector2(u, 0f));
                    uvs.Add(new Vector2(u, 1f));

                    if (i < ringSegs)
                    {
                        int c = baseIdx + i * 2;
                        int n = baseIdx + (i + 1) * 2;
                        if (face == 0)
                            AddQuad(triangles, c + 0, c + 1, n + 1, n + 0);
                        else
                            AddQuad(triangles, c + 0, n + 0, n + 1, c + 1);
                    }
                }
            }
        }

        // Trace 2: Radial cyber lines from center out to corner holes
        Vector2[] radialLines = new Vector2[]
        {
            new Vector2(0f, 0.16f), new Vector2(0f, 0.22f), // Bottom strut line
            new Vector2(0f, 0.30f), new Vector2(0f, 0.37f), // Top crest line
            new Vector2(-0.02f, 0.26f), new Vector2(-0.095f, 0.26f), // Left horizontal line
            new Vector2(0.02f, 0.26f), new Vector2(0.095f, 0.26f), // Right horizontal line
            new Vector2(-0.04f, 0.31f), new Vector2(-0.08f, 0.34f), // Upper-left angle line
            new Vector2(0.04f, 0.31f), new Vector2(0.08f, 0.34f), // Upper-right angle line
            new Vector2(-0.04f, 0.21f), new Vector2(-0.08f, 0.18f), // Lower-left angle line
            new Vector2(0.04f, 0.21f), new Vector2(0.08f, 0.18f), // Lower-right angle line
        };

        for (int l = 0; l < radialLines.Length; l += 2)
        {
            Vector2 p1 = radialLines[l];
            Vector2 p2 = radialLines[l + 1];
            Vector2 dir = (p2 - p1).normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x) * traceWidth;

            for (int face = 0; face < 2; face++)
            {
                float z = face == 0 ? zFront : zBack;
                int baseIdx = vertices.Count;

                vertices.Add(new Vector3(p1.x - normal.x, p1.y - normal.y, z));
                vertices.Add(new Vector3(p1.x + normal.x, p1.y + normal.y, z));
                vertices.Add(new Vector3(p2.x + normal.x, p2.y + normal.y, z));
                vertices.Add(new Vector3(p2.x - normal.x, p2.y - normal.y, z));

                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(0f, 1f));

                if (face == 0)
                    AddQuad(triangles, baseIdx + 0, baseIdx + 3, baseIdx + 2, baseIdx + 1);
                else
                    AddQuad(triangles, baseIdx + 0, baseIdx + 1, baseIdx + 2, baseIdx + 3);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Aerodynamic Dual-Strut Bridge / Throat
    private static Mesh BuildBridgeMesh()
    {
        Mesh mesh = new Mesh { name = "Tron_Bridge" };
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        // Left Strut and Right Strut with center aerodynamic triangular airflow duct
        BuildStrut(vertices, uvs, triangles, -1f);
        BuildStrut(vertices, uvs, triangles, 1f);

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void BuildStrut(List<Vector3> verts, List<Vector2> uvs, List<int> tris, float side)
    {
        // Spans from handle top y = 0.055m to head bottom y = 0.135m
        int slices = 8;
        float zThick = 0.016f;

        int baseStart = verts.Count;

        for (int i = 0; i <= slices; i++)
        {
            float t = (float)i / slices;
            float y = Mathf.Lerp(0.055f, 0.135f, t);

            // Curve outwards from handle radius (0.016m) to head shoulder (0.048m)
            float xInner = side * Mathf.Lerp(0.005f, 0.024f, t * t);
            float xOuter = side * Mathf.Lerp(0.018f, 0.052f, Mathf.Sqrt(t));

            // 4 box vertices around strut cross section
            verts.Add(new Vector3(xInner, y, zThick));
            verts.Add(new Vector3(xOuter, y, zThick));
            verts.Add(new Vector3(xOuter, y, -zThick));
            verts.Add(new Vector3(xInner, y, -zThick));

            uvs.Add(new Vector2(0f, t));
            uvs.Add(new Vector2(0.33f, t));
            uvs.Add(new Vector2(0.66f, t));
            uvs.Add(new Vector2(1f, t));

            if (i < slices)
            {
                int c = baseStart + i * 4;
                int n = baseStart + (i + 1) * 4;

                // Front quad
                AddQuad(tris, c + 0, n + 0, n + 1, c + 1);
                // Outer quad
                AddQuad(tris, c + 1, n + 1, n + 2, c + 2);
                // Back quad
                AddQuad(tris, c + 2, n + 2, n + 3, c + 3);
                // Inner quad
                AddQuad(tris, c + 3, n + 3, n + 0, c + 0);
            }
        }
    }

    // Bridge Neon Light Conduits running along the struts
    private static Mesh BuildBridgeNeonMesh()
    {
        Mesh mesh = new Mesh { name = "Tron_BridgeNeon" };
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        BuildBridgeNeonStrut(vertices, uvs, triangles, -1f);
        BuildBridgeNeonStrut(vertices, uvs, triangles, 1f);

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void BuildBridgeNeonStrut(List<Vector3> verts, List<Vector2> uvs, List<int> tris, float side)
    {
        int slices = 8;
        float ribbonHalfW = 0.0025f;
        int baseStart = verts.Count;

        for (int i = 0; i <= slices; i++)
        {
            float t = (float)i / slices;
            float y = Mathf.Lerp(0.056f, 0.134f, t);
            float xOuter = side * Mathf.Lerp(0.0185f, 0.0525f, Mathf.Sqrt(t));

            verts.Add(new Vector3(xOuter, y, -ribbonHalfW));
            verts.Add(new Vector3(xOuter + side * 0.0015f, y, 0f));
            verts.Add(new Vector3(xOuter, y, ribbonHalfW));

            uvs.Add(new Vector2(0f, t));
            uvs.Add(new Vector2(0.5f, t));
            uvs.Add(new Vector2(1f, t));

            if (i < slices)
            {
                int c = baseStart + i * 3;
                int n = baseStart + (i + 1) * 3;
                AddQuad(tris, c + 0, n + 0, n + 1, c + 1);
                AddQuad(tris, c + 1, n + 1, n + 2, c + 2);
            }
        }
    }

    // Handle & Grip (Octagonal futuristic grip with ribbed wrapping)
    private static Mesh BuildHandleMesh()
    {
        Mesh mesh = new Mesh { name = "Tron_Handle" };
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        int sides = 8; // Octagonal ergonomic handle
        int rings = 24; // Winding rib segments
        float yBottom = -0.075f;
        float yTop = 0.055f;
        float baseRadius = 0.0155f;

        for (int r = 0; r <= rings; r++)
        {
            float t = (float)r / rings;
            float y = Mathf.Lerp(yBottom, yTop, t);

            // Subtle ergonomic taper and ribbed wrap bumps
            float ribBump = Mathf.Sin(t * Mathf.PI * 18f) * 0.0008f;
            float taper = Mathf.Lerp(1.05f, 0.95f, t);
            float currentRadius = (baseRadius + ribBump) * taper;

            for (int s = 0; s <= sides; s++)
            {
                float angle = (float)(s % sides) / sides * Mathf.PI * 2f;
                // Oval/Octagonal grip profile
                float cos = Mathf.Cos(angle) * 0.92f;
                float sin = Mathf.Sin(angle) * 1.08f;

                vertices.Add(new Vector3(cos * currentRadius, y, sin * currentRadius));
                uvs.Add(new Vector2((float)s / sides, t * 6f));

                if (r < rings && s < sides)
                {
                    int c = r * (sides + 1) + s;
                    int n = (r + 1) * (sides + 1) + s;
                    AddQuad(triangles, c, c + 1, n + 1, n);
                }
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Grip Neon Accent Rings (Illuminated divider rings along the handle)
    private static Mesh BuildGripRingsNeonMesh()
    {
        Mesh mesh = new Mesh { name = "Tron_GripNeonRings" };
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        float[] ringYPositions = new float[] { -0.055f, -0.015f, 0.025f, 0.052f };
        int sides = 16;
        float ringH = 0.0022f;

        foreach (float yPos in ringYPositions)
        {
            float radius = 0.0162f;
            int baseIdx = vertices.Count;

            for (int i = 0; i <= sides; i++)
            {
                float angle = (float)(i % sides) / sides * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle) * 0.94f;
                float sin = Mathf.Sin(angle) * 1.06f;

                vertices.Add(new Vector3(cos * radius, yPos - ringH, sin * radius));
                vertices.Add(new Vector3(cos * (radius + 0.001f), yPos, sin * (radius + 0.001f)));
                vertices.Add(new Vector3(cos * radius, yPos + ringH, sin * radius));

                float u = (float)i / sides;
                uvs.Add(new Vector2(u, 0f));
                uvs.Add(new Vector2(u, 0.5f));
                uvs.Add(new Vector2(u, 1f));

                if (i < sides)
                {
                    int c = baseIdx + i * 3;
                    int n = baseIdx + (i + 1) * 3;
                    AddQuad(triangles, c + 0, c + 1, n + 1, n + 0);
                    AddQuad(triangles, c + 1, c + 2, n + 2, n + 1);
                }
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Pommel / End Cap (Futuristic flared base with core ring and lanyard loop)
    private static Mesh BuildPommelMesh()
    {
        Mesh mesh = new Mesh { name = "Tron_Pommel" };
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        int sides = 16;
        float yTop = -0.075f;
        float yMid = -0.088f;
        float yBottom = -0.095f;

        float rTop = 0.016f;
        float rFlare = 0.021f;
        float rCore = 0.012f;

        int baseIdx = vertices.Count;

        // Flare ring 0: Top
        // Flare ring 1: Widest bevel
        // Flare ring 2: Bottom chamfer
        // Flare ring 3: Inset bottom face

        for (int i = 0; i <= sides; i++)
        {
            float angle = (float)(i % sides) / sides * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            vertices.Add(new Vector3(cos * rTop, yTop, sin * rTop));
            vertices.Add(new Vector3(cos * rFlare, yMid, sin * rFlare));
            vertices.Add(new Vector3(cos * rFlare * 0.95f, yBottom, sin * rFlare * 0.95f));
            vertices.Add(new Vector3(cos * rCore, yBottom - 0.003f, sin * rCore));

            float u = (float)i / sides;
            uvs.Add(new Vector2(u, 0f));
            uvs.Add(new Vector2(u, 0.33f));
            uvs.Add(new Vector2(u, 0.66f));
            uvs.Add(new Vector2(u, 1f));

            if (i < sides)
            {
                int c = baseIdx + i * 4;
                int n = baseIdx + (i + 1) * 4;
                AddQuad(triangles, c + 0, c + 1, n + 1, n + 0);
                AddQuad(triangles, c + 1, c + 2, n + 2, n + 1);
                AddQuad(triangles, c + 2, c + 3, n + 3, n + 2);
            }
        }

        // Bottom cap center fan
        int centerIdx = vertices.Count;
        vertices.Add(new Vector3(0f, yBottom - 0.003f, 0f));
        uvs.Add(new Vector2(0.5f, 0.5f));

        for (int i = 0; i < sides; i++)
        {
            int c = baseIdx + i * 4 + 3;
            int n = baseIdx + (i + 1) * 4 + 3;
            triangles.Add(centerIdx);
            triangles.Add(n);
            triangles.Add(c);
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddQuad(List<int> triangles, int i0, int i1, int i2, int i3)
    {
        triangles.Add(i0);
        triangles.Add(i1);
        triangles.Add(i2);
        triangles.Add(i0);
        triangles.Add(i2);
        triangles.Add(i3);
    }
}
