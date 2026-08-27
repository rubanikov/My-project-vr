using UnityEngine;

// Pause MENU (2026-08-27 design-refresh, evolving the earlier bare pause):
// the left controller's menu button freezes the game and floats a panel in
// front of the player with the ball-speed setting and the match controls.
//
//   MENU (left)      — open/close (Time.timeScale 0/1)
//   RIGHT STICK ◄ ►  — ball speed step [0.5 … 1.4] (writes the shared
//                      BallPhysicsTuning.SpeedMultiplier, persisted — the
//                      multiplier scales hit/shot speeds, never physics)
//   A                — resume
//   B                — reset match (stays in the menu so speed can be set
//                      before starting; the start screen shows on resume)
//
// Gameplay button handlers (difficulty, skins, start screen) gate themselves
// on Time.timeScale > 0 so the menu owns the controller while open. Same
// TextMesh presentation as the scoreboard/start screen.
public class MatchPauseController : MonoBehaviour
{
    [SerializeField] private MatchController matchController;
    [Tooltip("Same transparent panel material as the scoreboard.")]
    [SerializeField] private Material panelMaterial;

    public event System.Action<bool> PauseChanged;
    public bool IsPaused { get; private set; }

    private static readonly float[] SpeedSteps = { 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1f, 1.2f, 1.4f };
    private const string SpeedPrefKey = "CourtClash.BallSpeed";

    private GameObject menuRoot;
    private TextMesh speedText;
    private int speedIndex = 5; // 1.0x
    private float lastStickStepTime;

    private void Start()
    {
        // Runs after BallPhysicsTuning.Awake (which resets the multiplier to
        // 1 each launch) — restore the player's saved speed on top of that.
        speedIndex = ClosestStep(PlayerPrefs.GetFloat(SpeedPrefKey, 1f));
        ApplySpeed(save: false);
    }

    private void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.Start))
        {
            SetPaused(!IsPaused);
        }
        if (!IsPaused) return;

        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            SetPaused(false);
            return;
        }

        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            matchController?.ResetMatch(); // stay in the menu; resume via A/MENU
        }

        float stickX = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch).x;
        if (Mathf.Abs(stickX) > 0.6f && Time.unscaledTime - lastStickStepTime > 0.3f)
        {
            lastStickStepTime = Time.unscaledTime;
            speedIndex = Mathf.Clamp(speedIndex + (stickX > 0f ? 1 : -1), 0, SpeedSteps.Length - 1);
            ApplySpeed(save: true);
        }
    }

    private void SetPaused(bool paused)
    {
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        if (paused) ShowMenu();
        else if (menuRoot != null) menuRoot.SetActive(false);
        PauseChanged?.Invoke(paused);
    }

    private void ApplySpeed(bool save)
    {
        BallPhysicsTuning.SpeedMultiplier = SpeedSteps[speedIndex];
        if (save)
        {
            PlayerPrefs.SetFloat(SpeedPrefKey, SpeedSteps[speedIndex]);
            PlayerPrefs.Save();
        }
        if (speedText != null)
        {
            speedText.text = $"BALL SPEED   x{SpeedSteps[speedIndex]:0.0}";
        }
    }

    private static int ClosestStep(float value)
    {
        int best = 0;
        for (int i = 1; i < SpeedSteps.Length; i++)
        {
            if (Mathf.Abs(SpeedSteps[i] - value) < Mathf.Abs(SpeedSteps[best] - value)) best = i;
        }
        return best;
    }

    // The menu floats ~1.3m in front of wherever the player is looking when
    // they pause, at a comfortable height.
    private void ShowMenu()
    {
        BuildMenu();

        Transform head = Camera.main != null ? Camera.main.transform : null;
        if (head != null)
        {
            Vector3 forward = head.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            Vector3 position = head.position + forward * 1.3f;
            position.y = Mathf.Clamp(head.position.y, 1f, 2f);
            menuRoot.transform.position = position;
            menuRoot.transform.rotation = Quaternion.LookRotation(forward);
        }
        menuRoot.SetActive(true);
        ApplySpeed(save: false); // refresh the speed row
    }

    private void BuildMenu()
    {
        if (menuRoot != null) return;

        menuRoot = new GameObject("PauseMenu");

        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "Backing";
        Destroy(panel.GetComponent<Collider>());
        panel.transform.SetParent(menuRoot.transform, false);
        panel.transform.localScale = new Vector3(1.4f, 0.8f, 0.02f);
        if (panelMaterial != null)
        {
            panel.GetComponent<Renderer>().sharedMaterial = panelMaterial;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        CreateText("Title", new Vector3(0f, 0.28f, -0.03f), 0.04f, font,
            new Color(0.4f, 1f, 0.95f), "PAUSED");
        speedText = CreateText("Speed", new Vector3(0f, 0.08f, -0.03f), 0.035f, font,
            Color.white, "BALL SPEED   x1.0");
        CreateText("Hints", new Vector3(0f, -0.18f, -0.03f), 0.022f, font,
            new Color(0.8f, 0.85f, 0.9f),
            "RIGHT STICK  < >   CHANGE SPEED\n\n(A) RESUME        (B) RESET MATCH\nMENU  CLOSE");
    }

    private TextMesh CreateText(string name, Vector3 localPosition, float characterSize, Font font,
        Color color, string content)
    {
        var go = new GameObject(name);
        go.transform.SetParent(menuRoot.transform, false);
        go.transform.localPosition = localPosition;

        TextMesh text = go.AddComponent<TextMesh>();
        text.font = font;
        text.fontSize = 64;
        text.characterSize = characterSize;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = color;
        text.text = content;

        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        return text;
    }

    private void OnDisable()
    {
        // Never leave the game frozen if this component goes away.
        if (IsPaused) SetPaused(false);
    }
}
