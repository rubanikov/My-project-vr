using System;
using System.Collections;
using UnityEngine;

// Court Clash's match/session flow, v1 scope per wayfinder ticket 02
// (.scratch/court-clash/issues/02-match-session-structure.md):
//   - No menu UI — the match starts the instant the player first hits the
//     ball with the racket (was "first grab" before the racket mechanic
//     replaced grab-and-throw, 2026-08-26 — same one-trigger-for-the-whole-
//     session idea, just re-anchored to the new interaction).
//   - First-to-11 points, single game, no sets.
//   - A fault awards the other side a point (bounce-limit violation, via
//     BallFaultTracker.Fault — see BounceFaultRule.cs for that rule).
//   - Match ends the instant either side hits 11: simple win/lose feedback
//     (a color flash, since there's no UI system yet), ball resets to its
//     pedestal, and grabbing it again starts a new match — same trigger as
//     start, so there's exactly one interaction for the whole session.
//
// Known gap: ticket 02 also calls a "missed catch" (the ball leaving play
// entirely, not just over-bouncing) a fault trigger. That needs an
// out-of-bounds/floor-exit detector this project doesn't have yet — only the
// bounce-limit fault (ticket 01, fully specified) is wired here.
public class MatchController : MonoBehaviour
{
    private const int WinScore = 11;

    [Header("References")]
    [SerializeField] private BallFaultTracker ballFaultTracker;
    [SerializeField] private Transform ball;
    [SerializeField] private Renderer ballRenderer;

    [Header("Ball reset")]
    [SerializeField] private Vector3 ballRestPosition = new Vector3(0f, 1f, 0.4f);

    [Header("Win feedback (color flash — floating text can subscribe to MatchEnded later)")]
    [SerializeField] private Color playerWinFlashColor = Color.green;
    [SerializeField] private Color aiWinFlashColor = Color.red;
    [SerializeField] private float winFlashDurationSeconds = 1.5f;

    public int PlayerScore { get; private set; }
    public int AIScore { get; private set; }
    public bool MatchInProgress { get; private set; }

    public event Action MatchStarted;
    public event Action<Side, int, int> PointScored; // (side, playerScore, aiScore)
    public event Action<Side> MatchEnded; // winner

    private void OnEnable()
    {
        if (ballFaultTracker != null) ballFaultTracker.Fault += OnFault;
    }

    private void OnDisable()
    {
        if (ballFaultTracker != null) ballFaultTracker.Fault -= OnFault;
    }

    // Called from PlayerRacket's collision handler on every racket-ball hit —
    // it's the single trigger for both starting the first match and
    // restarting after one ends.
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
        MatchStarted?.Invoke();
    }

    private void OnFault(Side pointGoesTo)
    {
        if (!MatchInProgress) return;
        AwardPoint(pointGoesTo);
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
    }

    private void EndMatch(Side winner)
    {
        MatchInProgress = false;
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
