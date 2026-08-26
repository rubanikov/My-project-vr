using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-click wiring of Court Clash's gameplay objects into the currently open
// scene. The court itself is generated procedurally at Play-mode Start()
// (see CourtBuilder) since it depends on runtime Guardian boundary data —
// this menu item just makes sure the GameObject + component exist ahead of time.
public static class CourtClashSetup
{
    [MenuItem("Court Clash/Setup Court In Scene")]
    public static void SetupCourt()
    {
        GameObject court = GameObject.Find("Court");
        if (court == null)
        {
            court = new GameObject("Court");
            Undo.RegisterCreatedObjectUndo(court, "Create Court");
        }

        if (court.GetComponent<CourtBuilder>() == null)
        {
            Undo.AddComponent<CourtBuilder>(court);
        }

        Selection.activeGameObject = court;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("Court Clash: 'Court' GameObject is set up with CourtBuilder. Press Play to generate the court geometry.");
    }
}
