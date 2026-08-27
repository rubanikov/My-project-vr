using UnityEngine;
using Meta.XR.ImmersiveDebugger;

// Exposes the ball's bounce-feel physics parameters as live-tweakable
// sliders in Meta's Immersive Debugger panel, so they can be dialed in while
// actually wearing the headset instead of guessing at numbers between
// rebuilds — added 2026-08-26, user request: "is there a way that we can
// move around parameters to find the best feelings and physics for the
// ball?"
//
// Uses Collider.material (not sharedMaterial) so tweaks only affect a
// runtime-instanced copy of BallPhysicMaterial.physicMaterial, never the
// shared asset on disk — safe to experiment with on-device, values reset
// next launch. Once good numbers are found, bake them into the real asset
// and Rigidbody defaults in the Editor.
//
// Toggle the debugger panel in-headset with the button set in
// ImmersiveDebuggerSettings (Meta > Immersive Debugger in the Editor).
[RequireComponent(typeof(Rigidbody))]
public class BallPhysicsTuning : MonoBehaviour
{
    // Global ball-speed scale (2026-08-27 user request: "slider for the
    // speed of the ball"). Consumed by PlayerRacket (scales hit exit speed)
    // and AIOpponent (stretches shot flight time, so aim stays true at any
    // speed). Static so the hit paths read it without wiring; resets to 1
    // each launch like every other tuning value here.
    public static float SpeedMultiplier = 1f;

    private Rigidbody rb;
    private PhysicsMaterial material;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        material = GetComponent<Collider>().material;
        SpeedMultiplier = 1f;
    }

    [DebugMember(Category = "Ball Physics", Tweakable = true, Min = 0.4f, Max = 1.5f, DisplayName = "Ball Speed")]
    public float BallSpeed
    {
        get => SpeedMultiplier;
        set => SpeedMultiplier = value;
    }

    [DebugMember(Category = "Ball Physics", Tweakable = true, Min = 0f, Max = 1.3f, DisplayName = "Bounciness")]
    public float Bounciness
    {
        get => material.bounciness;
        set => material.bounciness = value;
    }

    [DebugMember(Category = "Ball Physics", Tweakable = true, Min = 0f, Max = 1f, DisplayName = "Dynamic Friction")]
    public float DynamicFriction
    {
        get => material.dynamicFriction;
        set => material.dynamicFriction = value;
    }

    [DebugMember(Category = "Ball Physics", Tweakable = true, Min = 0f, Max = 1f, DisplayName = "Static Friction")]
    public float StaticFriction
    {
        get => material.staticFriction;
        set => material.staticFriction = value;
    }

    [DebugMember(Category = "Ball Physics", Tweakable = true, Min = 0.05f, Max = 1f, DisplayName = "Mass (kg)")]
    public float Mass
    {
        get => rb.mass;
        set => rb.mass = value;
    }

    [DebugMember(Category = "Ball Physics", Tweakable = true, Min = 0f, Max = 2f, DisplayName = "Linear Drag")]
    public float LinearDrag
    {
        get => rb.linearDamping;
        set => rb.linearDamping = value;
    }

    [DebugMember(Category = "Ball Physics", Tweakable = true, Min = 0f, Max = 2f, DisplayName = "Angular Drag")]
    public float AngularDrag
    {
        get => rb.angularDamping;
        set => rb.angularDamping = value;
    }

    [DebugMember(Category = "Ball Physics", DisplayName = "Log Current Values")]
    public void LogCurrentValues()
    {
        Debug.Log($"[BallPhysicsTuning] bounciness={material.bounciness:F2}, " +
            $"dynamicFriction={material.dynamicFriction:F2}, staticFriction={material.staticFriction:F2}, " +
            $"mass={rb.mass:F2}, linearDamping={rb.linearDamping:F2}, angularDamping={rb.angularDamping:F2}");
    }
}
