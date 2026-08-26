// Pure logic for Court Clash's "no more than one bounce" fault rule, decoupled
// from MonoBehaviour/collision lifecycle so it's directly unit-testable.
// Resolved semantics (wayfinder ticket 01 — .scratch/court-clash/issues/01-fault-bounce-rule-semantics.md):
//   - Counter resets on every catch (per-throw scoped, not a running rally total).
//   - Floor and wall bounces count identically — no special-casing by surface.
//   - No serve exemption — applies uniformly from the very first throw.
public class BounceFaultRule
{
    public const int BounceLimit = 1;

    public int BounceCount { get; private set; }

    public void RegisterBounce()
    {
        BounceCount++;
    }

    // Call when a side touches/catches the ball. Returns true if the ball
    // had already exceeded the bounce limit since it was last touched (a
    // fault), then resets the counter for the new "since last touch" cycle.
    public bool RegisterCatch()
    {
        bool isFault = BounceCount > BounceLimit;
        BounceCount = 0;
        return isFault;
    }
}
