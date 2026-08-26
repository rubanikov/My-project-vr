using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-click VR scene wiring: swaps the default Main Camera for OVRCameraRig
// (Meta XR Core SDK). Controller grab interaction is deliberately NOT wired
// here — see the "Grab interaction" note below.
public static class VRSceneSetup
{
    private const string CameraRigPrefabPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";

    [MenuItem("Court Clash/Setup VR Scene (Camera Rig)")]
    public static void SetupVRScene()
    {
        GameObject rig = GameObject.Find("OVRCameraRig");
        if (rig == null)
        {
            GameObject mainCamera = GameObject.Find("Main Camera");
            if (mainCamera != null)
            {
                Undo.DestroyObjectImmediate(mainCamera);
            }

            GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraRigPrefabPath);
            if (rigPrefab == null)
            {
                Debug.LogError($"Court Clash: could not find OVRCameraRig prefab at '{CameraRigPrefabPath}'. " +
                    "Is the Meta XR Core SDK package installed?");
                return;
            }

            rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            Undo.RegisterCreatedObjectUndo(rig, "Create OVRCameraRig");
            Debug.Log("Court Clash: instantiated OVRCameraRig.");
        }
        else
        {
            Debug.Log("Court Clash: OVRCameraRig already in scene, skipping camera setup.");
        }

        Selection.activeGameObject = rig;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("Court Clash: VR scene setup complete — OVRCameraRig in place. " +
            "Grab interaction is NOT wired yet — see PRD.md's 'Grab interaction' note before adding it.");
    }

    // Grab interaction note (2026-08-24): tried instantiating Interaction SDK's
    // ControllerGrabInteractor.prefab directly under each OVRCameraRig
    // controller anchor. It compiled fine, but every frame in Play mode it
    // threw NullReferenceException from Oculus.Interaction.Input.ControllerRef
    // .get_IsConnected() (ActiveStateTracker.Update() -> ControllerRef.get_Active()
    // -> get_IsConnected(), see ControllerRef.cs:56) because ControllerRef's
    // serialized `_controller` field (an IController source) was never wired —
    // that prefab expects to sit inside Interaction SDK's Controllers.prefab
    // rig (RightController/LeftController, each with its own working
    // ControllerRef + data-source pair), not standalone. The per-frame
    // exception spam briefly starved the Unity MCP bridge's own update loop
    // ("Unity not detected") until Play mode was stopped.
    // Correct path: use the Editor menu Meta XR Tools > Building Blocks >
    // "Grab Interaction" block, which wires this dependency graph
    // automatically (verified in Meta's docs: bb-overview.md). That's a
    // GUI-driven window this session can't drive reliably, so it's left as a
    // manual step rather than re-guessing the wiring unattended.
}
