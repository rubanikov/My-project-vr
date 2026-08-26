using UnityEngine;

// The Regrip gesture (2026-08-26, user-designed): hold the left index trigger
// with the left hand near the racket to grab it in place, position the right
// hand where the grip should really be, then press the right grip button to
// snap the racket to the right hand at exactly that relative pose. The
// captured Grip persists across sessions (PlayerRacket saves it); releasing
// the left trigger without snapping just returns the racket to its previous
// grip. Chosen over slider-based pose tuning — one natural gesture sets
// position and rotation at once, same idea as Eleven Table Tennis' adjust-grip.
//
// Button map: left index trigger = grab racket (left GRIP stays BallRecall's),
// right grip = snap. Right grip is only read mid-adjust, so it can't fire
// accidentally during normal play.
public class RacketRegrip : MonoBehaviour
{
    [SerializeField] private PlayerRacket racket;
    [SerializeField] private Transform leftControllerAnchor;
    [Tooltip("Max distance from the left hand to the racket's collider surface for a grab.")]
    [SerializeField] private float grabRadius = 0.2f;
    [SerializeField] private float pressThreshold = 0.5f;

    private Collider racketCollider;
    private bool adjusting;
    private bool rightGripWasPressed;

    private void Awake()
    {
        if (racket != null)
        {
            racketCollider = racket.GetComponent<Collider>();
        }
    }

    private void Update()
    {
        if (racket == null || leftControllerAnchor == null) return;

        bool leftTriggerHeld =
            OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch) >= pressThreshold;
        bool rightGripPressed =
            OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch) >= pressThreshold;
        bool rightGripDown = rightGripPressed && !rightGripWasPressed;
        rightGripWasPressed = rightGripPressed;

        if (!adjusting)
        {
            if (leftTriggerHeld && WithinReach())
            {
                adjusting = true;
                racket.FollowTemporarily(leftControllerAnchor);
            }
        }
        else if (rightGripDown)
        {
            adjusting = false;
            racket.AdoptCurrentPoseAsGrip();
        }
        else if (!leftTriggerHeld)
        {
            adjusting = false;
            racket.RestoreOwnGrip();
        }
    }

    private bool WithinReach()
    {
        Vector3 hand = leftControllerAnchor.position;
        Vector3 closest = racketCollider != null
            ? racketCollider.ClosestPoint(hand)
            : racket.transform.position;
        return Vector3.Distance(hand, closest) <= grabRadius;
    }
}
