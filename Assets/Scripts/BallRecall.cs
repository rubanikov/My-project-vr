using UnityEngine;
using Meta.XR.ImmersiveDebugger;

// Lets the player recall and hold the ball in their off-hand (left
// controller) with a grip press, since the racket mechanic (PlayerRacket.cs)
// has no other way to retrieve the ball once it's out of reach — added
// 2026-08-26 after the user's first hands-on test: "make the button in the
// controller to bring the ball to the hand without the racket."
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
// pattern Meta's own OVRGrabber uses on release. Throw Power defaults above
// 1.0 because VR throws chronically read ~20-30% weaker than intended (no
// real wind-up). Still scoring-neutral: no NotifyTouched, no match-start —
// putting the ball in play is the racket's job.
public class BallRecall : MonoBehaviour
{
    [SerializeField] private Transform leftControllerAnchor;
    [SerializeField] private Rigidbody ball;
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
    }

    private void Update()
    {
        if (leftControllerAnchor == null || ball == null) return;

        bool isPressed =
            OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch) >= pressThreshold;

        if (isPressed && !isHeld)
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
        ball.linearVelocity = handVelocity * throwPower;
    }

    [DebugMember(Category = "Ball Physics", Tweakable = true, Min = 0.5f, Max = 3f, DisplayName = "Throw Power")]
    public float ThrowPower
    {
        get => throwPower;
        set => throwPower = value;
    }
}
