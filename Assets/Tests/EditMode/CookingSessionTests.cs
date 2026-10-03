using System.Collections.Generic;
using Cook.Core;
using NUnit.Framework;
using CoreCookingSession = Cook.Core.CookingSession;

public sealed class CookingSessionTests
{
    [Test]
    public void ShortPress_CompletesAndStartsNextOperationImmediately()
    {
        CoreCookingSession session = StartSession(Round(
            Short("stir", CookingOperationType.StirOnce, CookingStation.SoupPot),
            Short("stir-2", CookingOperationType.StirOnce, CookingStation.SoupPot)));
        int completed = 0;
        session.OperationCompleted += _ => completed++;

        session.PressCook();
        Assert.That(session.CurrentOperationIndex, Is.EqualTo(1));

        session.PressCook();

        Assert.That(completed, Is.EqualTo(2));
        Assert.That(session.State, Is.EqualTo(CookingSessionState.RoundResult));
    }

    [Test]
    public void Operations_MustBeCompletedInAuthoredOrder()
    {
        CoreCookingSession session = StartSession(Round(
            Short("stir", CookingOperationType.StirOnce, CookingStation.SoupPot),
            Short("cut", CookingOperationType.ChopOnce, CookingStation.CuttingBoard)));
        session.PressCook();

        session.PressCook();

        Assert.That(session.CurrentOperationIndex, Is.EqualTo(1));
        Assert.That(session.CurrentOperationProgress01, Is.Zero);
    }

    [Test]
    public void WrongStation_PublishesInvalidInputWithoutProgress()
    {
        CoreCookingSession session = StartSession(Round(
            Short("cut", CookingOperationType.ChopOnce, CookingStation.CuttingBoard),
            Short("cut-2", CookingOperationType.ChopOnce, CookingStation.CuttingBoard)));
        int invalidCount = 0;
        session.InvalidInput += () => invalidCount++;

        session.PressCook();

        Assert.That(invalidCount, Is.EqualTo(1));
        Assert.That(session.CurrentOperationProgress01, Is.Zero);
    }

    [Test]
    public void RepeatedPress_AddsOneUnitPerPress()
    {
        CoreCookingSession session = StartSession(Round(
            Repeated("cut-loop", 3f),
            Short("cut", CookingOperationType.ChopOnce, CookingStation.CuttingBoard)),
            CookingStation.CuttingBoard);

        session.PressCook();
        session.PressCook();

        Assert.That(session.CurrentOperationProgress01, Is.EqualTo(2f / 3f).Within(0.001f));
        Assert.That(session.CurrentOperationIndex, Is.Zero);
    }

    [Test]
    public void Hold_AddsDeltaTimeOnlyWhileHeldAtCorrectStation()
    {
        CoreCookingSession session = StartSession(Round(
            Hold("stir-hold", CookingOperationType.StirHold, CookingStation.SoupPot, 2f),
            Short("stir", CookingOperationType.StirOnce, CookingStation.SoupPot)));

        session.PressCook();
        session.Tick(0.5f);
        session.ReleaseCook();
        session.Tick(0.5f);

        Assert.That(session.CurrentOperationProgress01, Is.EqualTo(0.25f).Within(0.001f));
    }

    [Test]
    public void HoldProgress_IsPreservedAfterReleaseAndStationChange()
    {
        CoreCookingSession session = StartSession(Round(
            Hold("stir-hold", CookingOperationType.StirHold, CookingStation.SoupPot, 2f),
            Short("stir", CookingOperationType.StirOnce, CookingStation.SoupPot)));

        session.PressCook();
        session.Tick(0.5f);
        session.ReleaseCook();
        session.MoveStation(1);
        session.Tick(0.5f);
        session.MoveStation(-1);
        session.PressCook();
        session.Tick(0.5f);

        Assert.That(session.CurrentOperationProgress01, Is.EqualTo(0.5f).Within(0.001f));
    }

    [TestCase(0.84f, CookingGrade.Excellent)]
    [TestCase(0.95f, CookingGrade.Great)]
    [TestCase(1.20f, CookingGrade.Good)]
    public void CompletedRound_UsesFourGradeThresholds(float elapsed, CookingGrade expected)
    {
        CoreCookingSession session = StartSession(RoundWithThresholds(
            0.85f, 1f, 1.4f,
            Short("stir", CookingOperationType.StirOnce, CookingStation.SoupPot),
            Short("stir-2", CookingOperationType.StirOnce, CookingStation.SoupPot)));
        CookingGrade? grade = null;
        session.RoundEvaluated += value => grade = value;

        session.Tick(elapsed);
        session.PressCook();
        session.PressCook();

        Assert.That(grade, Is.EqualTo(expected));
    }

    [Test]
    public void GoodTimeout_ProducesMissAndNoRecipeProgress()
    {
        CoreCookingSession session = StartSession(RoundWithThresholds(
            0.85f, 1f, 1.4f,
            Hold("stir-hold", CookingOperationType.StirHold, CookingStation.SoupPot, 10f),
            Short("stir", CookingOperationType.StirOnce, CookingStation.SoupPot)));
        CookingGrade? grade = null;
        session.RoundEvaluated += value => grade = value;

        session.Tick(1.41f);

        Assert.That(grade, Is.EqualTo(CookingGrade.Miss));
        Assert.That(session.RecipeProgress01, Is.Zero);
    }

    [Test]
    public void ExcellentRounds_CanReachOneHundredPercentEarly()
    {
        RoundRuntimeData quickRound = Round(
            Short("stir", CookingOperationType.StirOnce, CookingStation.SoupPot),
            Short("stir-2", CookingOperationType.StirOnce, CookingStation.SoupPot));
        CoreCookingSession session = StartSession(new RecipeRuntimeData(
            "three-rounds", "Three Rounds", new[] { quickRound, quickRound, quickRound }));

        CompleteTwoShortPresses(session);
        session.ContinueAfterRoundResult();
        CompleteTwoShortPresses(session);
        session.ContinueAfterRoundResult();

        Assert.That(session.RecipeProgress01, Is.EqualTo(1f));
        Assert.That(session.State, Is.EqualTo(CookingSessionState.RecipeSuccess));
        Assert.That(session.CurrentRoundIndex, Is.EqualTo(1));
    }

    [Test]
    public void ExhaustedRoundsBelowTarget_ProducesRecipeFailure()
    {
        RoundRuntimeData timeoutRound = RoundWithThresholds(
            0.5f, 0.75f, 1f,
            Hold("stir-hold", CookingOperationType.StirHold, CookingStation.SoupPot, 10f),
            Short("stir", CookingOperationType.StirOnce, CookingStation.SoupPot));
        CoreCookingSession session = StartSession(new RecipeRuntimeData(
            "two-rounds", "Two Rounds", new[] { timeoutRound, timeoutRound }));

        session.Tick(1.01f);
        session.ContinueAfterRoundResult();
        session.Tick(1.01f);
        session.ContinueAfterRoundResult();

        Assert.That(session.State, Is.EqualTo(CookingSessionState.RecipeFailure));
        Assert.That(session.RecipeProgress01, Is.Zero);
    }

    private static CoreCookingSession StartSession(RoundRuntimeData round, CookingStation station = CookingStation.SoupPot)
    {
        return StartSession(new RecipeRuntimeData("recipe", "Recipe", new[] { round }), station);
    }

    private static CoreCookingSession StartSession(RecipeRuntimeData recipe, CookingStation station = CookingStation.SoupPot)
    {
        CoreCookingSession session = new CoreCookingSession();
        session.StartRecipe(recipe, station);
        return session;
    }

    private static void CompleteTwoShortPresses(CoreCookingSession session)
    {
        session.PressCook();
        session.PressCook();
    }

    private static RoundRuntimeData Round(params OperationRuntimeData[] operations)
    {
        return RoundWithThresholds(5f, 7f, 10f, operations);
    }

    private static RoundRuntimeData RoundWithThresholds(
        float excellent,
        float great,
        float good,
        params OperationRuntimeData[] operations)
    {
        return new RoundRuntimeData(operations, excellent, great, good);
    }

    private static OperationRuntimeData Short(string id, CookingOperationType type, CookingStation station)
    {
        return new OperationRuntimeData(id, type, station, CookingInputMode.ShortPress, 1f, 0.5f,
            CookingAnimationMode.OneShot, id, string.Empty);
    }

    private static OperationRuntimeData Repeated(string id, float required)
    {
        return new OperationRuntimeData(id, CookingOperationType.ChopRepeated, CookingStation.CuttingBoard,
            CookingInputMode.RepeatedPress, required, 2f, CookingAnimationMode.Sustained, "repeat-start", "repeat-stop");
    }

    private static OperationRuntimeData Hold(
        string id,
        CookingOperationType type,
        CookingStation station,
        float required)
    {
        return new OperationRuntimeData(id, type, station, CookingInputMode.Hold, required, 2f,
            CookingAnimationMode.Sustained, "hold-start", "hold-stop");
    }
}
