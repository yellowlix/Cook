using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;

public sealed class CookingContentAssetTests
{
    private const string ControllerPath =
        "Assets/Animations/Controllers/CookingCharacterGenerated.controller";

    [Test]
    public void AnimatorControllerAsset_PersistsExpectedGraph()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Assert.That(controller, Is.Not.Null, $"Missing controller at {ControllerPath}.");
        Assert.That(controller.parameters, Has.Length.EqualTo(9));
        Assert.That(controller.layers, Has.Length.EqualTo(1));
        Assert.That(controller.layers[0].stateMachine.states, Has.Length.EqualTo(13));
        Assert.That(controller.layers[0].stateMachine.anyStateTransitions, Has.Length.EqualTo(6));
    }
}
