using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-click wiring of the 2026-08-27 playtest-feedback batch into the open
// scene: the ball's escape watchdog, the referee voice (Announcer), and the
// controls/start screen. Companion to BallSetup's contact-sound wiring —
// components self-find their scene references at runtime, so this only has
// to add them and assign the assets (voice clips, panel material).
public static class FeedbackSetup
{
    [MenuItem("Court Clash/Setup Feedback And Start Screen")]
    public static void Setup()
    {
        WireBallWatchdog();
        WireAnnouncer();
        WireStartScreen();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Court Clash: escape watchdog, announcer voice, and start screen are wired.");
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

    private static void WireStartScreen()
    {
        GameObject screen = GameObject.Find("StartScreen");
        if (screen == null)
        {
            screen = new GameObject("StartScreen");
            Undo.RegisterCreatedObjectUndo(screen, "Create StartScreen");
        }
        StartScreen component = screen.GetComponent<StartScreen>();
        if (component == null)
        {
            component = Undo.AddComponent<StartScreen>(screen);
        }

        // Reuse the scoreboard's transparent panel material so both boards
        // share one shipped shader variant.
        Scoreboard scoreboard = Object.FindFirstObjectByType<Scoreboard>();
        if (scoreboard != null)
        {
            var scoreboardSerialized = new SerializedObject(scoreboard);
            Material panelMaterial = scoreboardSerialized
                .FindProperty("panelMaterial").objectReferenceValue as Material;
            if (panelMaterial != null)
            {
                var serialized = new SerializedObject(component);
                serialized.FindProperty("panelMaterial").objectReferenceValue = panelMaterial;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
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
