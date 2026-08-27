using UnityEngine;

// The controls / start gate (2026-08-27 user request): at app launch and
// before every match, a panel over the net shows the control map, and the
// match cannot start until the player presses the right index trigger.
// Until then racket-ball contact is inert (MatchController.SessionArmed),
// so warm-up swings can't start a match accidentally.
//
// Same runtime-built TextMesh presentation as the Scoreboard (and the same
// deliberate non-TMP choice — no Essential Resources dependency).
public class StartScreen : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private CourtBuilder courtBuilder;
    [Tooltip("Same transparent panel material as the scoreboard.")]
    [SerializeField] private Material panelMaterial;

    [Header("Layout")]
    [SerializeField] private float height = 1.9f;
    [SerializeField] private Vector2 panelSize = new Vector2(2f, 1.15f);

    private GameObject panelRoot;
    private bool visible;

    private const string ControlsText =
        "SWING THE RACKET TO HIT THE BALL\n\n" +
        "LEFT GRIP (HOLD)  —  RECALL BALL, RELEASE TO TOSS\n" +
        "LEFT TRIGGER NEAR RACKET  —  ADJUST GRIP,\n" +
        "THEN RIGHT GRIP  —  SNAP THE NEW GRIP\n\n" +
        "A  —  DIFFICULTY        MENU  —  PAUSE\n" +
        "B (WHILE PAUSED)  —  RESET MATCH";

    private void Awake()
    {
        if (matchController == null) matchController = FindFirstObjectByType<MatchController>();
        if (courtBuilder == null) courtBuilder = FindFirstObjectByType<CourtBuilder>();
    }

    private void OnEnable()
    {
        if (courtBuilder != null) courtBuilder.CourtBuilt += OnCourtBuilt;
        if (matchController != null)
        {
            matchController.MatchEnded += OnMatchEnded;
            matchController.MatchReset += Show;
        }
    }

    private void OnDisable()
    {
        if (courtBuilder != null) courtBuilder.CourtBuilt -= OnCourtBuilt;
        if (matchController != null)
        {
            matchController.MatchEnded -= OnMatchEnded;
            matchController.MatchReset -= Show;
        }
    }

    private void Update()
    {
        if (!visible || matchController == null) return;
        if (Time.timeScale == 0f) return; // pause menu owns the controller

        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            Hide();
            matchController.ArmSession();
        }
    }

    private void OnCourtBuilt(Vector3 halfExtents)
    {
        // Floating just past the net at eye height, facing the player, like
        // the scoreboard but close enough to read the small lines.
        transform.position = new Vector3(courtBuilder.CenterX, height, courtBuilder.NetZ + 0.4f);
        transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        BuildVisuals();
        Show();
    }

    private void OnMatchEnded(Side winner)
    {
        Show();
    }

    private void Show()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(true);
        visible = true;
    }

    private void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        visible = false;
    }

    private void BuildVisuals()
    {
        if (panelRoot != null) return;

        panelRoot = new GameObject("StartScreenPanel");
        panelRoot.transform.SetParent(transform, false);

        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "Backing";
        Destroy(panel.GetComponent<Collider>());
        panel.transform.SetParent(panelRoot.transform, false);
        panel.transform.localScale = new Vector3(panelSize.x, panelSize.y, 0.02f);
        if (panelMaterial != null)
        {
            panel.GetComponent<Renderer>().sharedMaterial = panelMaterial;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        CreateText("Title", new Vector3(0f, 0.44f, -0.03f), 0.045f, font,
            new Color(0.4f, 1f, 0.95f), "COURT CLASH");
        CreateText("Controls", new Vector3(0f, 0.02f, -0.03f), 0.02f, font,
            Color.white, ControlsText);
        CreateText("Prompt", new Vector3(0f, -0.45f, -0.03f), 0.035f, font,
            new Color(0.4f, 1f, 0.95f), "PRESS RIGHT TRIGGER TO START");
    }

    private void CreateText(string name, Vector3 localPosition, float characterSize, Font font,
        Color color, string content)
    {
        var go = new GameObject(name);
        go.transform.SetParent(panelRoot.transform, false);
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
    }
}
