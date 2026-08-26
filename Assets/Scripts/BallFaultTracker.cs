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

    private readonly PadelRallyRule rule = new PadelRallyRule();

    // Fires with the side that WINS the point and why.
    public event Action<Side, FaultKind> Fault;

    public Side? LastTouch => rule.LastTouch;
    public int CurrentBounceCount => rule.FloorBounceCount;
    public bool RallyLive => rule.RallyLive;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.name != FloorName) return;

        // The net sits at z=0: positive z is the player's half.
        Side bounceHalf = transform.position.z >= 0f ? Side.Player : Side.AI;
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
