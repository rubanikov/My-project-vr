using System;
using UnityEngine;

// Wires Court Clash's ball-physics collisions and touch/grab events into the
// pure BounceFaultRule logic (see BounceFaultRule.cs for the resolved rule).
// Anything that represents "a side touched/caught the ball" — the player's
// grab interactor, the AI opponent's catch logic — should call
// NotifyTouched(side), passing which side is doing the touching.
[RequireComponent(typeof(Rigidbody))]
public class BallFaultTracker : MonoBehaviour
{
    private readonly BounceFaultRule rule = new BounceFaultRule();

    // Fires with the side that WINS the point — i.e. the side opposite
    // whoever just touched the ball too late (per ticket 01/02: a bounce-limit
    // violation "awards the other side a point").
    public event Action<Side> Fault;

    public int CurrentBounceCount => rule.BounceCount;

    // A hit from the player's racket is a touch, not a bounce — PlayerRacket
    // calls NotifyTouched for its own collision with the ball independently,
    // so skip counting it again here as a wall/floor-style bounce.
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.TryGetComponent(out PlayerRacket _)) return;
        rule.RegisterBounce();
    }

    public void NotifyTouched(Side touchedBy)
    {
        if (rule.RegisterCatch())
        {
            Side pointGoesTo = touchedBy == Side.Player ? Side.AI : Side.Player;
            Fault?.Invoke(pointGoesTo);
        }
    }
}
