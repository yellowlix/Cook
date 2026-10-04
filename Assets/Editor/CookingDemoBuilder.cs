#if UNITY_EDITOR
using System.Collections.Generic;
using Cook.Configuration;
using Cook.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Cook.Presentation;
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
        private const string FontsFolder = "Assets/Fonts";
        private const string HudFontSourcePath = FontsFolder + "/NotoSansSC-CookingSubset.ttf";
        private const string HudFontAssetPath = FontsFolder + "/NotoSansSC-CookingSubset SDF.asset";
        private const string HudFontCharacters =
            "切一下连续菜搅持续动翻炒完成失败料理练习✓> []ExcellentGreatGoodMiss";

        [MenuItem("Cook/Build Demo Content")]
        public static void BuildDemoContent()
        {
            EnsureFolders();
            CreateHudFontAsset();
            Dictionary<CookingOperationType, ConfigOperationDefinition> operations = CreateOperations();
            CreateRecipe(operations);
            Dictionary<string, AnimationClip> clips = CreateClips();
            CreateAnimatorController(clips);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Cooking demo content rebuilt.");
        }

        [MenuItem("Cook/Build Demo Scene")]
        public static void BuildDemoScene()
        {
            GameObject sceneRoot = FindRequired("World", "CookSceneRoot");
            sceneRoot.name = "CookSceneRoot";
            RenameDirectChild(sceneRoot.transform, "Main Camera", "MainCamera");
            RenameDirectChild(sceneRoot.transform, "Directional Light", "KeyLight");
            RenameDirectChild(sceneRoot.transform, "Global Volume", "GlobalPostProcess");

            Transform kitchen = FindRequiredChild(sceneRoot.transform, "Kitchen");
            Transform gameplay = FindRequiredChild(kitchen, "CookingArea", "CookingGameplay");
            gameplay.name = "CookingGameplay";
            Transform props = FindRequiredChild(kitchen, "CookingStove", "KitchenProps");
            props.name = "KitchenProps";

            RenameAnyDirectChild(props, "Counter", "Cube", "Stove", "Counter");
            RenameAnyDirectChild(props, "SoupPot", "Cook1", "Stir", "SoupPot");
            RenameAnyDirectChild(props, "CuttingBoard", "Cook2", "Cut", "CuttingBoard");
            RenameAnyDirectChild(props, "FryingPan", "Cook3", "Fry", "FryingPan");

            StationPresentation[] stationPresentations =
            {
                ConfigureStation(gameplay, "LeftStation", "SoupPotStation", "SoupPotCharacter", CookingStation.SoupPot),
                ConfigureStation(gameplay, "CenterStation", "CuttingBoardStation", "CuttingBoardCharacter", CookingStation.CuttingBoard),
                ConfigureStation(gameplay, "RightStation", "FryingPanStation", "FryingPanCharacter", CookingStation.FryingPan)
            };

            var presenter = GetOrAdd<CookingAnimationPresenter>(gameplay.gameObject);
            presenter.ConfigureStations(stationPresentations);
            EditorUtility.SetDirty(presenter);

            CookingHud hud = CreateHud(sceneRoot.transform);
            var controller = GetOrAdd<Cook.CookingInputController>(gameplay.gameObject);
            var controllerData = new SerializedObject(controller);
            controllerData.FindProperty("recipe").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<ConfigRecipeDefinition>($"{RecipesFolder}/DemoRecipe.asset");
            controllerData.FindProperty("startingStation").enumValueIndex = (int)CookingStation.SoupPot;
            controllerData.FindProperty("animationPresenter").objectReferenceValue = presenter;
            controllerData.FindProperty("hud").objectReferenceValue = hud;
            controllerData.FindProperty("roundResultDisplaySeconds").floatValue = 0.75f;
            controllerData.ApplyModifiedPropertiesWithoutUndo();

            stationPresentations[0].root.SetActive(true);
            stationPresentations[1].root.SetActive(false);
            stationPresentations[2].root.SetActive(false);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("Cooking demo scene hierarchy and references rebuilt.");
        }

        private static StationPresentation ConfigureStation(
            Transform gameplay,
            string oldStationName,
            string stationName,
            string characterName,
            CookingStation station)
        {
            Transform stationTransform = FindRequiredChild(gameplay, oldStationName, stationName);
            stationTransform.name = stationName;
            Transform character = FindRequiredChild(stationTransform, "Character", characterName);
            character.name = characterName;
            Transform visual = FindRequiredChild(character, "Capsule", "CharacterVisual");
            visual.name = "CharacterVisual";
            Animator animator = GetOrAdd<Animator>(character.gameObject);
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                $"{ControllersFolder}/CookingCharacter.controller");
            EditorUtility.SetDirty(animator);
            return new StationPresentation { station = station, root = stationTransform.gameObject, animator = animator };
        }

        private static CookingHud CreateHud(Transform sceneRoot)
        {
            Transform existing = sceneRoot.Find("CookingHUD");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            GameObject canvasObject = new GameObject(
                "CookingHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(sceneRoot, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            TMP_Text sequence = CreateText(canvasObject.transform, "OperationSequenceText", 34, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1200f, 70f));
            Slider operationProgress = CreateSlider(canvasObject.transform, "OperationProgressSlider",
                new Vector2(0.5f, 1f), new Vector2(0f, -125f), new Vector2(620f, 28f));
            Slider recipeProgress = CreateSlider(canvasObject.transform, "RecipeProgressSlider",
                new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(900f, 34f));
            TMP_Text grade = CreateText(canvasObject.transform, "GradeText", 54, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(600f, 90f));
            TMP_Text result = CreateText(canvasObject.transform, "ResultText", 70, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 110f));

            CookingHud hud = canvasObject.AddComponent<CookingHud>();
            var serialized = new SerializedObject(hud);
            serialized.FindProperty("operationSequenceText").objectReferenceValue = sequence;
            serialized.FindProperty("operationProgressSlider").objectReferenceValue = operationProgress;
            serialized.FindProperty("recipeProgressSlider").objectReferenceValue = recipeProgress;
            serialized.FindProperty("gradeText").objectReferenceValue = grade;
            serialized.FindProperty("resultText").objectReferenceValue = result;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem));
                eventSystem.transform.SetParent(sceneRoot, false);
            }
            return hud;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            int fontSize,
            TextAlignmentOptions alignment,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 position,
            Vector2 size)
        {
            GameObject target = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            target.transform.SetParent(parent, false);
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TMP_Text text = target.GetComponent<TMP_Text>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HudFontAssetPath);
            if (text.font == null)
            {
                throw new MissingReferenceException($"Missing TMP HUD font asset: {HudFontAssetPath}");
            }
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.enableWordWrapping = false;
            text.text = string.Empty;
            return text;
        }

        private static Slider CreateSlider(
            Transform parent,
            string name,
            Vector2 anchor,
            Vector2 position,
            Vector2 size)
        {
            GameObject target = DefaultControls.CreateSlider(new DefaultControls.Resources());
            target.name = name;
            target.transform.SetParent(parent, false);
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Slider slider = target.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.interactable = false;
            return slider;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static GameObject FindRequired(params string[] names)
        {
            foreach (string name in names)
            {
                GameObject result = GameObject.Find(name);
                if (result != null) return result;
            }
            throw new MissingReferenceException($"Cannot find any of: {string.Join(", ", names)}");
        }

        private static Transform FindRequiredChild(Transform parent, params string[] names)
        {
            foreach (string name in names)
            {
                Transform result = parent.Find(name);
                if (result != null) return result;
            }
            throw new MissingReferenceException($"Cannot find child under {parent.name}: {string.Join(", ", names)}");
        }

        private static void RenameDirectChild(Transform parent, string oldName, string newName)
        {
            Transform child = parent.Find(oldName) ?? parent.Find(newName);
            if (child == null) throw new MissingReferenceException($"Cannot find {oldName} under {parent.name}.");
            child.name = newName;
        }

        private static void RenameAnyDirectChild(Transform parent, string newName, params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                Transform child = parent.Find(candidate);
                if (child == null) continue;
                child.name = newName;
                return;
            }
            throw new MissingReferenceException($"Cannot find a source for {newName} under {parent.name}.");
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
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }
            else
            {
                ClearAnimatorController(controller);
            }
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
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssetIfDirty(controller);
            AssetDatabase.ForceReserializeAssets(new[] { path });
            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            AnimatorController importedController = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (importedController == null ||
                importedController.parameters.Length != 9 ||
                importedController.layers.Length != 1 ||
                importedController.layers[0].stateMachine.states.Length != 13)
            {
                throw new UnityException("Cooking Animator Controller did not persist its generated graph.");
            }
        }

        private static void ClearAnimatorController(AnimatorController controller)
        {
            for (int i = controller.parameters.Length - 1; i >= 0; i--)
            {
                controller.RemoveParameter(i);
            }

            if (controller.layers.Length == 0)
            {
                controller.AddLayer("Base Layer");
            }

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
            {
                machine.RemoveAnyStateTransition(transition);
            }
            foreach (AnimatorTransition transition in machine.entryTransitions)
            {
                machine.RemoveEntryTransition(transition);
            }
            foreach (ChildAnimatorState childState in machine.states)
            {
                machine.RemoveState(childState.state);
            }
            foreach (ChildAnimatorStateMachine childMachine in machine.stateMachines)
            {
                machine.RemoveStateMachine(childMachine.stateMachine);
            }
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
            EnsureFolder("Assets", "Fonts");
        }

        private static void CreateHudFontAsset()
        {
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(HudFontSourcePath);
            if (sourceFont == null)
            {
                throw new MissingReferenceException($"Missing HUD font source: {HudFontSourcePath}");
            }

            DeleteIfExists(HudFontAssetPath);
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
            fontAsset.name = "NotoSansSC Cooking Subset SDF";
            AssetDatabase.CreateAsset(fontAsset, HudFontAssetPath);

            foreach (Texture2D atlasTexture in fontAsset.atlasTextures)
            {
                if (atlasTexture != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(atlasTexture)))
                {
                    AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
                }
            }
            if (fontAsset.material != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(fontAsset.material)))
            {
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }
            if (!fontAsset.TryAddCharacters(HudFontCharacters, out string missingCharacters))
            {
                throw new UnityException($"TMP HUD font source is missing: {missingCharacters}");
            }
            EditorUtility.SetDirty(fontAsset);
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
