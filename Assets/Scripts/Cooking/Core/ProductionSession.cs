using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Cook.Core
{
    public enum ProductionState { Active, Completed, Cancelled }

    // 一次会话制作一道菜。输入识别、库存、定价和场景生命周期由调用方负责。
    public sealed class ProductionSession
    {
        private const int MaxActualOperations = 3;
        private const double TimeTolerance = 0.000001d;
        private readonly double runTimeLimit;
        private readonly List<ProductionRunResult> runResults = new List<ProductionRunResult>();
        private readonly ReadOnlyCollection<ProductionRunResult> runResultsView;
        private List<ProductionOperationData> actualOperations;
        private ReadOnlyCollection<ProductionOperationData> actualOperationsView;
        private double runElapsedTime;
        private double totalElapsedTime;
        private bool completionPublished;

        public ProductionSession(DishRuntimeData dish)
        {
            Dish = dish ?? throw new ArgumentNullException(nameof(dish));
            runTimeLimit = 60d / dish.Runs.Count;
            runResultsView = runResults.AsReadOnly();
            ResetActualOperations();
            State = ProductionState.Active;
        }

        // 事件在状态更新后发布。Completed 每份菜最多发布一次。
        public event Action Changed;
        public event Action<ProductionResult> Completed;

        public DishRuntimeData Dish { get; }
        public ProductionState State { get; private set; }
        public int RunIndex { get; private set; }
        public ProductionRunData CurrentRun => State == ProductionState.Active ? Dish.Runs[RunIndex] : null;
        public int RemainingRunCount => State == ProductionState.Active ? Dish.Runs.Count - RunIndex : 0;
        public float RunElapsedTime => (float)runElapsedTime;
        public float TotalElapsedTime => (float)totalElapsedTime;
        public float RemainingTime => State == ProductionState.Active
            ? (float)Math.Max(0d, runTimeLimit - runElapsedTime) : 0f;
        public IReadOnlyList<ProductionOperationData> ActualOperations => actualOperationsView;
        public IReadOnlyList<ProductionRunResult> RunResults => runResultsView;
        public ProductionResult Result { get; private set; }

        // 仅提交已经识别完成的单击、三连击或长按；不校验是否符合指引。
        public bool TryRecordOperation(ProductionOperationData operation)
        {
            if (State != ProductionState.Active)
            {
                return false;
            }

            if (!Enum.IsDefined(typeof(ProductionStation), operation.Station) ||
                !Enum.IsDefined(typeof(ProductionInteractionMode), operation.InteractionMode))
            {
                throw new ArgumentOutOfRangeException(nameof(operation));
            }

            actualOperations.Add(operation);
            if (actualOperations.Count == MaxActualOperations)
            {
                FinishRun(ProductionRunEndReason.ThreeOperations);
            }

            PublishChange();
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (State != ProductionState.Active || deltaTime == 0f)
            {
                return;
            }

            double remaining = deltaTime;
            while (State == ProductionState.Active && remaining > 0d)
            {
                double available = runTimeLimit - runElapsedTime;
                if (remaining + TimeTolerance >= available)
                {
                    runElapsedTime = runTimeLimit;
                    totalElapsedTime += available;
                    remaining = Math.Max(0d, remaining - available);
                    FinishRun(ProductionRunEndReason.TimeExpired);
                }
                else
                {
                    runElapsedTime += remaining;
                    totalElapsedTime += remaining;
                    remaining = 0d;
                }
            }

            PublishChange();
        }

        public bool Cancel()
        {
            if (State != ProductionState.Active)
            {
                return false;
            }

            State = ProductionState.Cancelled;
            RunIndex = 0;
            runElapsedTime = 0d;
            totalElapsedTime = 0d;
            runResults.Clear();
            ResetActualOperations();
            Changed?.Invoke();
            return true;
        }

        private void FinishRun(ProductionRunEndReason reason)
        {
            runResults.Add(new ProductionRunResult(RunIndex, (float)runElapsedTime, reason, actualOperations));
            if (RunIndex == Dish.Runs.Count - 1)
            {
                State = ProductionState.Completed;
                Result = new ProductionResult(Dish, (float)totalElapsedTime, runResults);
                return;
            }

            RunIndex++;
            runElapsedTime = 0d;
            ResetActualOperations();
        }

        private void ResetActualOperations()
        {
            actualOperations = new List<ProductionOperationData>(MaxActualOperations);
            actualOperationsView = actualOperations.AsReadOnly();
        }

        private void PublishChange()
        {
            // 完成结果在通知 UI 前已生成，调用方可直接读取一致状态。
            Changed?.Invoke();
            if (State == ProductionState.Completed && !completionPublished)
            {
                completionPublished = true;
                Completed?.Invoke(Result);
            }
        }
    }
}
