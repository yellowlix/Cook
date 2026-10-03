using System;
using Cook.Core;
using UnityEngine;

namespace Cook.Configuration
{
    [CreateAssetMenu(menuName = "Cook/Operation Definition")]
    public sealed class OperationDefinition : ScriptableObject
    {
        [SerializeField] private string operationId;
        [SerializeField] private CookingOperationType operationType;
        [SerializeField] private CookingStation station;
        [SerializeField] private CookingInputMode inputMode;
        [SerializeField, Min(0.01f)] private float requiredAmount = 1f;
        [SerializeField, Min(0.01f)] private float standardDuration = 0.5f;
        [SerializeField] private Sprite icon;
        [SerializeField] private CookingAnimationMode animationMode;
        [SerializeField] private string animatorStartTrigger;
        [SerializeField] private string animatorStopTrigger;

        public Sprite Icon => icon;

        public OperationRuntimeData ToRuntime()
        {
            if (!TryValidateMapping(out string error))
            {
                throw new InvalidOperationException(error);
            }

            return new OperationRuntimeData(
                operationId,
                operationType,
                station,
                inputMode,
                requiredAmount,
                standardDuration,
                animationMode,
                animatorStartTrigger,
                animatorStopTrigger);
        }

        private void OnValidate()
        {
            requiredAmount = Mathf.Max(0.01f, requiredAmount);
            standardDuration = Mathf.Max(0.01f, standardDuration);
            if (animationMode == CookingAnimationMode.OneShot)
            {
                animatorStopTrigger = string.Empty;
            }
        }

        private bool TryValidateMapping(out string error)
        {
            CookingStation expectedStation;
            CookingInputMode expectedInput;
            CookingAnimationMode expectedAnimation;

            switch (operationType)
            {
                case CookingOperationType.ChopOnce:
                    expectedStation = CookingStation.CuttingBoard;
                    expectedInput = CookingInputMode.ShortPress;
                    expectedAnimation = CookingAnimationMode.OneShot;
                    break;
                case CookingOperationType.ChopRepeated:
                    expectedStation = CookingStation.CuttingBoard;
                    expectedInput = CookingInputMode.RepeatedPress;
                    expectedAnimation = CookingAnimationMode.Sustained;
                    break;
                case CookingOperationType.StirOnce:
                    expectedStation = CookingStation.SoupPot;
                    expectedInput = CookingInputMode.ShortPress;
                    expectedAnimation = CookingAnimationMode.OneShot;
                    break;
                case CookingOperationType.StirHold:
                    expectedStation = CookingStation.SoupPot;
                    expectedInput = CookingInputMode.Hold;
                    expectedAnimation = CookingAnimationMode.Sustained;
                    break;
                case CookingOperationType.PanFlipOnce:
                    expectedStation = CookingStation.FryingPan;
                    expectedInput = CookingInputMode.ShortPress;
                    expectedAnimation = CookingAnimationMode.OneShot;
                    break;
                case CookingOperationType.PanFlipHold:
                    expectedStation = CookingStation.FryingPan;
                    expectedInput = CookingInputMode.Hold;
                    expectedAnimation = CookingAnimationMode.Sustained;
                    break;
                default:
                    error = $"Unsupported operation type: {operationType}.";
                    return false;
            }

            if (station != expectedStation || inputMode != expectedInput || animationMode != expectedAnimation)
            {
                error = $"{operationType} requires station {expectedStation}, input {expectedInput}, " +
                        $"and animation {expectedAnimation}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(operationId))
            {
                error = "Operation id cannot be blank.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(animatorStartTrigger))
            {
                error = $"{operationId} requires an Animator start trigger.";
                return false;
            }

            if (animationMode == CookingAnimationMode.Sustained && string.IsNullOrWhiteSpace(animatorStopTrigger))
            {
                error = $"{operationId} requires an Animator stop trigger.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
