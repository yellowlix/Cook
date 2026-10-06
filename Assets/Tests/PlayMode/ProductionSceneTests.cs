using System.Collections;
using Cook;
using Cook.Core;
using Cook.Managers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class ProductionSceneTests
{
    private Keyboard keyboard;

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        keyboard = null;
        yield return null;
    }

    [UnityTest]
    public IEnumerator SpaceTripleClick_RecordsOneStepAndStationSwitchKeepsIt()
    {
        yield return SceneManager.LoadSceneAsync("GameScene");
        var controller = Object.FindFirstObjectByType<CookController>();
        Assert.That(controller, Is.Not.Null);
        controller.StartProduction();
        GameManager.Instance.Input.SetFocused(true);
        keyboard = InputSystem.AddDevice<Keyboard>();
        for (int i = 0; i < 3; i++)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }
        Assert.That(controller.Session.ActualOperations.Count, Is.EqualTo(1));
        Assert.That(controller.Session.ActualOperations[0].InteractionMode, Is.EqualTo(ProductionInteractionMode.TripleClick));
        controller.ChangeStation(1);
        Assert.That(controller.Session.ActualOperations.Count, Is.EqualTo(1));
        var animators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int visible = 0;
        foreach (Animator animator in animators) if (animator.gameObject.activeInHierarchy) visible++;
        Assert.That(visible, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator Timeout_ClearsPendingClickBeforeNextRun()
    {
        yield return SceneManager.LoadSceneAsync("GameScene");
        var controller = Object.FindFirstObjectByType<CookController>();
        controller.StartProduction();
        GameManager.Instance.Input.SetFocused(true);
        keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        controller.Session.Tick(20f);
        yield return new WaitForSeconds(0.4f);
        Assert.That(controller.Session.RunIndex, Is.EqualTo(1));
        Assert.That(controller.Session.ActualOperations, Is.Empty);
    }

    [UnityTest]
    public IEnumerator Timeout_CompletesDishOnceWithoutGradesAndRestartCreatesNewSession()
    {
        yield return SceneManager.LoadSceneAsync("GameScene");
        var controller = Object.FindFirstObjectByType<CookController>();
        controller.StartProduction();
        ProductionSession previous = controller.Session;
        int completions = 0;
        System.Action<ProductionFinished> listener = message => completions++;
        GameManager.Instance.Events.Subscribe(listener);
        previous.Tick(60f);
        previous.Tick(60f);
        Assert.That(previous.State, Is.EqualTo(ProductionState.Completed));
        Assert.That(previous.Result.Runs.Count, Is.EqualTo(3));
        Assert.That(completions, Is.EqualTo(1));
        GameManager.Instance.Events.Unsubscribe(listener);
        controller.StartProduction();
        Assert.That(controller.Session, Is.Not.SameAs(previous));
        Assert.That(controller.Session.RunIndex, Is.Zero);
        Assert.That(controller.Session.ActualOperations, Is.Empty);
        yield return null;
    }
}
