using System.Collections;
using UnityEngine;
using Meta.XR.ImmersiveDebugger;

// Court Clash's racket: a kinematic paddle that follows the right controller
// and hits the ball with a computed "padel-style" response (see CONTEXT.md for
// the Grip/Regrip/Hit vocabulary).
//
// Reworked 2026-08-26 after the user's playtest ("the ball when hitting the
// racket moves very slowly if any at all"). Root cause: the Racket GameObject
// was BOTH parented under RightControllerAnchor AND MovePosition'd to it every
// FixedUpdate. Parenting drags the transform along as a teleport (zero
// velocity), so by the time MovePosition ran the racket was already at the
// target — sweep distance ~0, implicit velocity ~0, and PhysX resolved every
// contact against an effectively stationary racket. The fix is twofold:
// 1. The racket is UNPARENTED (scene change) and follows the hand purely via
//    MovePosition/MoveRotation, so the kinematic sweep carries real velocity.
// 2. On contact we don't trust PhysX's kinematic-vs-dynamic response (muted
//    even when set up correctly): the ball's exit velocity is computed here
//    from the real swing — racket velocity at the contact point plus the
//    incoming ball reflected off the face — the standard VR racket-game model.
[RequireComponent(typeof(Rigidbody))]
public class PlayerRacket : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform controllerAnchor;
    [SerializeField] private Rigidbody ball;
    [SerializeField] private BallFaultTracker ballFaultTracker;
    [SerializeField] private MatchController matchController;

    [Header("Hit feel")]
    [Tooltip("Scales the racket's own velocity contribution to the ball's exit velocity.")]
    [SerializeField] private float hitPower = 1.2f;
    [Tooltip("How much of the incoming ball speed reflects off the face (string-bed bounce).")]
    [SerializeField] private float faceRestitution = 0.7f;
    [SerializeField] private float maxBallSpeed = 15f;

    [Header("Hit feedback")]
    [Tooltip("The ball's sound player; the Hit sound reuses the haptic intensity curve. Auto-found from the ball when empty.")]
    [SerializeField] private BallContactSounds ballSounds;
    [Tooltip("Haptic amplitude of the gentlest contact (a graze).")]
    [SerializeField] private float hapticFloor = 0.3f;
    [Tooltip("Closing speed (m/s) at which the haptic pulse saturates at full amplitude.")]
    [SerializeField] private float hapticSaturationSpeed = 8f;
    [SerializeField] private float hapticPulseSeconds = 0.03f;

    private Rigidbody rb;
    private Coroutine hapticStopCoroutine;

    // The Grip: the racket's pose relative to the hand that owns it. During a
    // Regrip the racket temporarily follows the left hand instead; the stored
    // grip is always relative to controllerAnchor (the right hand).
    private Transform followAnchor;
    private Vector3 followPosition;
    private Quaternion followRotation;
    private Vector3 gripPosition;
    private Quaternion gripRotation;

    // Swing state, sampled from consecutive follow targets each physics step.
    // Derived from pose deltas rather than OVRInput so it automatically
    // includes the grip offset's lever arm (head speed > hand speed).
    private Vector3 swingVelocity;
    private Vector3 swingAngularVelocity;
    private Vector3 lastTargetPosition;
    private Quaternion lastTargetRotation;
    private bool hasLastTarget;

    // The ball's velocity as of the start of the current physics step.
    // OnCollisionEnter fires after the solver has already adjusted the ball,
    // so the post-solve velocity is useless as an "incoming" value.
    private Vector3 ballVelocityBeforeStep;

    private float lastHitTime = float.NegativeInfinity;
    private const float HitCooldown = 0.15f;

    private const string GripPositionKey = "CourtClash.Grip.Position";
    private const string GripRotationKey = "CourtClash.Grip.Rotation";

    // Default Grip (padel-style): handle continuing the forearm line, head in
    // front of the fist, face vertical (handshake grip). The racket model's
    // head runs along its local +Y and its face normal along local +Z, hence
    // the axis remap: local +Y (head) maps to the hand's forward pitched 35°
    // down (the controller's pointing axis tilts up relative to the fist, so
    // pitching down continues the arm line), local +Z (face) maps to the
    // hand's +X.
    private static readonly Vector3 DefaultGripPosition = new Vector3(0f, 0f, 0.02f);

    private static Quaternion DefaultGripRotation =>
        Quaternion.LookRotation(Vector3.right, Quaternion.AngleAxis(35f, Vector3.right) * Vector3.forward);

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        // The racket is the fastest object in the game (hand speed times the
        // grip's lever arm) and kinematic bodies get no sweep CCD — Speculative
        // is the only continuous mode they support, and without it a fast
        // swing can step over the ball or shove it through the court shell.
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // Default physics runs at 50Hz (0.02s), well under Quest 2's ~72-90Hz
        // display/tracking refresh — a real hand swing updates controllerAnchor
        // faster than FixedUpdate was sampling it, coarsening exactly the kind
        // of fast, precise contact this component depends on.
        Time.fixedDeltaTime = 1f / 90f;

        if (ballSounds == null && ball != null)
        {
            ballSounds = ball.GetComponent<BallContactSounds>();
        }

        LoadGrip();
        RestoreOwnGrip();
    }

    private void FixedUpdate()
    {
        if (followAnchor == null) return;

        if (ball != null)
        {
            ballVelocityBeforeStep = ball.linearVelocity;
        }

        Vector3 targetPosition = followAnchor.TransformPoint(followPosition);
        Quaternion targetRotation = followAnchor.rotation * followRotation;

        if (hasLastTarget)
        {
            float dt = Time.fixedDeltaTime;
            swingVelocity = (targetPosition - lastTargetPosition) / dt;

            Quaternion delta = targetRotation * Quaternion.Inverse(lastTargetRotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            swingAngularVelocity = float.IsNaN(axis.x) ? Vector3.zero : axis * (angle * Mathf.Deg2Rad / dt);
        }

        lastTargetPosition = targetPosition;
        lastTargetRotation = targetRotation;
        hasLastTarget = true;

        rb.MovePosition(targetPosition);
        rb.MoveRotation(targetRotation);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (ball == null || collision.rigidbody != ball) return;
        if (Time.time - lastHitTime < HitCooldown) return;
        lastHitTime = Time.time;

        // One normalized intensity (closing speed over the saturation speed)
        // drives both the haptic pulse and the Hit sound, so hand and ear
        // always report the same contact. A graze comes back as 0 and still
        // feeds through — contact you can see should be contact you can feel.
        float closingSpeed = ApplyHit(collision);
        float intensity = Mathf.Clamp01(closingSpeed / hapticSaturationSpeed);
        PulseHaptics(intensity);
        ballSounds?.PlayRacketHit(intensity);

        ballFaultTracker?.NotifyTouched(Side.Player);
        matchController?.OnBallInPlay();
    }

    // Returns the closing speed of ball and face (m/s), 0 for a graze that
    // was already separating and left untouched.
    private float ApplyHit(Collision collision)
    {
        ContactPoint contact = collision.GetContact(0);

        // Racket velocity at the contact point: linear plus the angular
        // contribution (a wrist flick moves the head much faster than the hand).
        Vector3 racketVelocity = swingVelocity
            + Vector3.Cross(swingAngularVelocity, contact.point - rb.position);

        // Face normal is the racket's thin local Z axis, signed toward the ball.
        Vector3 normal = transform.forward;
        if (Vector3.Dot(ball.position - contact.point, normal) < 0f) normal = -normal;

        Vector3 relativeVelocity = ballVelocityBeforeStep - racketVelocity;
        float approach = Vector3.Dot(relativeVelocity, normal);

        // Separating already (e.g. a graze PhysX resolved fine) — leave it alone.
        if (approach >= 0f) return 0f;

        Vector3 reflected = relativeVelocity - (1f + faceRestitution) * approach * normal;
        Vector3 outgoing = (racketVelocity * hitPower + reflected) * BallPhysicsTuning.SpeedMultiplier;

        ball.linearVelocity = Vector3.ClampMagnitude(
            outgoing, maxBallSpeed * BallPhysicsTuning.SpeedMultiplier);
        return -approach;
    }

    // One pulse per Hit: fixed length, full frequency, amplitude carrying the
    // expressiveness (floor to 1 over the closing speed). The buzz goes to
    // whichever hand holds the racket — right normally, left mid-Regrip.
    private void PulseHaptics(float intensity01)
    {
        OVRInput.Controller hand = followAnchor == controllerAnchor
            ? OVRInput.Controller.RTouch
            : OVRInput.Controller.LTouch;

        OVRInput.SetControllerVibration(1f, Mathf.Lerp(hapticFloor, 1f, intensity01), hand);

        if (hapticStopCoroutine != null) StopCoroutine(hapticStopCoroutine);
        hapticStopCoroutine = StartCoroutine(StopHaptics(hand));
    }

    private IEnumerator StopHaptics(OVRInput.Controller hand)
    {
        // Realtime so a pause mid-pulse can't pin the motor on until the
        // runtime's 2-second safety timeout.
        yield return new WaitForSecondsRealtime(hapticPulseSeconds);
        OVRInput.SetControllerVibration(1f, 0f, hand);
        hapticStopCoroutine = null;
    }

    // --- Grip management (used by RacketRegrip) ---

    // Grab in place: follow `anchor` while preserving the racket's current
    // world pose — the hand takes the racket wherever it happens to hold it.
    public void FollowTemporarily(Transform anchor)
    {
        followAnchor = anchor;
        followPosition = anchor.InverseTransformPoint(transform.position);
        followRotation = Quaternion.Inverse(anchor.rotation) * transform.rotation;
    }

    // The Regrip snap: the racket's current pose relative to the right hand
    // becomes the new persistent Grip.
    public void AdoptCurrentPoseAsGrip()
    {
        gripPosition = controllerAnchor.InverseTransformPoint(transform.position);
        gripRotation = Quaternion.Inverse(controllerAnchor.rotation) * transform.rotation;
        SaveGrip();
        RestoreOwnGrip();
    }

    // Return to the right hand with the stored Grip (also the abandoned-regrip path).
    public void RestoreOwnGrip()
    {
        followAnchor = controllerAnchor;
        followPosition = gripPosition;
        followRotation = gripRotation;
        // The racket may jump here (e.g. released mid-adjust) — don't let that
        // teleport read as a huge swing on the next step.
        hasLastTarget = false;
        swingVelocity = Vector3.zero;
        swingAngularVelocity = Vector3.zero;
    }

    [DebugMember(Category = "Racket", DisplayName = "Reset Grip")]
    public void ResetGrip()
    {
        gripPosition = DefaultGripPosition;
        gripRotation = DefaultGripRotation;
        PlayerPrefs.DeleteKey(GripPositionKey);
        PlayerPrefs.DeleteKey(GripRotationKey);
        RestoreOwnGrip();
    }

    private void SaveGrip()
    {
        PlayerPrefs.SetString(GripPositionKey,
            $"{gripPosition.x},{gripPosition.y},{gripPosition.z}");
        PlayerPrefs.SetString(GripRotationKey,
            $"{gripRotation.x},{gripRotation.y},{gripRotation.z},{gripRotation.w}");
        PlayerPrefs.Save();
    }

    private void LoadGrip()
    {
        gripPosition = DefaultGripPosition;
        gripRotation = DefaultGripRotation;

        string position = PlayerPrefs.GetString(GripPositionKey, "");
        string rotation = PlayerPrefs.GetString(GripRotationKey, "");
        if (string.IsNullOrEmpty(position) || string.IsNullOrEmpty(rotation)) return;

        string[] p = position.Split(',');
        string[] r = rotation.Split(',');
        if (p.Length != 3 || r.Length != 4) return;

        gripPosition = new Vector3(float.Parse(p[0]), float.Parse(p[1]), float.Parse(p[2]));
        gripRotation = new Quaternion(float.Parse(r[0]), float.Parse(r[1]), float.Parse(r[2]), float.Parse(r[3]));
    }

    // --- In-headset tuning (Immersive Debugger, same panel setup as BallPhysicsTuning) ---

    [DebugMember(Category = "Racket", Tweakable = true, Min = 0.5f, Max = 3f, DisplayName = "Hit Power")]
    public float HitPower
    {
        get => hitPower;
        set => hitPower = value;
    }

    [DebugMember(Category = "Racket", Tweakable = true, Min = 0f, Max = 1f, DisplayName = "Face Restitution")]
    public float FaceRestitution
    {
        get => faceRestitution;
        set => faceRestitution = value;
    }

    [DebugMember(Category = "Racket", Tweakable = true, Min = 5f, Max = 30f, DisplayName = "Max Ball Speed")]
    public float MaxBallSpeed
    {
        get => maxBallSpeed;
        set => maxBallSpeed = value;
    }

    [DebugMember(Category = "Racket", Tweakable = true, Min = 0f, Max = 1f, DisplayName = "Haptic Floor")]
    public float HapticFloor
    {
        get => hapticFloor;
        set => hapticFloor = value;
    }

    [DebugMember(Category = "Racket", Tweakable = true, Min = 2f, Max = 20f, DisplayName = "Haptic Saturation Speed")]
    public float HapticSaturationSpeed
    {
        get => hapticSaturationSpeed;
        set => hapticSaturationSpeed = value;
    }

    [DebugMember(Category = "Racket", Tweakable = true, Min = 0.01f, Max = 0.1f, DisplayName = "Haptic Pulse Seconds")]
    public float HapticPulseSeconds
    {
        get => hapticPulseSeconds;
        set => hapticPulseSeconds = value;
    }
}
