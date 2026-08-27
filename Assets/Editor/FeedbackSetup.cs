using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-click wiring of the 2026-08-27 playtest-feedback batch into the open
// scene: the ball's escape watchdog, the referee voice (Announcer), and the
// unified game menu (which replaced the separate StartScreen and
// MatchPauseController — this setup also migrates a scene that still has
// them). Components self-find their scene references at runtime, so this
// only has to add them and assign the assets (voice clips, menu materials).
public static class FeedbackSetup
{
    private const string ShaderPath = "Assets/Shaders/MenuOverlay.shader";
    private const string PanelMaterialPath = "Assets/Materials/MenuPanel.mat";
    private const string DimMaterialPath = "Assets/Materials/MenuDim.mat";

    [MenuItem("Court Clash/Setup Feedback And Game Menu")]
    public static void Setup()
    {
        WireBallWatchdog();
        WireAnnouncer();
        WireGameMenu();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Court Clash: escape watchdog, announcer voice, and game menu are wired.");
    }

    private static void WireBallWatchdog()
    {
        GameObject ball = GameObject.Find("Ball");
        if (ball == null)
        {
            Debug.LogWarning("Court Clash: no Ball in the scene — run Setup Ball In Scene first.");
            return;
        }
        if (ball.GetComponent<BallEscapeWatchdog>() == null)
        {
            Undo.AddComponent<BallEscapeWatchdog>(ball);
        }
    }

    private static void WireAnnouncer()
    {
        GameObject announcer = GameObject.Find("Announcer");
        if (announcer == null)
        {
            announcer = new GameObject("Announcer");
            Undo.RegisterCreatedObjectUndo(announcer, "Create Announcer");
        }
        if (announcer.GetComponent<AudioSource>() == null)
        {
            AudioSource source = Undo.AddComponent<AudioSource>(announcer);
            source.playOnAwake = false;
        }
        MatchAnnouncer component = announcer.GetComponent<MatchAnnouncer>();
        if (component == null)
        {
            component = Undo.AddComponent<MatchAnnouncer>(announcer);
        }

        var serialized = new SerializedObject(component);
        AssignClip(serialized, "winPointClip", "VoiceWinPoint");
        AssignClip(serialized, "losePointClip", "VoiceLosePoint");
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireGameMenu()
    {
        RemoveLegacyScreens();

        GameObject menu = GameObject.Find("GameMenu");
        if (menu == null)
        {
            menu = new GameObject("GameMenu");
            Undo.RegisterCreatedObjectUndo(menu, "Create GameMenu");
        }
        GameMenu component = menu.GetComponent<GameMenu>();
        if (component == null)
        {
            component = Undo.AddComponent<GameMenu>(menu);
        }

        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
        {
            Debug.LogWarning($"Court Clash: {ShaderPath} is missing — the menu materials were not created.");
            return;
        }
        // Material assets (not runtime Shader.Find) so the shader variant
        // ships in builds. Queues: dim over the scene, panel over the dim;
        // the menu text sits above both on a runtime clone of the font
        // material (GameMenu.TextQueue).
        Material panelMaterial = LoadOrCreateMaterial(PanelMaterialPath, shader,
            new Color(0.11f, 0.118f, 0.133f, 1f), 4000);  // opaque charcoal #1C1E22
        Material dimMaterial = LoadOrCreateMaterial(DimMaterialPath, shader,
            new Color(0f, 0f, 0f, 0.75f), 3900);          // scene dims to ~25%

        var serialized = new SerializedObject(component);
        serialized.FindProperty("panelMaterial").objectReferenceValue = panelMaterial;
        serialized.FindProperty("dimMaterial").objectReferenceValue = dimMaterial;
        serialized.FindProperty("matchController").objectReferenceValue =
            Object.FindFirstObjectByType<MatchController>();
        serialized.FindProperty("courtBuilder").objectReferenceValue =
            Object.FindFirstObjectByType<CourtBuilder>();
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Scoreboard scoreboard = Object.FindFirstObjectByType<Scoreboard>();
        if (scoreboard != null)
        {
            var scoreboardSerialized = new SerializedObject(scoreboard);
            scoreboardSerialized.FindProperty("pauseController").objectReferenceValue = component;
            scoreboardSerialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // The StartScreen lived on its own GameObject; MatchPauseController sat
    // on the MatchController object. Their scripts are gone, so they linger
    // as missing-script components until this sweep removes them.
    private static void RemoveLegacyScreens()
    {
        GameObject startScreen = GameObject.Find("StartScreen");
        if (startScreen != null)
        {
            Undo.DestroyObjectImmediate(startScreen);
        }

        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                {
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                }
            }
        }
    }

    private static Material LoadOrCreateMaterial(string path, Shader shader, Color color, int queue)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.color = color;
        material.renderQueue = queue;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void AssignClip(SerializedObject serialized, string property, string clipName)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/{clipName}.wav");
        if (clip == null)
        {
            Debug.LogWarning($"Court Clash: Assets/Audio/{clipName}.wav is missing — " +
                "that announcement stays silent until it exists and setup reruns.");
            return;
        }
        serialized.FindProperty(property).objectReferenceValue = clip;
    }
}
