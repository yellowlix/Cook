using Cook.Configuration;
using Cook.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ConfigOperationDefinition = Cook.Configuration.OperationDefinition;
using ConfigRecipeDefinition = Cook.Configuration.RecipeDefinition;

public sealed class CookingDefinitionTests
{
    [Test]
    public void OperationDefinition_ConvertsAllGameplayAndAnimationFields()
    {
        ConfigOperationDefinition definition = CreateOperation(
            "pan-hold", CookingOperationType.PanFlipHold, CookingStation.FryingPan,
            CookingInputMode.Hold, 2f, 2.25f, CookingAnimationMode.Sustained,
            "PanFlipHoldStart", "PanFlipHoldStop");

        OperationRuntimeData runtime = definition.ToRuntime();

        Assert.That(runtime.Id, Is.EqualTo("pan-hold"));
        Assert.That(runtime.Type, Is.EqualTo(CookingOperationType.PanFlipHold));
        Assert.That(runtime.Station, Is.EqualTo(CookingStation.FryingPan));
        Assert.That(runtime.InputMode, Is.EqualTo(CookingInputMode.Hold));
        Assert.That(runtime.RequiredAmount, Is.EqualTo(2f));
        Assert.That(runtime.StandardDuration, Is.EqualTo(2.25f));
        Assert.That(runtime.AnimationMode, Is.EqualTo(CookingAnimationMode.Sustained));
        Assert.That(runtime.AnimatorStartTrigger, Is.EqualTo("PanFlipHoldStart"));
        Assert.That(runtime.AnimatorStopTrigger, Is.EqualTo("PanFlipHoldStop"));
    }

    [Test]
    public void RecipeDefinition_RejectsEmptyRecipe()
    {
        ConfigRecipeDefinition recipe = CreateRecipe(0);

        bool valid = recipe.TryBuildRuntime(out _, out string error);

        Assert.That(valid, Is.False);
        StringAssert.Contains("at least one round", error);
    }

    [Test]
    public void RecipeDefinition_RejectsRoundOutsideTwoToThreeOperations()
    {
        ConfigRecipeDefinition recipe = CreateRecipe(1);
        SetRoundOperations(recipe, 0, CreateDefaultOperation());

        bool valid = recipe.TryBuildRuntime(out _, out string error);

        Assert.That(valid, Is.False);
        StringAssert.Contains("must contain 2 or 3 operations", error);
    }

    [Test]
    public void RecipeDefinition_RejectsNullOperationReference()
    {
        ConfigRecipeDefinition recipe = CreateRecipe(1);
        SetRoundOperations(recipe, 0, CreateDefaultOperation(), null);

        bool valid = recipe.TryBuildRuntime(out _, out string error);

        Assert.That(valid, Is.False);
        StringAssert.Contains("Round 1 operation 2 is null", error);
    }

    [Test]
    public void RecipeDefinition_RejectsUnorderedThresholds()
    {
        ConfigRecipeDefinition recipe = CreateRecipe(1);
        SetRoundOperations(recipe, 0, CreateDefaultOperation(), CreateDefaultOperation());
        SetCustomThresholds(recipe, 0, 2f, 1f, 3f);

        bool valid = recipe.TryBuildRuntime(out _, out string error);

        Assert.That(valid, Is.False);
        StringAssert.Contains("Excellent <= Great <= Good", error);
    }

    [Test]
    public void RecipeDefinition_ComputesDefaultThresholdsFromOperationDurationsAndStationChanges()
    {
        ConfigRecipeDefinition recipe = CreateRecipe(1);
        SetRoundOperations(
            recipe,
            0,
            CreateOperation("stir", CookingOperationType.StirOnce, CookingStation.SoupPot,
                CookingInputMode.ShortPress, 1f, 1f, CookingAnimationMode.OneShot, "Stir", string.Empty),
            CreateOperation("cut", CookingOperationType.ChopOnce, CookingStation.CuttingBoard,
                CookingInputMode.ShortPress, 1f, 2f, CookingAnimationMode.OneShot, "Cut", string.Empty),
            CreateOperation("pan", CookingOperationType.PanFlipOnce, CookingStation.FryingPan,
                CookingInputMode.ShortPress, 1f, 1f, CookingAnimationMode.OneShot, "Pan", string.Empty));

        bool valid = recipe.TryBuildRuntime(out RecipeRuntimeData runtime, out string error);

        Assert.That(valid, Is.True, error);
        Assert.That(runtime.Rounds[0].ExcellentTime, Is.EqualTo(3.825f).Within(0.001f));
        Assert.That(runtime.Rounds[0].GreatTime, Is.EqualTo(4.5f).Within(0.001f));
        Assert.That(runtime.Rounds[0].GoodTime, Is.EqualTo(6.3f).Within(0.001f));
    }

    private static ConfigOperationDefinition CreateDefaultOperation()
    {
        return CreateOperation("stir", CookingOperationType.StirOnce, CookingStation.SoupPot,
            CookingInputMode.ShortPress, 1f, 0.5f, CookingAnimationMode.OneShot, "Stir", string.Empty);
    }

    private static ConfigOperationDefinition CreateOperation(
        string id,
        CookingOperationType type,
        CookingStation station,
        CookingInputMode inputMode,
        float required,
        float duration,
        CookingAnimationMode animationMode,
        string startTrigger,
        string stopTrigger)
    {
        ConfigOperationDefinition definition = ScriptableObject.CreateInstance<ConfigOperationDefinition>();
        SerializedObject serialized = new SerializedObject(definition);
        serialized.FindProperty("operationId").stringValue = id;
        serialized.FindProperty("operationType").enumValueIndex = (int)type;
        serialized.FindProperty("station").enumValueIndex = (int)station;
        serialized.FindProperty("inputMode").enumValueIndex = (int)inputMode;
        serialized.FindProperty("requiredAmount").floatValue = required;
        serialized.FindProperty("standardDuration").floatValue = duration;
        serialized.FindProperty("animationMode").enumValueIndex = (int)animationMode;
        serialized.FindProperty("animatorStartTrigger").stringValue = startTrigger;
        serialized.FindProperty("animatorStopTrigger").stringValue = stopTrigger;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static ConfigRecipeDefinition CreateRecipe(int roundCount)
    {
        ConfigRecipeDefinition recipe = ScriptableObject.CreateInstance<ConfigRecipeDefinition>();
        SerializedObject serialized = new SerializedObject(recipe);
        serialized.FindProperty("recipeId").stringValue = "test-recipe";
        serialized.FindProperty("displayName").stringValue = "Test Recipe";
        serialized.FindProperty("stationChangeAllowance").floatValue = 0.25f;
        serialized.FindProperty("rounds").arraySize = roundCount;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return recipe;
    }

    private static void SetRoundOperations(
        ConfigRecipeDefinition recipe,
        int roundIndex,
        params ConfigOperationDefinition[] operations)
    {
        SerializedObject serialized = new SerializedObject(recipe);
        SerializedProperty round = serialized.FindProperty("rounds").GetArrayElementAtIndex(roundIndex);
        SerializedProperty operationList = round.FindPropertyRelative("operations");
        operationList.arraySize = operations.Length;
        for (int i = 0; i < operations.Length; i++)
        {
            operationList.GetArrayElementAtIndex(i).objectReferenceValue = operations[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetCustomThresholds(
        ConfigRecipeDefinition recipe,
        int roundIndex,
        float excellent,
        float great,
        float good)
    {
        SerializedObject serialized = new SerializedObject(recipe);
        SerializedProperty round = serialized.FindProperty("rounds").GetArrayElementAtIndex(roundIndex);
        round.FindPropertyRelative("useCustomTimeThresholds").boolValue = true;
        round.FindPropertyRelative("excellentTime").floatValue = excellent;
        round.FindPropertyRelative("greatTime").floatValue = great;
        round.FindPropertyRelative("goodTime").floatValue = good;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
