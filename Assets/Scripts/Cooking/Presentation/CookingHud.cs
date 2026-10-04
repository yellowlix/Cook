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
        [SerializeField] private Image operationProgressImage;
        [SerializeField] private Image recipeProgressImage;
        [SerializeField] private TMP_Text gradeText;
        [SerializeField] private TMP_Text resultText;
        private CoreCookingSession session;

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
            SetText(gradeText, string.Empty);
            SetText(resultText, string.Empty);
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
            session = null;
        }

        private void OnDestroy() => Unbind();

        private void OnOperationStarted(OperationRuntimeData operation, int currentIndex, int operationCount)
        {
            if (operationProgressImage != null)
            {
                operationProgressImage.gameObject.SetActive(operation.InputMode != CookingInputMode.ShortPress);
                SetProgress(operationProgressImage, 0f);
            }
            SetText(gradeText, string.Empty);
            RenderOperationSequence(currentIndex);
        }

        private void OnOperationProgressChanged(float progress)
        {
            SetProgress(operationProgressImage, progress);
        }

        private void OnRecipeProgressChanged(float progress)
        {
            SetProgress(recipeProgressImage, progress);
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

        private static void SetProgress(Image image, float progress)
        {
            if (image == null) return;
            image.rectTransform.localScale = new Vector3(Mathf.Clamp01(progress), 1f, 1f);
        }
    }
}
