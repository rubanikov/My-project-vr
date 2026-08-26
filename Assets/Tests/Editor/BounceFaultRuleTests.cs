using NUnit.Framework;

public class BounceFaultRuleTests
{
    [Test]
    public void NoBounces_NoFaultOnCatch()
    {
        var rule = new BounceFaultRule();

        bool isFault = rule.RegisterCatch();

        Assert.IsFalse(isFault);
    }

    [Test]
    public void OneBounce_NoFaultOnCatch()
    {
        var rule = new BounceFaultRule();

        rule.RegisterBounce();
        bool isFault = rule.RegisterCatch();

        Assert.IsFalse(isFault);
    }

    [Test]
    public void TwoBounces_FaultOnCatch()
    {
        var rule = new BounceFaultRule();

        rule.RegisterBounce();
        rule.RegisterBounce();
        bool isFault = rule.RegisterCatch();

        Assert.IsTrue(isFault);
    }

    [Test]
    public void ManyBounces_StillJustOneFault()
    {
        var rule = new BounceFaultRule();

        for (int i = 0; i < 10; i++) rule.RegisterBounce();
        bool isFault = rule.RegisterCatch();

        Assert.IsTrue(isFault);
    }

    [Test]
    public void CatchResetsCounterForNextCycle()
    {
        var rule = new BounceFaultRule();
        rule.RegisterBounce();
        rule.RegisterBounce();
        rule.RegisterCatch(); // fault, counter should reset

        bool secondCatchIsFault = rule.RegisterCatch(); // no bounces since first catch

        Assert.IsFalse(secondCatchIsFault);
    }

    [Test]
    public void FloorAndWallBouncesCountIdentically()
    {
        // BounceFaultRule has no concept of surface type at all — RegisterBounce()
        // takes no arguments, so a floor bounce and a wall bounce are
        // indistinguishable by construction. Two bounces from any mix of
        // surfaces still faults.
        var rule = new BounceFaultRule();

        rule.RegisterBounce(); // e.g. wall
        rule.RegisterBounce(); // e.g. floor
        bool isFault = rule.RegisterCatch();

        Assert.IsTrue(isFault);
    }

    [Test]
    public void FirstThrowOfRally_NoServeExemption()
    {
        // A fresh rule (equivalent to the very first throw of a rally) applies
        // the same limit as any other cycle — no special-cased serve behavior.
        var rule = new BounceFaultRule();

        rule.RegisterBounce();
        rule.RegisterBounce();

        Assert.IsTrue(rule.RegisterCatch());
    }

    [Test]
    public void BounceCount_ReflectsRegisteredBounces()
    {
        var rule = new BounceFaultRule();

        rule.RegisterBounce();
        rule.RegisterBounce();
        rule.RegisterBounce();

        Assert.AreEqual(3, rule.BounceCount);
    }
}
