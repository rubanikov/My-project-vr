using System;
using UnityEngine;

// Wires the ball's physics contacts and racket/body events into the pure
// PadelRallyRule (see that file and CONTEXT.md for the resolved padel rules).
// Only the FLOOR registers as a bounce — walls, net, ceiling, and rackets are
// free contacts, so this handler simply ignores every collider except the
// runtime-built "CourtFloor". Racket hits arrive via NotifyTouched (the
// player's collision handler and the AI's shot logic both call it), body
// contacts via NotifyBodyHit (the AI's trigger capsule).
[RequireComponent(typeof(Rigidbody))]
public class BallFaultTracker : MonoBehaviour
{
    private const string FloorName = "CourtFloor";

    [Tooltip("Source of the net's Z position — the halves boundary. Falls back to z=0 when unwired.")]
    [SerializeField] private CourtBuilder courtBuilder;

    [Header("Dead ball (flat hops merge contacts, so a second bounce may never fire)")]
    [SerializeField] private float deadBallSeconds = 1f;
    [Tooltip("A ball pinned below this height after its first bounce is unreturnable at any speed.")]
    [SerializeField] private float deadBallMaxHeight = 0.35f;

    private readonly PadelRallyRule rule = new PadelRallyRule();
    private float deadTime;

    // Fires with the side that WINS the point and why.
    public event Action<Side, FaultKind> Fault;

    public Side? LastTouch => rule.LastTouch;
    public int CurrentBounceCount => rule.FloorBounceCount;
    public bool RallyLive => rule.RallyLive;

    // The dead-ball watchdog: after one legal bounce, a ball that stays low
    // never yields a clean second OnCollisionEnter — rolling keeps floor
    // contact, and fast flat skims (1-3 m/s) re-contact before Unity counts a
    // separation (2026-08-27 playtest, widened same day: the original version
    // also required low SPEED, leaving skims in a blind window). A ball pinned
    // under bounce height for a continuous second is the second Bounce, at
    // any speed — the receiver never returned it.
    private void Update()
    {
        bool dying = rule.RallyLive
            && rule.FloorBounceCount >= 1
            && transform.position.y < deadBallMaxHeight;

        if (!dying)
        {
            deadTime = 0f;
            return;
        }

        deadTime += Time.deltaTime;
        if (deadTime < deadBallSeconds) return;

        deadTime = 0f;
        Resolve(rule.RegisterDeadBall());
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.name != FloorName) return;

        // The player's half is everything on the player's side of the net
        // (the net sits at the front edge of the Guardian area, not z=0).
        float netZ = courtBuilder != null ? courtBuilder.NetZ : 0f;
        Side bounceHalf = transform.position.z >= netZ ? Side.Player : Side.AI;
        Resolve(rule.RegisterFloorBounce(bounceHalf));
    }

    public void NotifyTouched(Side touchedBy) => rule.RegisterHit(touchedBy);

    public void NotifyBodyHit(Side bodySide) => Resolve(rule.RegisterBodyHit(bodySide));

    public void ResetRally() => rule.ResetRally();

    private void Resolve(FaultResult? result)
    {
        if (result == null) return;
        Fault?.Invoke(result.Value.PointTo, result.Value.Kind);
    }
}
