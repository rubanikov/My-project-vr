using UnityEngine;
using Meta.XR.ImmersiveDebugger;

// Lets the player recall and hold the ball in their off-hand (left
// controller) with a grip press, since the racket mechanic (PlayerRacket.cs)
// has no other way to retrieve the ball once it's out of reach — added
// 2026-08-26 after the user's first hands-on test: "make the button in the
// controller to bring the ball to the hand without the racket."
//
// Gated 2026-08-27 (rules grill — supersedes the unconditional recall; see
// CONTEXT.md "Ball Recall"): recall is legal only while the ball is waiting
// on the PLAYER'S serve — pre-match idle / warm-up, or the player is the
// serving side with no live rally. During a live rally or the AI's serve the
// grip does nothing (ungated, a grab during the AI's kinematic serve wind-up
// fought ServeRoutine for the ball and the serve then fired from the
// player's hand). If the AI's serve begins while the ball is held, the serve
// takes the ball: the hold ends silently and the ball's physics state is
// left to the serve routine that now owns it.
//
// The ball is kinematic for the duration of the hold (a one-shot position
// snap left the dynamic ball's collider overlapping solid geometry and
// depenetration launched it vertically — kinematic bodies are immune to that
// whole bug class).
//
// Release is a Toss (2026-08-26, user: "moving the hand upwards and releasing
// makes the ball drop instead of move upward"): the ball inherits the left
// hand's real tracked velocity, scaled by Throw Power. The velocity comes
// from OVRInput in tracking space and is rotated into world space — the exact
// pattern Meta's own OVRGrabber uses on release — then divided by the Game
// Speed time scale so the real-time hand reads correctly in a dilated world
// (docs/adr/0001-game-speed-is-time-dilation.md). Throw Power defaults above
// 1.0 because VR throws chronically read ~20-30% weaker than intended (no
// real wind-up). Still scoring-neutral: no NotifyTouched, no match-start —
// putting the ball in play is the racket's job.
public class BallRecall : MonoBehaviour
{
    [SerializeField] private Transform leftControllerAnchor;
    [SerializeField] private Rigidbody ball;
    [Tooltip("Gate source: recall only when it's the player's serve. Auto-found when empty.")]
    [SerializeField] private MatchController matchController;
    [Tooltip("Gate source: a live rally forbids recall. Auto-found when empty.")]
    [SerializeField] private BallFaultTracker ballFaultTracker;
    [Tooltip("Matches PlayerRacket's grip-press convention (OVRInput.Axis1D, not the Button enum).")]
    [SerializeField] private float pressThreshold = 0.5f;
    [SerializeField] private float throwPower = 1.2f;

    private Transform trackingSpace;
    private bool isHeld;

    private void Awake()
    {
        OVRCameraRig rig = FindFirstObjectByType<OVRCameraRig>();
        if (rig != null)
        {
            trackingSpace = rig.trackingSpace;
        }
        if (matchController == null) matchController = FindFirstObjectByType<MatchController>();
        if (ballFaultTracker == null) ballFaultTracker = FindFirstObjectByType<BallFaultTracker>();
    }

    // Pre-match idle and warm-up are always legal; inside a match, only the
    // player's own pending serve.
    private bool RecallLegal =>
        matchController == null
        || !matchController.MatchInProgress
        || (matchController.ServingSide == Side.Player
            && (ballFaultTracker == null || !ballFaultTracker.RallyLive));

    private void Update()
    {
        if (leftControllerAnchor == null || ball == null) return;
        // Menu open: the world is frozen and the ball with it. Deferring
        // grip changes to resume also keeps the Release divide below away
        // from timeScale 0.
        if (Time.timeScale == 0f) return;

        // The hold became illegal mid-grip — the AI's serve just took the
        // ball. Drop without touching physics: the serve routine owns the
        // (kinematic) ball now, and a Release here would yank it dynamic
        // mid-wind-up.
        if (isHeld && !RecallLegal)
        {
            isHeld = false;
            return;
        }

        bool isPressed =
            OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch) >= pressThreshold;

        if (isPressed && !isHeld && RecallLegal)
        {
            Grab();
        }
        else if (!isPressed && isHeld)
        {
            Release();
        }

        if (isHeld)
        {
            ball.position = leftControllerAnchor.position;
        }
    }

    private void Grab()
    {
        isHeld = true;
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        ball.isKinematic = true;
        ball.position = leftControllerAnchor.position;
    }

    private void Release()
    {
        isHeld = false;
        ball.isKinematic = false;

        Vector3 handVelocity = OVRInput.GetLocalControllerVelocity(OVRInput.Controller.LTouch);
        if (trackingSpace != null)
        {
            handVelocity = trackingSpace.rotation * handVelocity;
        }
        // Real-time hand velocity → game-time units (a toss released exactly
        // at pause-open would divide by 0, hence the floor).
        handVelocity /= Mathf.Max(Time.timeScale, 0.05f);
        ball.linearVelocity = handVelocity * throwPower;
    }

    [DebugMember(Category = "Ball Physics", Tweakable = true, Min = 0.5f, Max = 3f, DisplayName = "Throw Power")]
    public float ThrowPower
    {
        get => throwPower;
        set => throwPower = value;
    }
}
