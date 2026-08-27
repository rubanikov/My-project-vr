using UnityEngine;

// The referee voice (2026-08-27 user request: even with the scoreboard
// flash, instant faults made points hard to notice — "hard to know when
// someone won a point"). Speaks every point from the player's perspective:
// "you win the point" / "you lose the point".
//
// Deliberately 2D (no spatialization): a referee in your ear, not an object
// in the court. Clips are Windows-TTS-generated WAVs in Assets/Audio.
[RequireComponent(typeof(AudioSource))]
public class MatchAnnouncer : MonoBehaviour
{
    [SerializeField] private MatchController matchController;
    [SerializeField] private AudioClip winPointClip;
    [SerializeField] private AudioClip losePointClip;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        if (matchController == null) matchController = FindFirstObjectByType<MatchController>();
    }

    private void OnEnable()
    {
        if (matchController != null) matchController.PointScored += OnPointScored;
    }

    private void OnDisable()
    {
        if (matchController != null) matchController.PointScored -= OnPointScored;
    }

    private void OnPointScored(Side side, int playerScore, int aiScore)
    {
        AudioClip clip = side == Side.Player ? winPointClip : losePointClip;
        if (clip == null) return;
        audioSource.PlayOneShot(clip, volume);
    }
}
