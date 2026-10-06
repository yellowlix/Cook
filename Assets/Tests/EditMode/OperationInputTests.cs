using System.Collections.Generic;
using Cook.Core;
using NUnit.Framework;

public sealed class OperationInputTests
{
    [Test]
    public void SingleClick_WaitsForWindowBeforeCompleting()
    {
        var input = new OperationInput(0.35f);
        var results = Listen(input);
        Tap(input);
        input.Tick(0.2f);
        Assert.That(results, Is.Empty);
        input.Tick(0.16f);
        Assert.That(results, Is.EqualTo(new[] { ProductionInteractionMode.Click }));
    }

    [Test]
    public void TwoClicks_ExpireWithoutCompleting()
    {
        var input = new OperationInput(0.35f);
        var results = Listen(input);
        Tap(input);
        input.Tick(0.1f);
        Tap(input);
        input.Tick(0.36f);
        Assert.That(results, Is.Empty);
        Assert.That(input.ClickCount, Is.Zero);
    }

    [Test]
    public void ThreeClicks_CompleteOnceAndReportProgress()
    {
        var input = new OperationInput(0.35f);
        var results = Listen(input);
        Tap(input);
        Assert.That(input.ClickCount, Is.EqualTo(1));
        Tap(input);
        Assert.That(input.Progress, Is.EqualTo(2f / 3f));
        Tap(input);
        input.Tick(1f);
        Assert.That(results, Is.EqualTo(new[] { ProductionInteractionMode.TripleClick }));
        Assert.That(input.ClickCount, Is.Zero);
    }

    [Test]
    public void ExpiredDoubleClick_RestartsCounting()
    {
        var input = new OperationInput(0.35f);
        var results = Listen(input);
        Tap(input);
        Tap(input);
        input.Tick(0.36f);
        Tap(input);
        Assert.That(input.ClickCount, Is.EqualTo(1));
        input.Tick(0.36f);
        Assert.That(results, Is.EqualTo(new[] { ProductionInteractionMode.Click }));
    }

    [Test]
    public void Hold_CompletesAtThreeSecondsOnlyOnce()
    {
        var input = new OperationInput(0.35f);
        var results = Listen(input);
        input.Press();
        input.Tick(1.5f);
        Assert.That(input.Progress, Is.EqualTo(0.5f));
        Assert.That(results, Is.Empty);
        input.Tick(1.5f);
        input.Tick(4f);
        input.Release();
        input.Tick(1f);
        Assert.That(results, Is.EqualTo(new[] { ProductionInteractionMode.Hold }));
    }

    [Test]
    public void EarlyHoldRelease_DoesNotBecomeClick()
    {
        var input = new OperationInput(0.35f);
        var results = Listen(input);
        input.Press();
        input.Tick(1f);
        input.Release();
        input.Tick(1f);
        Assert.That(results, Is.Empty);
    }

    [Test]
    public void Reset_DiscardsPendingClicksAndHold()
    {
        var input = new OperationInput(0.35f);
        var results = Listen(input);
        Tap(input);
        input.Reset();
        input.Tick(1f);
        input.Press();
        input.Tick(2f);
        input.Reset();
        input.Tick(3f);
        input.Release();
        Assert.That(results, Is.Empty);
        Assert.That(input.Progress, Is.Zero);
    }

    private static void Tap(OperationInput input)
    {
        input.Press();
        input.Release();
    }

    private static List<ProductionInteractionMode> Listen(OperationInput input)
    {
        var results = new List<ProductionInteractionMode>();
        input.Completed += results.Add;
        return results;
    }
}
