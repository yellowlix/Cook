using System;
using System.Text;
using Cook.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CoreCookingSession = Cook.Core.CookingSession;

namespace Cook.Presentation
{
    /// <summary>第一阶段占位 HUD，只消费状态机事件，不决定玩法流程。</summary>
    public sealed class CookingHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text operationSequenceText;
        [SerializeField] private GameObject operationProgressBar;
        [SerializeField] private Image operationProgressFill;
        [SerializeField] private Image recipeProgressFill;
        [SerializeField] private TMP_Text gradeText;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private TMP_Text roundCountdownText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button restartButton;
        private CoreCookingSession session;

        public event Action StartRequested;
        public event Action RestartRequested;

        private void Awake()
        {
            if (startButton != null) startButton.onClick.AddListener(NotifyStartRequested);
            if (restartButton != null) restartButton.onClick.AddListener(NotifyRestartRequested);
        }

        public void ShowReady()
        {
            SetVisible(startButton, true);
            SetVisible(restartButton, false);
            SetVisible(operationSequenceText, false);
            SetVisible(operationProgressBar, false);
            SetVisible(recipeProgressFill != null ? recipeProgressFill.transform.parent.gameObject : null, false);
            SetVisible(gradeText, false);
            SetVisible(resultText, false);
            SetVisible(roundCountdownText, false);
        }

        public void Bind(CoreCookingSession value)
        {
            Unbind();
            session = value;
            if (session == null) return;
            session.OperationStarted += OnOperationStarted;
            session.OperationProgressChanged += OnOperationProgressChanged;
            session.RoundEvaluated += OnRoundEvaluated;
            session.RecipeProgressChanged += OnRecipeProgressChanged;
            session.RecipeSucceeded += OnRecipeSucceeded;
            session.RecipeFailed += OnRecipeFailed;
            session.StateChanged += OnStateChanged;
            SetText(gradeText, string.Empty);
            SetText(resultText, string.Empty);
            SetProgress(operationProgressFill, 0f);
            SetProgress(recipeProgressFill, 0f);
            SetVisible(startButton, false);
            SetVisible(restartButton, true);
            SetVisible(operationSequenceText, true);
            SetVisible(recipeProgressFill != null ? recipeProgressFill.transform.parent.gameObject : null, true);
            SetVisible(gradeText, true);
            SetVisible(resultText, true);
        }

        public void Unbind()
        {
            if (session == null) return;
            session.OperationStarted -= OnOperationStarted;
            session.OperationProgressChanged -= OnOperationProgressChanged;
            session.RoundEvaluated -= OnRoundEvaluated;
            session.RecipeProgressChanged -= OnRecipeProgressChanged;
            session.RecipeSucceeded -= OnRecipeSucceeded;
            session.RecipeFailed -= OnRecipeFailed;
            session.StateChanged -= OnStateChanged;
            session = null;
        }

        private void OnDestroy()
        {
            Unbind();
            if (startButton != null) startButton.onClick.RemoveListener(NotifyStartRequested);
            if (restartButton != null) restartButton.onClick.RemoveListener(NotifyRestartRequested);
        }

        private void Update()
        {
            if (session?.State != CookingSessionState.OperationActive || session.CurrentRound == null) return;
            float remaining = Mathf.Max(0f, session.CurrentRound.GoodTime - session.RoundElapsedTime);
            SetText(roundCountdownText, $"剩余 {remaining:0.0}s");
        }

        private void NotifyStartRequested() => StartRequested?.Invoke();
        private void NotifyRestartRequested() => RestartRequested?.Invoke();

        private void OnStateChanged(CookingSessionState state)
        {
            SetVisible(roundCountdownText, state == CookingSessionState.OperationActive);
            if (state == CookingSessionState.OperationActive)
            {
                SetText(roundCountdownText, $"剩余 {session.CurrentRound.GoodTime:0.0}s");
            }
        }

        private void OnOperationStarted(OperationRuntimeData operation, int currentIndex, int operationCount)
        {
            if (operationProgressBar != null)
            {
                operationProgressBar.SetActive(operation.InputMode != CookingInputMode.ShortPress);
            }
            SetProgress(operationProgressFill, 0f);
            SetText(gradeText, string.Empty);
            RenderOperationSequence(currentIndex);
        }

        private void OnOperationProgressChanged(float progress)
        {
            SetProgress(operationProgressFill, progress);
        }

        private void OnRecipeProgressChanged(float progress)
        {
            SetProgress(recipeProgressFill, progress);
        }

        private void OnRoundEvaluated(CookingGrade grade) => SetText(gradeText, grade.ToString());
        private void OnRecipeSucceeded() => SetText(resultText, "完成");
        private void OnRecipeFailed() => SetText(resultText, "失败");

        private void RenderOperationSequence(int activeIndex)
        {
            if (operationSequenceText == null || session?.CurrentRound == null) return;
            var builder = new StringBuilder();
            for (int i = 0; i < session.CurrentRound.Operations.Count; i++)
            {
                if (i > 0) builder.Append("  >  ");
                string label = LabelFor(session.CurrentRound.Operations[i].Type);
                if (i < activeIndex) builder.Append("✓ ").Append(label);
                else if (i == activeIndex) builder.Append('[').Append(label).Append(']');
                else builder.Append(label);
            }
            operationSequenceText.text = builder.ToString();
        }

        private static string LabelFor(CookingOperationType type)
        {
            switch (type)
            {
                case CookingOperationType.ChopOnce: return "切一下";
                case CookingOperationType.ChopRepeated: return "连续切菜";
                case CookingOperationType.StirOnce: return "搅一下";
                case CookingOperationType.StirHold: return "持续搅动";
                case CookingOperationType.PanFlipOnce: return "翻一下";
                case CookingOperationType.PanFlipHold: return "持续翻炒";
                default: return type.ToString();
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null) target.text = value;
        }

        private static void SetVisible(Component target, bool visible)
        {
            if (target != null) target.gameObject.SetActive(visible);
        }

        private static void SetVisible(GameObject target, bool visible)
        {
            if (target != null) target.SetActive(visible);
        }

        private static void SetProgress(Image image, float progress)
        {
            if (image == null) return;
            image.fillAmount = Mathf.Clamp01(progress);
        }
    }
}
