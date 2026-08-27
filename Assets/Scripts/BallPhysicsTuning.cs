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
    // The physics step at 1.0 Game Speed (90Hz, matching Quest 2 tracking —
    // see PlayerRacket's rationale). GameMenu scales Time.fixedDeltaTime by
    // its Game Speed so steps stay ~90 per REAL second at any dilation
    // (docs/adr/0001-game-speed-is-time-dilation.md). The old SpeedMultiplier
    // hit-velocity scale is gone — Game Speed is Time.timeScale now.
    public const float BaseFixedDeltaTime = 1f / 90f;

    private Rigidbody rb;
    private PhysicsMaterial material;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        material = GetComponent<Collider>().material;
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
