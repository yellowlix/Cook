#if UNITY_EDITOR
using System.Collections.Generic;
using Cook.Configuration;
using Cook.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ConfigOperationDefinition = Cook.Configuration.OperationDefinition;
using ConfigRecipeDefinition = Cook.Configuration.RecipeDefinition;

namespace Cook.Editor
{
    /// <summary>生成可重复构建的占位料理配置、动画片段和 Animator。</summary>
    public static class CookingDemoBuilder
    {
        private const string OperationsFolder = "Assets/Cooking/Operations";
        private const string RecipesFolder = "Assets/Cooking/Recipes";
        private const string ClipsFolder = "Assets/Animations/Clips";
        private const string ControllersFolder = "Assets/Animations/Controllers";

        [MenuItem("Cook/Build Demo Content")]
        public static void BuildDemoContent()
        {
            EnsureFolders();
            Dictionary<CookingOperationType, ConfigOperationDefinition> operations = CreateOperations();
            CreateRecipe(operations);
            Dictionary<string, AnimationClip> clips = CreateClips();
            CreateAnimatorController(clips);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Cooking demo content rebuilt.");
        }

        private static Dictionary<CookingOperationType, ConfigOperationDefinition> CreateOperations()
        {
            return new Dictionary<CookingOperationType, ConfigOperationDefinition>
            {
                [CookingOperationType.ChopOnce] = CreateOperation(
                    "ChopOnce", CookingOperationType.ChopOnce, CookingStation.CuttingBoard,
                    CookingInputMode.ShortPress, 1f, 0.5f, CookingAnimationMode.OneShot,
                    "ChopOnce", string.Empty),
                [CookingOperationType.ChopRepeated] = CreateOperation(
                    "ChopRepeated", CookingOperationType.ChopRepeated, CookingStation.CuttingBoard,
                    CookingInputMode.RepeatedPress, 6f, 2.5f, CookingAnimationMode.Sustained,
                    "ChopRepeatedStart", "ChopRepeatedStop"),
                [CookingOperationType.StirOnce] = CreateOperation(
                    "StirOnce", CookingOperationType.StirOnce, CookingStation.SoupPot,
                    CookingInputMode.ShortPress, 1f, 0.55f, CookingAnimationMode.OneShot,
                    "StirOnce", string.Empty),
                [CookingOperationType.StirHold] = CreateOperation(
                    "StirHold", CookingOperationType.StirHold, CookingStation.SoupPot,
                    CookingInputMode.Hold, 2f, 2.25f, CookingAnimationMode.Sustained,
                    "StirHoldStart", "StirHoldStop"),
                [CookingOperationType.PanFlipOnce] = CreateOperation(
                    "PanFlipOnce", CookingOperationType.PanFlipOnce, CookingStation.FryingPan,
                    CookingInputMode.ShortPress, 1f, 0.6f, CookingAnimationMode.OneShot,
                    "PanFlipOnce", string.Empty),
                [CookingOperationType.PanFlipHold] = CreateOperation(
                    "PanFlipHold", CookingOperationType.PanFlipHold, CookingStation.FryingPan,
                    CookingInputMode.Hold, 2f, 2.25f, CookingAnimationMode.Sustained,
                    "PanFlipHoldStart", "PanFlipHoldStop")
            };
        }

        private static ConfigOperationDefinition CreateOperation(
            string name,
            CookingOperationType type,
            CookingStation station,
            CookingInputMode inputMode,
            float requiredAmount,
            float standardDuration,
            CookingAnimationMode animationMode,
            string startTrigger,
            string stopTrigger)
        {
            string path = $"{OperationsFolder}/{name}.asset";
            DeleteIfExists(path);
            ConfigOperationDefinition asset = ScriptableObject.CreateInstance<ConfigOperationDefinition>();
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("operationId").stringValue = name;
            serialized.FindProperty("operationType").enumValueIndex = (int)type;
            serialized.FindProperty("station").enumValueIndex = (int)station;
            serialized.FindProperty("inputMode").enumValueIndex = (int)inputMode;
            serialized.FindProperty("requiredAmount").floatValue = requiredAmount;
            serialized.FindProperty("standardDuration").floatValue = standardDuration;
            serialized.FindProperty("animationMode").enumValueIndex = (int)animationMode;
            serialized.FindProperty("animatorStartTrigger").stringValue = startTrigger;
            serialized.FindProperty("animatorStopTrigger").stringValue = stopTrigger;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void CreateRecipe(Dictionary<CookingOperationType, ConfigOperationDefinition> operations)
        {
            string path = $"{RecipesFolder}/DemoRecipe.asset";
            DeleteIfExists(path);
            ConfigRecipeDefinition recipe = ScriptableObject.CreateInstance<ConfigRecipeDefinition>();
            var serialized = new SerializedObject(recipe);
            serialized.FindProperty("recipeId").stringValue = "demo-recipe";
            serialized.FindProperty("displayName").stringValue = "料理练习";
            serialized.FindProperty("stationChangeAllowance").floatValue = 0.25f;
            SerializedProperty rounds = serialized.FindProperty("rounds");
            rounds.arraySize = 3;
            SetRound(rounds.GetArrayElementAtIndex(0), operations,
                CookingOperationType.StirOnce, CookingOperationType.ChopOnce);
            SetRound(rounds.GetArrayElementAtIndex(1), operations,
                CookingOperationType.ChopRepeated, CookingOperationType.PanFlipOnce, CookingOperationType.StirHold);
            SetRound(rounds.GetArrayElementAtIndex(2), operations,
                CookingOperationType.PanFlipHold, CookingOperationType.ChopOnce, CookingOperationType.StirOnce);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(recipe, path);
        }

        private static void SetRound(
            SerializedProperty round,
            IReadOnlyDictionary<CookingOperationType, ConfigOperationDefinition> assets,
            params CookingOperationType[] operationTypes)
        {
            SerializedProperty operationList = round.FindPropertyRelative("operations");
            operationList.arraySize = operationTypes.Length;
            for (int i = 0; i < operationTypes.Length; i++)
            {
                operationList.GetArrayElementAtIndex(i).objectReferenceValue = assets[operationTypes[i]];
            }
            round.FindPropertyRelative("useCustomTimeThresholds").boolValue = false;
        }

        private static Dictionary<string, AnimationClip> CreateClips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            clips["Idle"] = CreateClip("Idle", true,
                new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.5f, 0.54f), new Keyframe(1f, 0.5f)),
                ConstantCurve(0f, 1f));
            clips["Cut_Once"] = CreateClip("Cut_Once", false,
                new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.16f, 0.18f), new Keyframe(0.35f, 0.5f)),
                new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.16f, 0.16f), new Keyframe(0.35f, 0f)));
            clips["Stir_Once"] = CreateClip("Stir_Once", false,
                new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.22f, 0.56f), new Keyframe(0.45f, 0.5f)),
                new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.12f, -0.22f), new Keyframe(0.34f, 0.22f), new Keyframe(0.45f, 0f)));
            clips["PanFlip_Once"] = CreateClip("PanFlip_Once", false,
                new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.24f, 1.05f), new Keyframe(0.5f, 0.5f)),
                new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.24f, -0.28f), new Keyframe(0.5f, 0f)));

            AddSustainedClips(clips, "CutRepeated", 0.2f, 0.28f, 0.34f);
            AddSustainedClips(clips, "StirHold", 0.1f, 0.18f, 0.55f);
            AddSustainedClips(clips, "PanFlipHold", 0.3f, 0.38f, 0.5f);
            return clips;
        }

        private static void AddSustainedClips(
            IDictionary<string, AnimationClip> clips,
            string prefix,
            float xAmount,
            float yAmount,
            float loopDuration)
        {
            clips[$"{prefix}_Enter"] = CreateClip($"{prefix}_Enter", false,
                new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.15f, 0.5f + yAmount * 0.4f)),
                new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, xAmount * 0.5f)));
            clips[$"{prefix}_Loop"] = CreateClip($"{prefix}_Loop", true,
                new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(loopDuration * 0.5f, 0.5f + yAmount), new Keyframe(loopDuration, 0.5f)),
                new AnimationCurve(new Keyframe(0f, -xAmount), new Keyframe(loopDuration * 0.5f, xAmount), new Keyframe(loopDuration, -xAmount)));
            clips[$"{prefix}_Exit"] = CreateClip($"{prefix}_Exit", false,
                new AnimationCurve(new Keyframe(0f, 0.5f + yAmount * 0.4f), new Keyframe(0.15f, 0.5f)),
                new AnimationCurve(new Keyframe(0f, xAmount * 0.5f), new Keyframe(0.15f, 0f)));
        }

        private static AnimationClip CreateClip(string name, bool loop, AnimationCurve yCurve, AnimationCurve xCurve)
        {
            string path = $"{ClipsFolder}/{name}.anim";
            DeleteIfExists(path);
            var clip = new AnimationClip { name = name, frameRate = 60f };
            clip.SetCurve("CharacterVisual", typeof(Transform), "m_LocalPosition.y", yCurve);
            clip.SetCurve("CharacterVisual", typeof(Transform), "m_LocalPosition.x", xCurve);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static AnimationCurve ConstantCurve(float value, float duration)
        {
            return new AnimationCurve(new Keyframe(0f, value), new Keyframe(duration, value));
        }

        private static void CreateAnimatorController(IReadOnlyDictionary<string, AnimationClip> clips)
        {
            string path = $"{ControllersFolder}/CookingCharacter.controller";
            DeleteIfExists(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AddTrigger(controller, "ChopOnce");
            AddTrigger(controller, "StirOnce");
            AddTrigger(controller, "PanFlipOnce");
            AddTrigger(controller, "ChopRepeatedStart");
            AddTrigger(controller, "ChopRepeatedStop");
            AddTrigger(controller, "StirHoldStart");
            AddTrigger(controller, "StirHoldStop");
            AddTrigger(controller, "PanFlipHoldStart");
            AddTrigger(controller, "PanFlipHoldStop");

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idle = AddState(machine, "Idle", clips["Idle"]);
            machine.defaultState = idle;
            AddOneShot(machine, idle, "Cut_Once", clips["Cut_Once"], "ChopOnce");
            AddOneShot(machine, idle, "Stir_Once", clips["Stir_Once"], "StirOnce");
            AddOneShot(machine, idle, "PanFlip_Once", clips["PanFlip_Once"], "PanFlipOnce");
            AddSustained(machine, idle, clips, "CutRepeated", "ChopRepeatedStart", "ChopRepeatedStop");
            AddSustained(machine, idle, clips, "StirHold", "StirHoldStart", "StirHoldStop");
            AddSustained(machine, idle, clips, "PanFlipHold", "PanFlipHoldStart", "PanFlipHoldStop");
            EditorUtility.SetDirty(controller);
        }

        private static void AddTrigger(AnimatorController controller, string name)
        {
            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
        }

        private static AnimatorState AddState(AnimatorStateMachine machine, string name, Motion motion)
        {
            AnimatorState state = machine.AddState(name);
            state.motion = motion;
            return state;
        }

        private static void AddOneShot(
            AnimatorStateMachine machine,
            AnimatorState idle,
            string stateName,
            AnimationClip clip,
            string trigger)
        {
            AnimatorState state = AddState(machine, stateName, clip);
            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0.02f;
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            AddExitTransition(state, idle);
        }

        private static void AddSustained(
            AnimatorStateMachine machine,
            AnimatorState idle,
            IReadOnlyDictionary<string, AnimationClip> clips,
            string prefix,
            string startTrigger,
            string stopTrigger)
        {
            AnimatorState enterState = AddState(machine, $"{prefix}_Enter", clips[$"{prefix}_Enter"]);
            AnimatorState loopState = AddState(machine, $"{prefix}_Loop", clips[$"{prefix}_Loop"]);
            AnimatorState exitState = AddState(machine, $"{prefix}_Exit", clips[$"{prefix}_Exit"]);
            AnimatorStateTransition start = machine.AddAnyStateTransition(enterState);
            start.hasExitTime = false;
            start.duration = 0.02f;
            start.AddCondition(AnimatorConditionMode.If, 0f, startTrigger);
            AddExitTransition(enterState, loopState);
            AnimatorStateTransition stop = loopState.AddTransition(exitState);
            stop.hasExitTime = false;
            stop.duration = 0.03f;
            stop.AddCondition(AnimatorConditionMode.If, 0f, stopTrigger);
            AddExitTransition(exitState, idle);
        }

        private static void AddExitTransition(AnimatorState source, AnimatorState destination)
        {
            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = true;
            transition.exitTime = 0.95f;
            transition.duration = 0.03f;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Cooking");
            EnsureFolder("Assets/Cooking", "Operations");
            EnsureFolder("Assets/Cooking", "Recipes");
            EnsureFolder("Assets", "Animations");
            EnsureFolder("Assets/Animations", "Clips");
            EnsureFolder("Assets/Animations", "Controllers");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }

        private static void DeleteIfExists(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
        }
    }
}
#endif
