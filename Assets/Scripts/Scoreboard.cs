using UnityEngine;

// Semi-transparent scoreboard on the wall behind the AI — the wall the
// player faces all match (user decision 2026-08-26). Shows both scores, whose
// Serve it is (essential now that serves alternate), and the match result.
//
// Built at runtime once CourtBuilder reports the real court size, same as
// every other court fixture. Uses legacy TextMesh rather than TextMeshPro on
// purpose: TMP needs its Essential Resources imported and adds nothing at
// this text size, while TextMesh ships with the engine.
public class Scoreboard : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private CourtBuilder courtBuilder;
    [Tooltip("Transparent dark material for the backing panel (must be an asset so its shader variant ships in builds).")]
    [SerializeField] private Material panelMaterial;

    [Header("Layout")]
    [SerializeField] private float heightOnWall = 2f;
    [SerializeField] private Vector2 panelSize = new Vector2(2.2f, 0.9f);

    private TextMesh scoreText;
    private TextMesh statusText;

    private void OnEnable()
    {
        if (courtBuilder != null) courtBuilder.CourtBuilt += OnCourtBuilt;
        if (matchController != null)
        {
            matchController.MatchStarted += OnMatchStarted;
            matchController.PointScored += OnPointScored;
            matchController.ServeChanged += OnServeChanged;
            matchController.MatchEnded += OnMatchEnded;
        }
    }

    private void OnDisable()
    {
        if (courtBuilder != null) courtBuilder.CourtBuilt -= OnCourtBuilt;
        if (matchController != null)
        {
            matchController.MatchStarted -= OnMatchStarted;
            matchController.PointScored -= OnPointScored;
            matchController.ServeChanged -= OnServeChanged;
            matchController.MatchEnded -= OnMatchEnded;
        }
    }

    private void OnCourtBuilt(Vector3 halfExtents)
    {
        // Just inside the far (AI-side) wall, facing the player at +Z.
        transform.position = new Vector3(0f, heightOnWall, courtBuilder.CourtMinZ + 0.12f);
        transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        BuildVisuals();
        ShowIdleState();
    }

    private void BuildVisuals()
    {
        if (scoreText != null) return; // already built (court rebuilds don't happen, but be safe)

        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "ScoreboardPanel";
        Destroy(panel.GetComponent<Collider>());
        panel.transform.SetParent(transform, false);
        panel.transform.localScale = new Vector3(panelSize.x, panelSize.y, 0.02f);
        if (panelMaterial != null)
        {
            panel.GetComponent<Renderer>().sharedMaterial = panelMaterial;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        scoreText = CreateText("ScoreText", new Vector3(0f, 0.12f, -0.03f), 0.055f, font,
            new Color(0.4f, 1f, 0.95f));
        statusText = CreateText("StatusText", new Vector3(0f, -0.24f, -0.03f), 0.03f, font,
            Color.white);
    }

    private TextMesh CreateText(string name, Vector3 localPosition, float characterSize, Font font, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPosition;

        TextMesh text = go.AddComponent<TextMesh>();
        text.font = font;
        text.fontSize = 64;
        text.characterSize = characterSize;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = color;

        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = font.material;
        return text;
    }

    private void ShowIdleState()
    {
        SetScore(0, 0);
        SetStatus("HIT THE BALL TO START — YOUR SERVE");
    }

    private void OnMatchStarted()
    {
        SetScore(0, 0);
    }

    private void OnPointScored(Side side, int playerScore, int aiScore)
    {
        SetScore(playerScore, aiScore);
    }

    private void OnServeChanged(Side servingSide)
    {
        SetStatus(servingSide == Side.Player ? "YOUR SERVE" : "AI SERVE");
    }

    private void OnMatchEnded(Side winner)
    {
        SetStatus(winner == Side.Player ? "YOU WIN!  HIT THE BALL FOR A REMATCH"
            : "AI WINS.  HIT THE BALL FOR A REMATCH");
    }

    private void SetScore(int playerScore, int aiScore)
    {
        if (scoreText != null) scoreText.text = $"YOU  {playerScore}  —  {aiScore}  AI";
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }
}
