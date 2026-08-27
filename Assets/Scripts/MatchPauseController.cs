using UnityEngine;

// Pause / resume / reset (2026-08-27, user request). The left controller's
// menu button (the only app-usable system button — the right Oculus button
// is reserved) toggles pause; while paused, B on the right controller resets
// the match to 0-0 idle and resumes.
//
// Pausing sets Time.timeScale = 0: physics, the AI, rally coroutines, and
// the ball all freeze (the player's racket also freezes mid-air, since it is
// physics-driven — expected while paused). OVRInput keeps updating, so the
// resume button still works.
public class MatchPauseController : MonoBehaviour
{
    [SerializeField] private MatchController matchController;

    public event System.Action<bool> PauseChanged;
    public bool IsPaused { get; private set; }

    private void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.Start))
        {
            SetPaused(!IsPaused);
        }

        if (IsPaused && OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            matchController?.ResetMatch();
            SetPaused(false);
        }
    }

    private void SetPaused(bool paused)
    {
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        PauseChanged?.Invoke(paused);
    }

    private void OnDisable()
    {
        // Never leave the game frozen if this component goes away.
        if (IsPaused) SetPaused(false);
    }
}
