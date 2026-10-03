using System;

namespace Cook.Core
{
    /// <summary>
    /// 纯 C# 料理状态机，只负责规则、计时和进度，不依赖 Unity 表现对象。
    /// </summary>
    public sealed class CookingSession
    {
        private RecipeRuntimeData recipe;
        private CookingSessionState state = CookingSessionState.Idle;
        private CookingStation currentStation;
        private int currentRoundIndex;
        private int currentOperationIndex;
        private float currentAmount;
        private float recipeProgress;
        private float roundElapsedTime;
        private bool isHolding;

        public CookingSessionState State => state;
        public CookingStation CurrentStation => currentStation;
        public int CurrentRoundIndex => currentRoundIndex;
        public int CurrentOperationIndex => currentOperationIndex;
        public float CurrentOperationProgress01 => CurrentOperation == null
            ? 0f
            : Math.Min(1f, currentAmount / CurrentOperation.RequiredAmount);
        public float RecipeProgress01 => recipeProgress;
        public float RoundElapsedTime => roundElapsedTime;
        public OperationRuntimeData CurrentOperation =>
            CurrentRound != null && currentOperationIndex >= 0 && currentOperationIndex < CurrentRound.Operations.Count
                ? CurrentRound.Operations[currentOperationIndex]
                : null;
        public RoundRuntimeData CurrentRound =>
            recipe != null && currentRoundIndex >= 0 && currentRoundIndex < recipe.Rounds.Count
                ? recipe.Rounds[currentRoundIndex]
                : null;

        public event Action<CookingSessionState> StateChanged;
        public event Action<CookingStation> StationChanged;
        public event Action<OperationRuntimeData, int, int> OperationStarted;
        public event Action<float> OperationProgressChanged;
        public event Action<OperationRuntimeData> OperationCompleted;
        public event Action InvalidInput;
        public event Action<CookingGrade> RoundEvaluated;
        public event Action<float> RecipeProgressChanged;
        public event Action RecipeSucceeded;
        public event Action RecipeFailed;

        public void StartRecipe(RecipeRuntimeData recipe, CookingStation startingStation)
        {
            this.recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
            currentStation = startingStation;
            currentRoundIndex = 0;
            recipeProgress = 0f;
            StationChanged?.Invoke(currentStation);
            RecipeProgressChanged?.Invoke(recipeProgress);
            StartCurrentRound();
        }

        public void MoveStation(int direction)
        {
            if (direction == 0)
            {
                return;
            }

            int next = Math.Max(0, Math.Min(2, (int)currentStation + Math.Sign(direction)));
            if (next == (int)currentStation)
            {
                return;
            }

            // 离开工位只暂停长按，不清除已经累积的工序进度。
            isHolding = false;
            currentStation = (CookingStation)next;
            StationChanged?.Invoke(currentStation);
        }

        public void PressCook()
        {
            if (state != CookingSessionState.OperationActive || CurrentOperation == null)
            {
                return;
            }

            if (currentStation != CurrentOperation.Station)
            {
                InvalidInput?.Invoke();
                return;
            }

            switch (CurrentOperation.InputMode)
            {
                case CookingInputMode.ShortPress:
                    AddOperationProgress(CurrentOperation.RequiredAmount);
                    break;
                case CookingInputMode.RepeatedPress:
                    AddOperationProgress(1f);
                    break;
                case CookingInputMode.Hold:
                    isHolding = true;
                    break;
            }
        }

        public void ReleaseCook()
        {
            if (state == CookingSessionState.OperationActive &&
                CurrentOperation?.InputMode == CookingInputMode.Hold)
            {
                isHolding = false;
            }
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (state != CookingSessionState.OperationActive)
            {
                return;
            }

            roundElapsedTime += deltaTime;

            if (isHolding && CurrentOperation != null &&
                CurrentOperation.InputMode == CookingInputMode.Hold &&
                currentStation == CurrentOperation.Station)
            {
                AddOperationProgress(deltaTime);
            }

            if (state == CookingSessionState.OperationActive && roundElapsedTime > CurrentRound.GoodTime)
            {
                EvaluateRound(CookingGrade.Miss);
            }
        }

        public void ContinueAfterRoundResult()
        {
            if (state != CookingSessionState.RoundResult)
            {
                return;
            }

            if (recipeProgress >= 1f)
            {
                ChangeState(CookingSessionState.RecipeSuccess);
                RecipeSucceeded?.Invoke();
                return;
            }

            if (currentRoundIndex + 1 < recipe.Rounds.Count)
            {
                currentRoundIndex++;
                StartCurrentRound();
                return;
            }

            ChangeState(CookingSessionState.RecipeFailure);
            RecipeFailed?.Invoke();
        }

        private void StartCurrentRound()
        {
            currentOperationIndex = 0;
            currentAmount = 0f;
            roundElapsedTime = 0f;
            isHolding = false;
            ChangeState(CookingSessionState.RoundIntro);
            StartCurrentOperation();
        }

        private void StartCurrentOperation()
        {
            currentAmount = 0f;
            isHolding = false;
            ChangeState(CookingSessionState.OperationActive);
            OperationProgressChanged?.Invoke(0f);
            OperationStarted?.Invoke(CurrentOperation, currentOperationIndex, CurrentRound.Operations.Count);
        }

        private void AddOperationProgress(float amount)
        {
            OperationRuntimeData operation = CurrentOperation;
            if (operation == null || amount <= 0f)
            {
                return;
            }

            currentAmount = Math.Min(operation.RequiredAmount, currentAmount + amount);
            OperationProgressChanged?.Invoke(CurrentOperationProgress01);
            if (currentAmount >= operation.RequiredAmount)
            {
                CompleteCurrentOperation();
            }
        }

        private void CompleteCurrentOperation()
        {
            OperationRuntimeData completed = CurrentOperation;
            currentAmount = completed.RequiredAmount;
            OperationProgressChanged?.Invoke(1f);
            OperationCompleted?.Invoke(completed);
            isHolding = false;
            currentOperationIndex++;

            // 动画不参与状态判定，下一工序在同一调用栈中立即开放输入。
            if (currentOperationIndex < CurrentRound.Operations.Count)
            {
                StartCurrentOperation();
                return;
            }

            EvaluateRound(GradeForElapsedTime());
        }

        private CookingGrade GradeForElapsedTime()
        {
            if (roundElapsedTime <= CurrentRound.ExcellentTime)
            {
                return CookingGrade.Excellent;
            }

            if (roundElapsedTime <= CurrentRound.GreatTime)
            {
                return CookingGrade.Great;
            }

            return roundElapsedTime <= CurrentRound.GoodTime
                ? CookingGrade.Good
                : CookingGrade.Miss;
        }

        private void EvaluateRound(CookingGrade grade)
        {
            isHolding = false;
            float multiplier;
            switch (grade)
            {
                case CookingGrade.Excellent:
                    multiplier = 1.5f;
                    break;
                case CookingGrade.Great:
                    multiplier = 1.25f;
                    break;
                case CookingGrade.Good:
                    multiplier = 1f;
                    break;
                default:
                    multiplier = 0f;
                    break;
            }

            float baseProgress = 1f / recipe.Rounds.Count;
            recipeProgress = Math.Min(1f, recipeProgress + baseProgress * multiplier);
            ChangeState(CookingSessionState.RoundResult);
            RoundEvaluated?.Invoke(grade);
            RecipeProgressChanged?.Invoke(recipeProgress);
        }

        private void ChangeState(CookingSessionState next)
        {
            state = next;
            StateChanged?.Invoke(state);
        }
    }
}
