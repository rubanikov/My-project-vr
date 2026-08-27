using UnityEngine;

// The one menu (2026-08-27 unification of StartScreen + MatchPauseController,
// which stacked two transparent panels on top of each other): launch, pause,
// and match-over all show this same Quest-style panel — headline, control
// map, the match settings (Game Speed and Difficulty, right stick), and a
// context line that changes with the situation.
//
// Game Speed is time dilation (docs/adr/0001-game-speed-is-time-dilation.md):
// the row drives Time.timeScale plus a matching fixedDeltaTime, so the whole
// world slows or quickens uniformly while the player's hands stay real-time.
// Closing the menu therefore restores the CHOSEN speed, never a bare 1.
//
// While open the game freezes, a black dome parented to the camera dims the
// scene, and the menu's materials sit on the overlay queue with ZTest Always
// so nothing behind it can bleed into the text. Closing without starting
// (A or MENU) leaves the session un-armed for warm-up play; starting a match
// is only possible from inside the menu (right trigger), so a stray squeeze
// mid-swing can never launch one.
//
// Same runtime-built TextMesh presentation as the Scoreboard (and the same
// deliberate non-TMP choice — no Essential Resources dependency). The text
// blocks are stacked from measured mesh bounds and auto-shrunk to fit the
// panel, so lines can't overlap the way the old hand-placed slots did.
public class GameMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private CourtBuilder courtBuilder;
    [Tooltip("Difficulty row target. Auto-found when empty.")]
    [SerializeField] private AIOpponent aiOpponent;
    [Tooltip("Opaque charcoal panel on the overlay queue (MenuPanel.mat).")]
    [SerializeField] private Material panelMaterial;
    [Tooltip("Translucent black dome that dims the scene (MenuDim.mat).")]
    [SerializeField] private Material dimMaterial;

    [Header("Layout")]
    [Tooltip("How far in front of the player's gaze the menu opens.")]
    [SerializeField] private float openDistance = 1.8f;
    [SerializeField] private Vector2 panelSize = new Vector2(2f, 1.5f);

    // Kept from MatchPauseController so the Scoreboard wiring survives —
    // "paused" now simply means "the menu is open".
    public event System.Action<bool> PauseChanged;
    public bool IsPaused { get; private set; }

    private static readonly Color AccentColor = new Color(0.35f, 0.65f, 1f); // Meta blue, brightened for charcoal
    private static readonly Color BodyColor = Color.white;

    private static readonly float[] SpeedSteps = { 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1f, 1.2f, 1.4f };
    private const string SpeedPrefKey = "CourtClash.GameSpeed";
    private const string LegacySpeedPrefKey = "CourtClash.BallSpeed"; // pre-dilation name, read once as fallback

    // Scene transparents (incl. all other TextMesh text) live at 3000: the
    // dome dims them, the panel covers the dome, the menu text tops the panel.
    private const int DimQueue = 3900;
    private const int PanelQueue = 4000;
    private const int TextQueue = 4010;

    private const string ControlsText =
        "SWING THE RACKET TO HIT THE BALL\n" +
        "LEFT GRIP (HOLD)  —  RECALL BALL ON YOUR SERVE, RELEASE TO TOSS\n" +
        "LEFT TRIGGER NEAR RACKET, THEN RIGHT GRIP  —  REGRIP\n" +
        "(X) AI SKIN          (Y) HOLD  —  REALIGN COURT";

    private GameObject panelRoot;
    private GameObject dimDome;
    private TextMesh headlineText;
    private TextMesh speedText;
    private TextMesh difficultyText;
    private TextMesh contextText;
    private Font font;
    private Material textMaterial;
    private int speedIndex = 5; // 1.0x
    private float lastStickStepTime;
    private Side? lastWinner;

    private void Awake()
    {
        if (matchController == null) matchController = FindFirstObjectByType<MatchController>();
        if (courtBuilder == null) courtBuilder = FindFirstObjectByType<CourtBuilder>();
        if (aiOpponent == null) aiOpponent = FindFirstObjectByType<AIOpponent>();
    }

    private void Start()
    {
        // Runs after PlayerRacket.Awake (which sets the base 90Hz physics
        // step) — restore the player's saved Game Speed on top of that.
        speedIndex = ClosestStep(PlayerPrefs.GetFloat(SpeedPrefKey,
            PlayerPrefs.GetFloat(LegacySpeedPrefKey, 1f)));
        ApplySpeed(save: false);
    }

    private void OnEnable()
    {
        if (courtBuilder != null) courtBuilder.CourtBuilt += OnCourtBuilt;
        if (matchController != null)
        {
            matchController.MatchEnded += OnMatchEnded;
            matchController.MatchReset += OnMatchReset;
        }
        if (aiOpponent != null) aiOpponent.DifficultyChanged += OnDifficultyChanged;
    }

    private void OnDisable()
    {
        if (courtBuilder != null) courtBuilder.CourtBuilt -= OnCourtBuilt;
        if (matchController != null)
        {
            matchController.MatchEnded -= OnMatchEnded;
            matchController.MatchReset -= OnMatchReset;
        }
        if (aiOpponent != null) aiOpponent.DifficultyChanged -= OnDifficultyChanged;
        // Never leave the game frozen if this component goes away.
        if (IsPaused) Close();
    }

    private void OnDestroy()
    {
        Font.textureRebuilt -= OnFontTextureRebuilt;
        if (textMaterial != null) Destroy(textMaterial);
        if (dimDome != null) Destroy(dimDome); // parented to the camera, not to us
    }

    private void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.Start))
        {
            if (IsPaused) Close();
            else Open();
            return;
        }
        if (!IsPaused) return;

        // Close on A *release*: on the press frame the game is still frozen,
        // so AIOpponent's timescale gate keeps the same press from also
        // toggling difficulty once we unfreeze.
        if (OVRInput.GetUp(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            Close();
            return;
        }

        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            matchController?.ResetMatch(); // stays open; OnMatchReset refreshes the context
        }

        if (matchController != null && !matchController.MatchInProgress &&
            OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            lastWinner = null;
            matchController.ArmSession();
            Close();
            return;
        }

        // One repeat guard for both rows, dominant axis wins — a diagonal
        // flick can't step speed and difficulty in the same beat.
        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
        if (Mathf.Max(Mathf.Abs(stick.x), Mathf.Abs(stick.y)) > 0.6f
            && Time.unscaledTime - lastStickStepTime > 0.3f)
        {
            lastStickStepTime = Time.unscaledTime;
            if (Mathf.Abs(stick.x) >= Mathf.Abs(stick.y))
            {
                speedIndex = Mathf.Clamp(speedIndex + (stick.x > 0f ? 1 : -1), 0, SpeedSteps.Length - 1);
                ApplySpeed(save: true);
            }
            else
            {
                aiOpponent?.StepDifficulty(stick.y > 0f ? 1 : -1);
            }
        }
    }

    private void OnDifficultyChanged(AIDifficulty difficulty)
    {
        RefreshDifficultyRow();
    }

    private void RefreshDifficultyRow()
    {
        if (difficultyText == null) return;
        string name = aiOpponent != null ? aiOpponent.Difficulty.ToString().ToUpper() : "-";
        difficultyText.text = $"DIFFICULTY   ^  {name}  v";
    }

    private void OnCourtBuilt(Vector3 halfExtents)
    {
        Open(); // the launch menu — and after any court rebuild
    }

    private void OnMatchEnded(Side winner)
    {
        lastWinner = winner;
        Open();
    }

    private void OnMatchReset()
    {
        lastWinner = null;
        Open();
    }

    private void Open()
    {
        BuildVisuals();
        PlaceInFrontOfPlayer();
        panelRoot.SetActive(true);
        AttachDim();
        if (!IsPaused)
        {
            IsPaused = true;
            Time.timeScale = 0f;
            PauseChanged?.Invoke(true);
        }
        RefreshContext();
        ApplySpeed(save: false); // refresh the speed row
        RefreshDifficultyRow();
    }

    private void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (dimDome != null) dimDome.SetActive(false);
        if (IsPaused)
        {
            IsPaused = false;
            // The chosen Game Speed, never a hard-coded 1 (ADR 0001).
            Time.timeScale = SpeedSteps[speedIndex];
            PauseChanged?.Invoke(false);
        }
    }

    // Quest-style: the menu opens facing the player, in whatever direction
    // they're looking, then stays world-locked while open.
    private void PlaceInFrontOfPlayer()
    {
        Transform head = Camera.main != null ? Camera.main.transform : null;
        if (head == null) return;

        Vector3 forward = head.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
        Vector3 position = head.position + forward * openDistance;
        position.y = Mathf.Clamp(head.position.y, 1f, 2f);
        transform.position = position;
        transform.rotation = Quaternion.LookRotation(forward);
    }

    private void AttachDim()
    {
        Transform head = Camera.main != null ? Camera.main.transform : null;
        if (head == null || dimDome == null) return;
        dimDome.transform.SetParent(head, false);
        dimDome.transform.localPosition = Vector3.zero;
        dimDome.SetActive(true);
    }

    private void RefreshContext()
    {
        if (headlineText == null) return;
        if (matchController != null && matchController.MatchInProgress)
        {
            headlineText.text = "PAUSED";
            contextText.text = "(A) RESUME      (B) RESET MATCH\nMENU  —  CLOSE";
        }
        else if (lastWinner.HasValue)
        {
            headlineText.text = lastWinner == Side.Player ? "YOU WIN THE MATCH" : "AI WINS THE MATCH";
            contextText.text = "PRESS RIGHT TRIGGER FOR A NEW MATCH\n(A) OR MENU  —  CLOSE FOR WARM-UP";
        }
        else
        {
            headlineText.text = "COURT CLASH";
            contextText.text = "PRESS RIGHT TRIGGER TO START\n(A) OR MENU  —  CLOSE FOR WARM-UP";
        }
    }

    private void ApplySpeed(bool save)
    {
        float speed = SpeedSteps[speedIndex];
        // Time dilation (ADR 0001): the world's clock carries the setting,
        // and the physics step scales with it so the racket keeps sampling
        // the real-time hand ~90 times per real second at any speed.
        Time.fixedDeltaTime = BallPhysicsTuning.BaseFixedDeltaTime * speed;
        if (!IsPaused) Time.timeScale = speed;
        if (save)
        {
            PlayerPrefs.SetFloat(SpeedPrefKey, speed);
            PlayerPrefs.Save();
        }
        if (speedText != null)
        {
            speedText.text = $"GAME SPEED   <  x{speed:0.0}  >";
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

    private void BuildVisuals()
    {
        if (panelRoot != null) return;

        // Layout measures world-space bounds, so build unrotated; Open()
        // re-aims the whole thing afterwards.
        transform.rotation = Quaternion.identity;

        panelRoot = new GameObject("GameMenuPanel");
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

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        // A clone of the font material bumped onto the overlay queue; the
        // dynamic font atlas can rebuild, so track its texture.
        textMaterial = new Material(font.material) { renderQueue = TextQueue };
        Font.textureRebuilt += OnFontTextureRebuilt;

        headlineText = CreateText("Headline", 0.042f, AccentColor, "COURT CLASH");
        TextMesh controls = CreateText("Controls", 0.019f, BodyColor, ControlsText);
        speedText = CreateText("Speed", 0.032f, BodyColor, "GAME SPEED   <  x1.0  >");
        difficultyText = CreateText("Difficulty", 0.032f, BodyColor, "DIFFICULTY   ^  NORMAL  v");
        TextMesh settingsHint = CreateText("SettingsHint", 0.017f, BodyColor,
            "RIGHT STICK   < >  SPEED      ^ v  DIFFICULTY");
        // Laid out with the tallest (two-line) context variant so later,
        // shorter variants stay inside the same slot.
        contextText = CreateText("Context", 0.032f, AccentColor,
            "PRESS RIGHT TRIGGER TO START\n(A) OR MENU  —  CLOSE FOR WARM-UP");

        LayoutBlocks(new[] { headlineText, controls, speedText, difficultyText, settingsHint, contextText });

        dimDome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dimDome.name = "MenuDim";
        Destroy(dimDome.GetComponent<Collider>());
        dimDome.transform.localScale = Vector3.one * 1.6f; // 0.8 m radius, well past the near clip
        if (dimMaterial != null)
        {
            dimDome.GetComponent<Renderer>().sharedMaterial = dimMaterial;
        }
        dimDome.SetActive(false);
    }

    private TextMesh CreateText(string name, float characterSize, Color color, string content)
    {
        var go = new GameObject(name);
        go.transform.SetParent(panelRoot.transform, false);

        TextMesh text = go.AddComponent<TextMesh>();
        text.font = font;
        text.fontSize = 64;
        text.characterSize = characterSize;
        text.anchor = TextAnchor.UpperCenter;
        text.alignment = TextAlignment.Center;
        text.color = color;
        text.text = content;

        go.GetComponent<MeshRenderer>().sharedMaterial = textMaterial;
        return text;
    }

    // Stack the blocks top-down from measured mesh bounds — no guessed line
    // heights, so blocks cannot overlap. If the stack would overflow the
    // panel (or a line would overflow its width), every character size
    // shrinks by the same factor until it fits.
    private void LayoutBlocks(TextMesh[] stack)
    {
        const float edgeMargin = 0.1f;
        float gap = 0.05f;

        float availableHeight = panelSize.y - edgeMargin * 2f;
        float availableWidth = panelSize.x - edgeMargin * 2f;

        float used = MeasureAndPlace(stack, gap);
        float widest = 0f;
        foreach (TextMesh block in stack)
        {
            widest = Mathf.Max(widest, block.GetComponent<Renderer>().bounds.size.x);
        }

        float fit = Mathf.Min(availableHeight / used, availableWidth / widest);
        if (fit < 1f)
        {
            foreach (TextMesh block in stack)
            {
                block.characterSize *= fit;
            }
            gap *= fit;
            used = MeasureAndPlace(stack, gap);
        }

        // Center the stack vertically on the panel.
        float shift = used / 2f;
        foreach (TextMesh block in stack)
        {
            Vector3 p = block.transform.localPosition;
            block.transform.localPosition = new Vector3(p.x, p.y + shift, p.z);
        }
    }

    private float MeasureAndPlace(TextMesh[] stack, float gap)
    {
        float cursor = 0f;
        foreach (TextMesh block in stack)
        {
            block.transform.localPosition = new Vector3(0f, cursor, -0.03f);
            cursor -= block.GetComponent<Renderer>().bounds.size.y + gap;
        }
        return -(cursor + gap);
    }

    private void OnFontTextureRebuilt(Font rebuiltFont)
    {
        if (rebuiltFont == font && textMaterial != null)
        {
            textMaterial.mainTexture = font.material.mainTexture;
        }
    }
}
