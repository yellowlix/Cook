using System.Collections;
using Cook.Core;
using Cook.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CoreCookingSession = Cook.Core.CookingSession;

public sealed class CookingPresentationTests
{
    [UnityTest]
    public IEnumerator StationChanged_ActivatesExactlyOnePresenter()
    {
        GameObject owner = new GameObject("Presenter");
        GameObject soup = new GameObject("Soup");
        GameObject board = new GameObject("Board");
        GameObject pan = new GameObject("Pan");
        var presenter = owner.AddComponent<CookingAnimationPresenter>();
        presenter.ConfigureStations(new[]
        {
            new StationPresentation { station = CookingStation.SoupPot, root = soup },
            new StationPresentation { station = CookingStation.CuttingBoard, root = board },
            new StationPresentation { station = CookingStation.FryingPan, root = pan }
        });

        var session = new CoreCookingSession();
        presenter.Bind(session);
        session.StartRecipe(CreateRecipe(), CookingStation.SoupPot);
        yield return null;

        Assert.That(soup.activeSelf, Is.True);
        Assert.That(board.activeSelf, Is.False);
        Assert.That(pan.activeSelf, Is.False);

        session.MoveStation(1);
        yield return null;

        Assert.That(soup.activeSelf, Is.False);
        Assert.That(board.activeSelf, Is.True);
        Assert.That(pan.activeSelf, Is.False);

        presenter.Unbind();
        Object.Destroy(owner);
        Object.Destroy(soup);
        Object.Destroy(board);
        Object.Destroy(pan);
    }

    [UnityTest]
    public IEnumerator OneShotCompletion_AllowsNextInputBeforeAnimatorFinishes()
    {
        var session = new CoreCookingSession();
        session.StartRecipe(CreateRecipe(), CookingStation.SoupPot);

        session.PressCook();
        session.PressCook();
        yield return null;

        Assert.That(session.CurrentOperationIndex, Is.EqualTo(2));
        Assert.That(session.State, Is.EqualTo(CookingSessionState.RoundResult));
    }

    private static RecipeRuntimeData CreateRecipe()
    {
        var first = new OperationRuntimeData(
            "stir-1", CookingOperationType.StirOnce, CookingStation.SoupPot,
            CookingInputMode.ShortPress, 1f, 0.5f, CookingAnimationMode.OneShot,
            "StirOnce", string.Empty);
        var second = new OperationRuntimeData(
            "stir-2", CookingOperationType.StirOnce, CookingStation.SoupPot,
            CookingInputMode.ShortPress, 1f, 0.5f, CookingAnimationMode.OneShot,
            "StirOnce", string.Empty);
        return new RecipeRuntimeData(
            "test", "Test", new[] { new RoundRuntimeData(new[] { first, second }, 1f, 2f, 3f) });
    }
}
