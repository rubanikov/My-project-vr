using System;
using System.Collections;
using UnityEngine;

// Court Clash's match/session flow, padel version (2026-08-26 — supersedes
// the ticket-02 continuous-play flow):
//   - No menu UI — the match starts the instant the player first hits the
//     ball with the racket (the player always serves the first rally).
//   - First-to-11 points, single game.
//   - Faults arrive instantly from BallFaultTracker (double bounce, failed
//     net clear, AI body hit) and award the point right away.
//   - After each point: short pause, serve alternates, and the ball is
//     handed to whoever serves — reset to the player's pedestal, or floated
//     to the AI for its computed serve.
//   - Match ends at 11: win/lose color flash (Scoreboard shows the text),
//     ball resets, serve resets to the player, and the next player hit
//     starts a new match.
public class MatchController : MonoBehaviour
{
    private const int WinScore = 11;

    [Header("References")]
    [SerializeField] private BallFaultTracker ballFaultTracker;
    [SerializeField] private Transform ball;
    [SerializeField] private Renderer ballRenderer;
    [SerializeField] private AIOpponent aiOpponent;
    [SerializeField] private CourtBuilder courtBuilder;

    [Header("Ball reset (recomputed to the player's half-center once the court is built)")]
    [SerializeField] private Vector3 ballRestPosition = new Vector3(0f, 1f, 0.4f);

    [Header("Rally flow")]
    [SerializeField] private float betweenRallyPauseSeconds = 1.5f;

    [Header("Win feedback (color flash — Scoreboard adds the text)")]
    [SerializeField] private Color playerWinFlashColor = Color.white;
    [SerializeField] private Color aiWinFlashColor = Color.red;
    [SerializeField] private float winFlashDurationSeconds = 1.5f;

    public int PlayerScore { get; private set; }
    public int AIScore { get; private set; }
    public bool MatchInProgress { get; private set; }
    public Side ServingSide { get; private set; } = Side.Player;

    public event Action MatchStarted;
    public event Action<Side, int, int> PointScored; // (side, playerScore, aiScore)
    public event Action<Side, FaultKind> PointDetail; // who won the point and why (for the scoreboard)
    public event Action<Side> ServeLetOccurred; // serve didn't clear — same side serves again, no point
    public event Action<Side> ServeChanged;
    public event Action<Side> MatchEnded; // winner
    public event Action MatchReset;

    private Coroutine rallyFlowCoroutine;

    private void OnEnable()
    {
        if (ballFaultTracker != null) ballFaultTracker.Fault += OnFault;
        if (courtBuilder != null) courtBuilder.CourtBuilt += OnCourtBuilt;
    }

    private void OnDisable()
    {
        if (ballFaultTracker != null) ballFaultTracker.Fault -= OnFault;
        if (courtBuilder != null) courtBuilder.CourtBuilt -= OnCourtBuilt;
    }

    // The pedestal follows the court: middle of the player's half, wherever
    // the play area actually is (the court is no longer centered on the
    // launch position).
    private void OnCourtBuilt(Vector3 halfExtents)
    {
        ballRestPosition = new Vector3(
            courtBuilder.CenterX, 1f, courtBuilder.NetZ + courtBuilder.HalfDepthPerSide * 0.5f);
        ResetBall();
    }

    // Called from PlayerRacket's collision handler on every racket-ball hit —
    // the single trigger for both starting the first match and restarting
    // after one ends.
    public void OnBallInPlay()
    {
        if (!MatchInProgress)
        {
            StartMatch();
        }
    }

    private void StartMatch()
    {
        PlayerScore = 0;
        AIScore = 0;
        MatchInProgress = true;
        ServingSide = Side.Player;
        MatchStarted?.Invoke();
        ServeChanged?.Invoke(ServingSide);
    }

    private void OnFault(Side pointGoesTo, FaultKind kind)
    {
        if (!MatchInProgress) return;

        // A point was just scored and the next serve is pending — anything
        // the dead ball does in that window (including someone poking it
        // with a racket, reviving the rally, and instantly re-faulting) must
        // not score. This was one source of the "score randomly jumps"
        // playtest report (2026-08-27).
        if (rallyFlowCoroutine != null) return;

        if (kind == FaultKind.ServeLet)
        {
            // Serve didn't clear the net: no point, same side serves again.
            ServeLetOccurred?.Invoke(pointGoesTo);
            rallyFlowCoroutine = StartCoroutine(ReServeAfterPause());
            return;
        }

        AwardPoint(pointGoesTo);
        PointDetail?.Invoke(pointGoesTo, kind);
    }

    private IEnumerator ReServeAfterPause()
    {
        yield return new WaitForSeconds(1f);

        ballFaultTracker?.ResetRally();
        ServeChanged?.Invoke(ServingSide);

        if (ServingSide == Side.Player)
        {
            ResetBall();
        }
        else if (aiOpponent != null)
        {
            aiOpponent.BeginServe();
        }
        rallyFlowCoroutine = null;
    }

    // Full reset back to the pre-match idle state (pause menu's B button).
    public void ResetMatch()
    {
        if (rallyFlowCoroutine != null)
        {
            StopCoroutine(rallyFlowCoroutine);
            rallyFlowCoroutine = null;
        }
        PlayerScore = 0;
        AIScore = 0;
        MatchInProgress = false;
        ServingSide = Side.Player;
        ballFaultTracker?.ResetRally();
        ResetBall();
        MatchReset?.Invoke();
    }

    private void AwardPoint(Side side)
    {
        if (side == Side.Player) PlayerScore++;
        else AIScore++;

        PointScored?.Invoke(side, PlayerScore, AIScore);

        if (PlayerScore >= WinScore || AIScore >= WinScore)
        {
            EndMatch(PlayerScore >= WinScore ? Side.Player : Side.AI);
        }
        else
        {
            if (rallyFlowCoroutine != null) StopCoroutine(rallyFlowCoroutine);
            rallyFlowCoroutine = StartCoroutine(NextRallyAfterPause());
        }
    }

    private IEnumerator NextRallyAfterPause()
    {
        yield return new WaitForSeconds(betweenRallyPauseSeconds);

        ServingSide = ServingSide == Side.Player ? Side.AI : Side.Player;
        ballFaultTracker?.ResetRally();
        ServeChanged?.Invoke(ServingSide);

        if (ServingSide == Side.Player)
        {
            ResetBall();
        }
        else if (aiOpponent != null)
        {
            aiOpponent.BeginServe();
        }
        rallyFlowCoroutine = null;
    }

    private void EndMatch(Side winner)
    {
        MatchInProgress = false;
        ServingSide = Side.Player; // next match starts on the player's serve
        MatchEnded?.Invoke(winner);
        StartCoroutine(FlashAndReset(winner));
    }

    private IEnumerator FlashAndReset(Side winner)
    {
        if (ballRenderer != null)
        {
            Color flashColor = winner == Side.Player ? playerWinFlashColor : aiWinFlashColor;
            Material instanceMaterial = ballRenderer.material; // intentional per-instance copy for a temporary effect
            Color original = instanceMaterial.color;
            instanceMaterial.color = flashColor;
            yield return new WaitForSeconds(winFlashDurationSeconds);
            instanceMaterial.color = original;
        }
        ballFaultTracker?.ResetRally();
        ResetBall();
    }

    private void ResetBall()
    {
        if (ball == null) return;
        if (ball.TryGetComponent(out Rigidbody rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        ball.position = ballRestPosition;
    }
}
