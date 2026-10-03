using System;
using System.Collections.Generic;

namespace Cook.Core
{
    public enum CookingStation { SoupPot, CuttingBoard, FryingPan }
    public enum CookingInputMode { ShortPress, RepeatedPress, Hold }
    public enum CookingOperationType { ChopOnce, ChopRepeated, StirOnce, StirHold, PanFlipOnce, PanFlipHold }
    public enum CookingAnimationMode { OneShot, Sustained }
    public enum CookingSessionState { Idle, RoundIntro, OperationActive, RoundResult, RecipeSuccess, RecipeFailure }
    public enum CookingGrade { Miss, Good, Great, Excellent }

    public sealed class OperationRuntimeData
    {
        public OperationRuntimeData(
            string id,
            CookingOperationType type,
            CookingStation station,
            CookingInputMode inputMode,
            float requiredAmount,
            float standardDuration,
            CookingAnimationMode animationMode,
            string animatorStartTrigger,
            string animatorStopTrigger)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Operation id cannot be blank.", nameof(id));
            }

            if (requiredAmount <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredAmount));
            }

            if (standardDuration <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(standardDuration));
            }

            Id = id;
            Type = type;
            Station = station;
            InputMode = inputMode;
            RequiredAmount = requiredAmount;
            StandardDuration = standardDuration;
            AnimationMode = animationMode;
            AnimatorStartTrigger = animatorStartTrigger;
            AnimatorStopTrigger = animatorStopTrigger;
        }

        public string Id { get; }
        public CookingOperationType Type { get; }
        public CookingStation Station { get; }
        public CookingInputMode InputMode { get; }
        public float RequiredAmount { get; }
        public float StandardDuration { get; }
        public CookingAnimationMode AnimationMode { get; }
        public string AnimatorStartTrigger { get; }
        public string AnimatorStopTrigger { get; }
    }

    public sealed class RoundRuntimeData
    {
        public RoundRuntimeData(
            IReadOnlyList<OperationRuntimeData> operations,
            float excellentTime,
            float greatTime,
            float goodTime)
        {
            if (operations == null)
            {
                throw new ArgumentNullException(nameof(operations));
            }

            if (operations.Count < 2 || operations.Count > 3)
            {
                throw new ArgumentException("A round must contain two or three operations.", nameof(operations));
            }

            var copy = new List<OperationRuntimeData>(operations.Count);
            for (int i = 0; i < operations.Count; i++)
            {
                if (operations[i] == null)
                {
                    throw new ArgumentException($"Operation {i} cannot be null.", nameof(operations));
                }

                copy.Add(operations[i]);
            }

            if (excellentTime <= 0f || excellentTime > greatTime || greatTime > goodTime)
            {
                throw new ArgumentException("Thresholds must satisfy 0 < Excellent <= Great <= Good.");
            }

            Operations = copy.AsReadOnly();
            ExcellentTime = excellentTime;
            GreatTime = greatTime;
            GoodTime = goodTime;
        }

        public IReadOnlyList<OperationRuntimeData> Operations { get; }
        public float ExcellentTime { get; }
        public float GreatTime { get; }
        public float GoodTime { get; }
    }

    public sealed class RecipeRuntimeData
    {
        public RecipeRuntimeData(string id, string displayName, IReadOnlyList<RoundRuntimeData> rounds)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Recipe id cannot be blank.", nameof(id));
            }

            if (rounds == null)
            {
                throw new ArgumentNullException(nameof(rounds));
            }

            if (rounds.Count == 0)
            {
                throw new ArgumentException("A recipe must contain at least one round.", nameof(rounds));
            }

            var copy = new List<RoundRuntimeData>(rounds.Count);
            for (int i = 0; i < rounds.Count; i++)
            {
                if (rounds[i] == null)
                {
                    throw new ArgumentException($"Round {i} cannot be null.", nameof(rounds));
                }

                copy.Add(rounds[i]);
            }

            Id = id;
            DisplayName = displayName;
            Rounds = copy.AsReadOnly();
        }

        public string Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<RoundRuntimeData> Rounds { get; }
    }
}
