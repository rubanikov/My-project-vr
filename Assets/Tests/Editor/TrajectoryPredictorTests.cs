using NUnit.Framework;
using UnityEngine;

public class TrajectoryPredictorTests
{
    [Test]
    public void StraightLineNoGravity_PredictsExactCrossing()
    {
        // Ball at origin moving purely in +Z at 2 m/s, no gravity (0) —
        // should reach z = 4 after exactly 2 seconds, x/y unchanged.
        bool found = TrajectoryPredictor.TryPredictPlaneCrossing(
            Vector3.zero, new Vector3(0f, 0f, 2f), 0f, 4f,
            out Vector3 predicted, out float time);

        Assert.IsTrue(found);
        Assert.AreEqual(2f, time, 0.001f);
        Assert.AreEqual(new Vector3(0f, 0f, 4f), predicted);
    }

    [Test]
    public void MovingAwayFromPlane_ReturnsFalse()
    {
        // Moving in -Z, target plane is ahead in +Z — never gets there.
        bool found = TrajectoryPredictor.TryPredictPlaneCrossing(
            Vector3.zero, new Vector3(0f, 0f, -2f), -9.81f, 4f,
            out _, out _);

        Assert.IsFalse(found);
    }

    [Test]
    public void ParallelToPlane_ReturnsFalse()
    {
        // Zero Z velocity — will never reach a plane at a different Z.
        bool found = TrajectoryPredictor.TryPredictPlaneCrossing(
            new Vector3(0f, 1f, 0f), new Vector3(3f, 0f, 0f), -9.81f, 4f,
            out _, out _);

        Assert.IsFalse(found);
    }

    [Test]
    public void GravityPullsPredictedYDownward()
    {
        // Thrown level (vy=0) toward the plane, with gravity — predicted Y
        // at arrival must be lower than the launch height.
        bool found = TrajectoryPredictor.TryPredictPlaneCrossing(
            new Vector3(0f, 1.5f, 0f), new Vector3(0f, 0f, 4f), -9.81f, 4f,
            out Vector3 predicted, out _);

        Assert.IsTrue(found);
        Assert.Less(predicted.y, 1.5f);
    }

    [Test]
    public void XVelocity_CarriesLinearlyToPredictedPosition()
    {
        bool found = TrajectoryPredictor.TryPredictPlaneCrossing(
            Vector3.zero, new Vector3(1f, 0f, 2f), 0f, 4f,
            out Vector3 predicted, out float time);

        Assert.IsTrue(found);
        Assert.AreEqual(time * 1f, predicted.x, 0.001f);
    }

    [Test]
    public void BounceDeviation_SameDirection_IsZero()
    {
        float degrees = TrajectoryPredictor.BounceDeviationDegrees(
            new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, 1f));

        Assert.AreEqual(0f, degrees, 0.01f);
    }

    [Test]
    public void BounceDeviation_ReversedDirection_IsAround180()
    {
        float degrees = TrajectoryPredictor.BounceDeviationDegrees(
            new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, -1f));

        Assert.AreEqual(180f, degrees, 0.01f);
    }

    [Test]
    public void BounceDeviation_RightAngle_IsAround90()
    {
        float degrees = TrajectoryPredictor.BounceDeviationDegrees(
            new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 1f));

        Assert.AreEqual(90f, degrees, 0.01f);
    }

    [Test]
    public void BounceDeviation_ZeroVelocity_IsZero()
    {
        float degrees = TrajectoryPredictor.BounceDeviationDegrees(Vector3.zero, new Vector3(0f, 0f, 1f));

        Assert.AreEqual(0f, degrees, 0.01f);
    }
}
