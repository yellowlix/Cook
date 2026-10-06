#if UNITY_EDITOR
using Cook;
using Cook.Configuration;
using Cook.Core;
using Cook.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

namespace Cook.Editor
{
    /// <summary>只更新当前场景的制作接线和 2D 占位动画，不重建用户布景。</summary>
    public static class CookSceneBuilder
    {
        private const string Folder = "Assets/Animations";
        private const string ControllerPath = Folder + "/Controllers/Cook2D.controller";

        [MenuItem("Cook/Update Production Scene And 2D Animations")]
        public static void UpdateScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/GameScene.unity")
                throw new System.InvalidOperationException("Open GameScene before updating production assets.");

            AnimatorController animatorController = BuildAnimations();
            CookController controller = Object.FindFirstObjectByType<CookController>();
            CookAnimationPresenter presenter = Object.FindFirstObjectByType<CookAnimationPresenter>();
            CookHud hud = Object.FindFirstObjectByType<CookHud>();
            if (controller == null || presenter == null || hud == null)
                throw new System.InvalidOperationException("Missing production scene components.");

            var config = new SerializedObject(controller);
            config.FindProperty("dish").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DishDefinition>("Assets/Cooking/Recipes/PracticeDish.asset");
            config.FindProperty("hud").objectReferenceValue = hud;
            config.ApplyModifiedPropertiesWithoutUndo();

            string[] names = { "SoupPotStation", "CuttingBoardStation", "FryingPanStation" };
            var presentation = new SerializedObject(presenter);
            SerializedProperty stations = presentation.FindProperty("stations");
            stations.arraySize = 3;
            for (int i = 0; i < names.Length; i++)
            {
                Transform root = FindTransform(names[i]);
                if (root == null) throw new System.InvalidOperationException("Missing " + names[i]);
                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null) throw new System.InvalidOperationException("Missing character Animator at " + names[i]);
                animator.runtimeAnimatorController = animatorController;
                animator.applyRootMotion = false;
                SerializedProperty item = stations.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("station").enumValueIndex = i;
                item.FindPropertyRelative("root").objectReferenceValue = root.gameObject;
                item.FindPropertyRelative("animator").objectReferenceValue = animator;
                root.gameObject.SetActive(i == 0);
                EditorUtility.SetDirty(animator);
            }
            presentation.ApplyModifiedPropertiesWithoutUndo();

            // Prefab 和三个场景实例使用相同的 2D 视觉子对象路径。
            GameObject prefab = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Character.prefab");
            try
            {
                Animator animator = prefab.GetComponent<Animator>();
                animator.runtimeAnimatorController = animatorController;
                animator.applyRootMotion = false;
                PrefabUtility.SaveAsPrefabAsset(prefab, "Assets/Prefabs/Character.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            LayoutHud(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static Transform FindTransform(string name)
        {
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    if (item.name == name) return item;
            return null;
        }

        private static AnimatorController BuildAnimations()
        {
            AnimationClip idle = BuildClip("Idle2D", 1f, true, new[] { 0f, 0.5f, 1f },
                new[] { 1f, 1.025f, 1f }, new[] { 1f, 1f, 1f }, new[] { 1f, 1f, 1f }, new[] { 0f, 0f, 0f });
            AnimationClip tap = BuildClip("Tap2D", 0.24f, false, new[] { 0f, 0.08f, 0.16f, 0.24f },
                new[] { 1f, 0.92f, 1.03f, 1f }, new[] { 1f, 1.08f, 0.97f, 1f }, new[] { 1f, 0.92f, 1.03f, 1f }, new[] { 0f, -7f, 4f, 0f });
            AnimationClip hold = BuildClip("Hold2D", 0.6f, true, new[] { 0f, 0.15f, 0.3f, 0.45f, 0.6f },
                new[] { 1f, 0.96f, 1f, 0.96f, 1f }, new[] { 1f, 1.03f, 1f, 1.03f, 1f }, new[] { 1f, 0.97f, 1f, 0.97f, 1f }, new[] { 0f, -5f, 0f, 5f, 0f });
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller != null) AssetDatabase.DeleteAsset(ControllerPath);
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Tap", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Holding", AnimatorControllerParameterType.Bool);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idleState = machine.AddState("Idle", new Vector3(250, 0));
            AnimatorState tapState = machine.AddState("Tap", new Vector3(500, 0));
            AnimatorState holdState = machine.AddState("Hold", new Vector3(250, 160));
            idleState.motion = idle; tapState.motion = tap; holdState.motion = hold;
            machine.defaultState = idleState;
            AnimatorStateTransition pulse = machine.AddAnyStateTransition(tapState);
            pulse.hasExitTime = false; pulse.duration = 0.025f; pulse.canTransitionToSelf = true;
            pulse.AddCondition(AnimatorConditionMode.If, 0, "Tap");
            AnimatorStateTransition looping = machine.AddAnyStateTransition(holdState);
            looping.hasExitTime = false; looping.duration = 0.05f; looping.canTransitionToSelf = false;
            looping.AddCondition(AnimatorConditionMode.If, 0, "Holding");
            AnimatorStateTransition finishTap = tapState.AddTransition(idleState);
            finishTap.hasExitTime = true; finishTap.exitTime = 1f; finishTap.duration = 0.025f;
            AnimatorStateTransition finishHold = holdState.AddTransition(idleState);
            finishHold.hasExitTime = false; finishHold.duration = 0.05f;
            finishHold.AddCondition(AnimatorConditionMode.IfNot, 0, "Holding");
            return controller;
        }

        private static AnimationClip BuildClip(string name, float duration, bool loop,
            float[] times, float[] y, float[] scaleX, float[] scaleY, float[] angle)
        {
            string path = Folder + "/Clips/" + name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
            clip.ClearCurves(); clip.name = name; clip.frameRate = 30f;
            Curve(clip, "m_LocalPosition.x", times, new float[times.Length]);
            Curve(clip, "m_LocalPosition.y", times, y);
            Curve(clip, "m_LocalPosition.z", times, new float[times.Length]);
            Curve(clip, "m_LocalScale.x", times, scaleX);
            Curve(clip, "m_LocalScale.y", times, scaleY);
            var z = new float[times.Length]; for (int i = 0; i < z.Length; i++) z[i] = 1f;
            Curve(clip, "m_LocalScale.z", times, z);
            Curve(clip, "localEulerAnglesRaw.z", times, angle);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop; settings.startTime = 0f; settings.stopTime = duration;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void Curve(AnimationClip clip, string property, float[] times, float[] values)
        {
            var keys = new Keyframe[times.Length];
            for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(times[i], values[i]);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("CharacterVisual", typeof(Transform), property), new AnimationCurve(keys));
        }

        private static void LayoutHud(CookHud hud)
        {
            var data = new SerializedObject(hud);
            PlaceText(data, "operationSequenceText", "OperationSequenceText", new Vector2(0, 205), new Vector2(950, 140), 26);
            PlaceText(data, "actualStepsText", "ActualStepsText", new Vector2(0, 110), new Vector2(1000, 60), 24);
            PlaceText(data, "roundCountdownText", "RoundCountdownText", new Vector2(0, 60), new Vector2(400, 45), 26);
            PlaceText(data, "resultText", "ResultText", new Vector2(0, 20), new Vector2(800, 65), 30);
        }

        private static void PlaceText(SerializedObject data, string property, string name, Vector2 position, Vector2 size, int fontSize)
        {
            TMP_Text text = data.FindProperty(property).objectReferenceValue as TMP_Text;
            if (text == null) return;
            text.name = name; text.gameObject.SetActive(true); text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size;
            EditorUtility.SetDirty(text);
        }
    }
}
#endif
