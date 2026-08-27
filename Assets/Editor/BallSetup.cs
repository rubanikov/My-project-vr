using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-click wiring of Court Clash's ball into the currently open scene:
// physics (bouncy PhysicMaterial, continuous collision so it can't tunnel
// through thin wall geometry at speed) and the fault-rule tracker.
//
// Grab interaction is deliberately NOT added here yet — see VRSceneSetup.cs's
// "Grab interaction" note for why, and PRD.md's status section for the
// current plan. Without it the ball can be pushed by hand colliders but not
// picked up/thrown; that's the next piece once grab interaction is wired.
public static class BallSetup
{
    private const string PhysicMaterialPath = "Assets/BallPhysicMaterial.physicMaterial";

    // Was 0.22f (~volleyball) for the original grab-and-throw mechanic;
    // halved to 0.11f for the racket mechanic (2026-08-26, user request:
    // "make the ball smaller (50% smaller for now)") — a smaller, faster
    // target reads better for racket-sport-style play. Rest position stays
    // within the 0.3-0.8m arm's-reach comfort zone (Meta's immersive design
    // guidelines) in front of where a player stands at the court's center.
    private const float BallDiameter = 0.11f;
    private static readonly Vector3 RestPosition = new Vector3(0f, 1.0f, 0.4f);

    [MenuItem("Court Clash/Setup Ball In Scene")]
    public static void SetupBall()
    {
        GameObject ball = GameObject.Find("Ball");
        if (ball == null)
        {
            ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Ball";
            Undo.RegisterCreatedObjectUndo(ball, "Create Ball");
        }

        ball.transform.position = RestPosition;
        ball.transform.localScale = Vector3.one * BallDiameter;

        PhysicsMaterial bounceMaterial = GetOrCreateBounceMaterial();
        SphereCollider collider = ball.GetComponent<SphereCollider>();
        if (collider != null)
        {
            collider.sharedMaterial = bounceMaterial;
        }

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = Undo.AddComponent<Rigidbody>(ball);
        }
        rb.mass = 0.3f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (ball.GetComponent<BallFaultTracker>() == null)
        {
            Undo.AddComponent<BallFaultTracker>(ball);
        }

        // Contact sounds: one spatialized AudioSource on the ball voices Hits,
        // Bounces, and ricochets (see BallContactSounds). The source's 3D
        // settings are configured by the component itself at runtime; here we
        // only add the pieces and assign the clips.
        if (ball.GetComponent<AudioSource>() == null)
        {
            AudioSource source = Undo.AddComponent<AudioSource>(ball);
            source.playOnAwake = false;
        }
        BallContactSounds sounds = ball.GetComponent<BallContactSounds>();
        if (sounds == null)
        {
            sounds = Undo.AddComponent<BallContactSounds>(ball);
        }
        AssignContactClips(sounds);

        Selection.activeGameObject = ball;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("Court Clash: Ball is set up with bouncy physics, BallFaultTracker, " +
            "and contact sounds. Grab interaction is not wired yet.");
    }

    private static void AssignContactClips(BallContactSounds sounds)
    {
        var serialized = new SerializedObject(sounds);
        AssignClip(serialized, "racketHitClip", "RacketHit");
        AssignClip(serialized, "surfaceBounceClip", "CourtBounce");
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignClip(SerializedObject serialized, string property, string clipName)
    {
        AudioClip clip = LoadClip(clipName);
        if (clip == null)
        {
            Debug.LogWarning($"Court Clash: no {clipName}.wav/.ogg under Assets/Audio — that " +
                "contact stays silent until the clip exists and Setup Ball In Scene reruns.");
            return;
        }
        serialized.FindProperty(property).objectReferenceValue = clip;
    }

    private static AudioClip LoadClip(string name)
    {
        foreach (string extension in new[] { "wav", "ogg" })
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/{name}.{extension}");
            if (clip != null) return clip;
        }
        return null;
    }

    private static PhysicsMaterial GetOrCreateBounceMaterial()
    {
        PhysicsMaterial existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysicMaterialPath);
        if (existing != null) return existing;

        var material = new PhysicsMaterial("BallPhysicMaterial")
        {
            bounciness = 0.85f,
            dynamicFriction = 0.3f,
            staticFriction = 0.3f,
            bounceCombine = PhysicsMaterialCombine.Maximum,
            frictionCombine = PhysicsMaterialCombine.Average,
        };
        AssetDatabase.CreateAsset(material, PhysicMaterialPath);
        return material;
    }
}
