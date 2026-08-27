using UnityEngine;
using Meta.XR.ImmersiveDebugger;

// The ball's voice: every contact sound plays from one spatialized
// AudioSource on the ball, because every audible event — a Hit (racket
// contact), a floor Bounce, a wall/ceiling/net ricochet — physically happens
// to the ball, so the ball is always at the sound's true origin.
//
// Two clips: a racket "pok" shared by player and AI (same racket, same ball)
// and a duller surface bounce for every non-racket contact.
//
// Who calls what:
// - PlayerRacket calls PlayRacketHit with the SAME normalized intensity that
//   drives its haptic pulse, so what the hand feels and the ear hears always
//   agree — one curve to tune, not two.
// - AIOpponent calls PlayRacketHit from its computed shot; its Hit is not a
//   physics collision (it sets the ball's velocity directly), so no collider
//   event exists to piggyback on.
// - Surface contacts are detected here via OnCollisionEnter, scaled by the
//   ball's velocity change; player-racket contacts are recognized and
//   skipped (PlayerRacket owns those, with better intensity data).
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))]
public class BallContactSounds : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] private AudioClip racketHitClip;
    [SerializeField] private AudioClip surfaceBounceClip;

    [Header("References")]
    [Tooltip("The player racket's collider, so its contacts don't double as bounces. Auto-found when empty.")]
    [SerializeField] private Collider racketCollider;

    [Header("Feel")]
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 0.8f;
    [Tooltip("Volume of the gentlest audible contact, as a fraction of full.")]
    [Range(0f, 1f)]
    [SerializeField] private float volumeFloor = 0.25f;
    [Tooltip("Velocity change (m/s) at which a bounce reaches full volume.")]
    [SerializeField] private float bounceSaturationSpeed = 8f;
    [Tooltip("Bounces below this velocity change stay silent — a settling ball shouldn't tick forever.")]
    [SerializeField] private float minBounceSpeed = 0.75f;
    [Tooltip("Random pitch variation so one sample doesn't machine-gun.")]
    [SerializeField] private float pitchJitter = 0.05f;

    private Rigidbody rb;
    private AudioSource audioSource;
    private Vector3 velocityAtStepStart;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();

        // Configured here rather than in the Inspector so the setup menu item
        // stays one-click: full 3D through Meta XR Audio (the project's
        // registered spatializer plugin), no doppler — a fast ball smears
        // short transients into chirps.
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.spatialize = true;
        audioSource.dopplerLevel = 0f;

        if (racketCollider == null)
        {
            PlayerRacket racket = FindFirstObjectByType<PlayerRacket>();
            if (racket != null) racketCollider = racket.GetComponent<Collider>();
        }
    }

    private void FixedUpdate()
    {
        velocityAtStepStart = rb.linearVelocity;
    }

    // Surface contacts only (floor Bounce, wall/ceiling/net ricochets). The
    // racket's own OnCollisionEnter drives the Hit sound so its intensity
    // matches the haptic pulse exactly.
    private void OnCollisionEnter(Collision collision)
    {
        if (racketCollider != null && collision.collider == racketCollider) return;

        float speedChange = (rb.linearVelocity - velocityAtStepStart).magnitude;
        if (speedChange < minBounceSpeed) return;

        Play(surfaceBounceClip, speedChange / bounceSaturationSpeed);
    }

    // intensity01: 0 = gentlest contact (a graze), 1 = full-strength hit.
    public void PlayRacketHit(float intensity01)
    {
        Play(racketHitClip, intensity01);
    }

    private void Play(AudioClip clip, float intensity01)
    {
        if (clip == null) return;

        // Pitch is set on the source, which also bends any one-shot still
        // playing — inaudible at these clip lengths, and far cheaper than a
        // source pool.
        audioSource.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        audioSource.PlayOneShot(clip,
            masterVolume * Mathf.Lerp(volumeFloor, 1f, Mathf.Clamp01(intensity01)));
    }

    [DebugMember(Category = "Racket", Tweakable = true, Min = 0f, Max = 1f, DisplayName = "Hit Volume")]
    public float MasterVolume
    {
        get => masterVolume;
        set => masterVolume = value;
    }
}
