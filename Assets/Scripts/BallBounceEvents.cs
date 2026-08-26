using System;
using UnityEngine;

// Exposes the ball's velocity just before/after each physics collision, for
// the AI opponent's reaction-delay scaling (a sharper, more unexpected carom
// should cost more reaction time — see AIOpponent.cs and wayfinder ticket 03).
//
// Velocity "before" is cached at the start of each FixedUpdate, i.e. before
// this physics step resolves any collision; "after" is read inside
// OnCollisionEnter, by which point Unity has already applied the collision
// response for this step.
[RequireComponent(typeof(Rigidbody))]
public class BallBounceEvents : MonoBehaviour
{
    private Rigidbody rb;
    private Vector3 velocityAtStepStart;

    public event Action<Vector3, Vector3> Bounced; // (velocityBefore, velocityAfter)

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        velocityAtStepStart = rb.linearVelocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        Bounced?.Invoke(velocityAtStepStart, rb.linearVelocity);
    }
}
