using UnityEngine;

// AI robot skin cycling (2026-08-27 design-refresh): X on the left
// controller swaps the robot's shell material through a fixed set of skins —
// textures on the existing mesh ONLY (AI-generated meshes crashed the Quest
// at ~750k vertices; materials are the proven safe path). Persisted like
// difficulty; announced on the scoreboard via SkinChanged.
public class AISkinSelector : MonoBehaviour
{
    [System.Serializable]
    public struct Skin
    {
        public string name;
        public Material material;
    }

    [Header("References")]
    [Tooltip("The robot's visual root — every MeshRenderer under it gets the skin material.")]
    [SerializeField] private Transform robotVisual;
    [SerializeField] private Skin[] skins;

    public event System.Action<string> SkinChanged;

    private const string SkinPrefKey = "CourtClash.AISkin";
    private int index;

    private void Start()
    {
        if (skins == null || skins.Length == 0) return;
        index = Mathf.Clamp(PlayerPrefs.GetInt(SkinPrefKey, 0), 0, skins.Length - 1);
        Apply(announce: false);
    }

    private void Update()
    {
        if (skins == null || skins.Length == 0) return;
        if (Time.timeScale == 0f) return; // pause menu owns the controller

        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch)) // X
        {
            index = (index + 1) % skins.Length;
            PlayerPrefs.SetInt(SkinPrefKey, index);
            PlayerPrefs.Save();
            Apply(announce: true);
        }
    }

    private void Apply(bool announce)
    {
        Skin skin = skins[index];
        if (robotVisual != null && skin.material != null)
        {
            // Renderer, not MeshRenderer: the rigged robot draws through a
            // SkinnedMeshRenderer (2026-08-27 rig swap).
            foreach (var renderer in robotVisual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = skin.material;
            }
        }
        if (announce) SkinChanged?.Invoke(skin.name);
    }
}
