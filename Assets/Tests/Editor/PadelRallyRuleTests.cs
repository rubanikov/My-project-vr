using NUnit.Framework;

// EditMode tests for the padel rally rules (replaces BounceFaultRuleTests —
// the padel conversion superseded that rule entirely).
public class PadelRallyRuleTests
{
    private PadelRallyRule rule;

    [SetUp]
    public void SetUp() => rule = new PadelRallyRule();

    [Test]
    public void PreServeBouncesAreIgnored()
    {
        Assert.IsNull(rule.RegisterFloorBounce(Side.Player));
        Assert.IsNull(rule.RegisterFloorBounce(Side.AI));
        Assert.AreEqual(0, rule.FloorBounceCount);
    }

    [Test]
    public void LegalRallyProducesNoFault()
    {
        rule.RegisterHit(Side.Player);
        Assert.IsNull(rule.RegisterFloorBounce(Side.AI)); // cleared the net, one bounce
        rule.RegisterHit(Side.AI);                        // returned in time
        Assert.IsNull(rule.RegisterFloorBounce(Side.Player));
        rule.RegisterHit(Side.Player);
        Assert.IsTrue(rule.RallyLive);
    }

    [Test]
    public void SecondBounceIsPointToHitter()
    {
        rule.RegisterHit(Side.Player);
        Assert.IsNull(rule.RegisterFloorBounce(Side.AI));
        FaultResult? fault = rule.RegisterFloorBounce(Side.AI);
        Assert.IsNotNull(fault);
        Assert.AreEqual(Side.Player, fault.Value.PointTo);
        Assert.AreEqual(FaultKind.DoubleBounce, fault.Value.Kind);
    }

    [Test]
    public void SecondBounceAnywhereCountsEvenBackOnHittersHalf()
    {
        // Legal first bounce on the AI half, wall ricochet carries it back —
        // the second bounce is still the receiver's failure wherever it lands.
        rule.RegisterHit(Side.Player);
        Assert.IsNull(rule.RegisterFloorBounce(Side.AI));
        FaultResult? fault = rule.RegisterFloorBounce(Side.Player);
        Assert.IsNotNull(fault);
        Assert.AreEqual(Side.Player, fault.Value.PointTo);
        Assert.AreEqual(FaultKind.DoubleBounce, fault.Value.Kind);
    }

    [Test]
    public void ServeFailingToClearIsALetNotAPoint()
    {
        rule.RegisterHit(Side.Player); // the serve — first hit of the rally
        FaultResult? fault = rule.RegisterFloorBounce(Side.Player);
        Assert.IsNotNull(fault);
        Assert.AreEqual(FaultKind.ServeLet, fault.Value.Kind);
        Assert.AreEqual(Side.Player, fault.Value.PointTo); // side that serves again
    }

    [Test]
    public void AiServeFailingToClearIsALetToo()
    {
        rule.RegisterHit(Side.AI);
        FaultResult? fault = rule.RegisterFloorBounce(Side.AI);
        Assert.IsNotNull(fault);
        Assert.AreEqual(FaultKind.ServeLet, fault.Value.Kind);
        Assert.AreEqual(Side.AI, fault.Value.PointTo);
    }

    [Test]
    public void MidRallyFailedClearStillCostsThePoint()
    {
        rule.RegisterHit(Side.Player);              // serve
        Assert.IsNull(rule.RegisterFloorBounce(Side.AI)); // legal
        rule.RegisterHit(Side.AI);                  // return
        Assert.IsNull(rule.RegisterFloorBounce(Side.Player));
        rule.RegisterHit(Side.Player);              // third hit — not a serve
        FaultResult? fault = rule.RegisterFloorBounce(Side.Player); // into the net
        Assert.IsNotNull(fault);
        Assert.AreEqual(FaultKind.FailedClear, fault.Value.Kind);
        Assert.AreEqual(Side.AI, fault.Value.PointTo);
    }

    [Test]
    public void ServeDoubleBounceOnReceiverSideStillCountsNormally()
    {
        rule.RegisterHit(Side.Player);              // serve
        Assert.IsNull(rule.RegisterFloorBounce(Side.AI)); // cleared the net
        FaultResult? fault = rule.RegisterFloorBounce(Side.AI); // unreturned
        Assert.IsNotNull(fault);
        Assert.AreEqual(FaultKind.DoubleBounce, fault.Value.Kind);
        Assert.AreEqual(Side.Player, fault.Value.PointTo);
    }

    [Test]
    public void BodyHitIsAlwaysPointToOpponentOfBody()
    {
        rule.RegisterHit(Side.AI); // even after the AI's own shot
        FaultResult? fault = rule.RegisterBodyHit(Side.AI);
        Assert.IsNotNull(fault);
        Assert.AreEqual(Side.Player, fault.Value.PointTo);
        Assert.AreEqual(FaultKind.BodyHit, fault.Value.Kind);
    }

    [Test]
    public void BodyHitOnDeadBallIsIgnored()
    {
        Assert.IsNull(rule.RegisterBodyHit(Side.AI));
    }

    [Test]
    public void RallyIsDeadAfterFaultUntilNextHit()
    {
        rule.RegisterHit(Side.Player);
        rule.RegisterFloorBounce(Side.AI);
        rule.RegisterFloorBounce(Side.AI); // DoubleBounce fault
        Assert.IsFalse(rule.RallyLive);
        Assert.IsNull(rule.RegisterFloorBounce(Side.AI));
        Assert.IsNull(rule.RegisterBodyHit(Side.AI));

        rule.RegisterHit(Side.AI); // next serve revives
        Assert.IsTrue(rule.RallyLive);
        Assert.IsNull(rule.RegisterFloorBounce(Side.Player));
    }

    [Test]
    public void HitResetsBounceCount()
    {
        rule.RegisterHit(Side.Player);
        rule.RegisterFloorBounce(Side.AI);
        rule.RegisterHit(Side.AI);
        Assert.AreEqual(0, rule.FloorBounceCount);
    }

    [Test]
    public void ResetRallyClearsEverything()
    {
        rule.RegisterHit(Side.Player);
        rule.RegisterFloorBounce(Side.AI);
        rule.ResetRally();
        Assert.IsNull(rule.LastTouch);
        Assert.AreEqual(0, rule.FloorBounceCount);
        Assert.IsFalse(rule.RallyLive);
        Assert.IsNull(rule.RegisterFloorBounce(Side.AI));
    }
}
