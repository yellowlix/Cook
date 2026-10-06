using System;
using Cook.Core;
using NUnit.Framework;

public sealed class ProductionSessionTests
{
    [Test]
    public void ThreeActualOperations_AdvanceEvenWhenGuideOnlyHasOne()
    {
        var session = new ProductionSession(Dish(3));
        session.Tick(2f);
        for (int i = 0; i < 3; i++)
        {
            Assert.That(session.TryRecordOperation(OtherOperation()), Is.True);
        }

        Assert.That(session.RunIndex, Is.EqualTo(1));
        Assert.That(session.ActualOperations, Is.Empty);
        Assert.That(session.RemainingTime, Is.EqualTo(20f));
        Assert.That(session.RunResults[0].ActualOperations.Count, Is.EqualTo(3));
        Assert.That(session.RunResults[0].ActualOperations[0].Station,
            Is.EqualTo(ProductionStation.FryingPan));
        Assert.That(session.RunResults[0].ElapsedTime, Is.EqualTo(2f));
        Assert.That(session.RunResults[0].EndReason, Is.EqualTo(ProductionRunEndReason.ThreeOperations));
    }

    [Test]
    public void ZeroOperations_TimeoutAndPreserveUnusedFrameTime()
    {
        var session = new ProductionSession(Dish(3));
        session.Tick(25f);

        Assert.That(session.RunIndex, Is.EqualTo(1));
        Assert.That(session.RunElapsedTime, Is.EqualTo(5f));
        Assert.That(session.RunResults[0].ActualOperations, Is.Empty);
        Assert.That(session.RunResults[0].EndReason, Is.EqualTo(ProductionRunEndReason.TimeExpired));
    }

    [Test]
    public void FewerThanThreeOperations_RemainUntilTimeout()
    {
        var session = new ProductionSession(Dish(3));
        session.TryRecordOperation(OtherOperation());
        session.TryRecordOperation(OtherOperation());
        session.Tick(19f);
        Assert.That(session.RunIndex, Is.Zero);
        session.Tick(1f);

        Assert.That(session.RunIndex, Is.EqualTo(1));
        Assert.That(session.RunResults[0].ActualOperations.Count, Is.EqualTo(2));
    }

    [Test]
    public void TripleClick_IsOneCompletedOperation()
    {
        var session = new ProductionSession(Dish(3));
        session.TryRecordOperation(new ProductionOperationData(
            ProductionStation.CuttingBoard, ProductionInteractionMode.TripleClick));

        Assert.That(session.ActualOperations.Count, Is.EqualTo(1));
        Assert.That(session.RunIndex, Is.Zero);
    }

    [Test]
    public void FinalRun_CompletesDishOnceAndRejectsFurtherOperations()
    {
        var session = new ProductionSession(Dish(3));
        int completions = 0;
        session.Completed += result =>
        {
            completions++;
            Assert.That(session.State, Is.EqualTo(ProductionState.Completed));
            Assert.That(result, Is.SameAs(session.Result));
        };
        for (int run = 0; run < 3; run++)
        {
            session.Tick(1f);
            for (int operation = 0; operation < 3; operation++)
            {
                session.TryRecordOperation(OtherOperation());
            }
        }

        session.Tick(60f);
        Assert.That(session.TryRecordOperation(OtherOperation()), Is.False);
        Assert.That(completions, Is.EqualTo(1));
        Assert.That(session.Result.Runs.Count, Is.EqualTo(3));
        Assert.That(session.Result.TotalElapsedTime, Is.EqualTo(3f));
        Assert.That(session.RemainingRunCount, Is.Zero);
    }

    [Test]
    public void SevenRuns_FinishAtSixtySecondsWithoutRoundingDelay()
    {
        var session = new ProductionSession(Dish(7));
        session.Tick(60f);

        Assert.That(session.State, Is.EqualTo(ProductionState.Completed));
        Assert.That(session.Result.Runs.Count, Is.EqualTo(7));
        Assert.That(session.Result.TotalElapsedTime, Is.EqualTo(60f).Within(0.0001f));
    }

    [Test]
    public void Cancel_DiscardsEntireUnfinishedDishProgress()
    {
        var session = new ProductionSession(Dish(3));
        session.Tick(22f);
        session.TryRecordOperation(OtherOperation());
        int completions = 0;
        session.Completed += _ => completions++;

        Assert.That(session.Cancel(), Is.True);
        session.Tick(60f);
        Assert.That(session.TryRecordOperation(OtherOperation()), Is.False);
        Assert.That(session.Cancel(), Is.False);
        Assert.That(session.State, Is.EqualTo(ProductionState.Cancelled));
        Assert.That(session.RunResults, Is.Empty);
        Assert.That(session.ActualOperations, Is.Empty);
        Assert.That(session.TotalElapsedTime, Is.Zero);
        Assert.That(session.Result, Is.Null);
        Assert.That(completions, Is.Zero);
    }

    [Test]
    public void CompletedRunRecords_AreReadOnly()
    {
        var session = new ProductionSession(Dish(3));
        session.TryRecordOperation(OtherOperation());
        session.Tick(20f);
        var actual = (System.Collections.Generic.IList<ProductionOperationData>)
            session.RunResults[0].ActualOperations;

        Assert.Throws<NotSupportedException>(() => actual.Add(OtherOperation()));
    }

    [Test]
    public void InvalidDeltaTime_IsRejectedWithoutChangingState()
    {
        var session = new ProductionSession(Dish(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Tick(-1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Tick(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Tick(float.PositiveInfinity));
        Assert.That(session.TotalElapsedTime, Is.Zero);
    }

    private static DishRuntimeData Dish(int runCount)
    {
        var runs = new ProductionRunData[runCount];
        for (int i = 0; i < runCount; i++)
        {
            runs[i] = new ProductionRunData(new[]
            {
                new ProductionOperationData(ProductionStation.SoupPot, ProductionInteractionMode.Click)
            });
        }

        return new DishRuntimeData("test-dish", "Test dish", 1, runs);
    }

    private static ProductionOperationData OtherOperation() =>
        new ProductionOperationData(ProductionStation.FryingPan, ProductionInteractionMode.Hold);
}
