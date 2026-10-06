using System.Collections.Generic;

namespace Cook.Core
{
    public enum ProductionRunEndReason { ThreeOperations, TimeExpired }

    public sealed class ProductionRunResult
    {
        internal ProductionRunResult(int runIndex, float elapsedTime,
            ProductionRunEndReason endReason, IReadOnlyList<ProductionOperationData> actualOperations)
        {
            RunIndex = runIndex;
            ElapsedTime = elapsedTime;
            EndReason = endReason;
            var copy = new List<ProductionOperationData>(actualOperations.Count);
            for (int i = 0; i < actualOperations.Count; i++)
            {
                copy.Add(actualOperations[i]);
            }

            ActualOperations = copy.AsReadOnly();
        }

        public int RunIndex { get; }
        public float ElapsedTime { get; }
        public ProductionRunEndReason EndReason { get; }
        public IReadOnlyList<ProductionOperationData> ActualOperations { get; }
    }

    // 保留指引与实际操作，后续偏离比较和定价只消费这份结果。
    public sealed class ProductionResult
    {
        internal ProductionResult(DishRuntimeData dish, float totalElapsedTime,
            IReadOnlyList<ProductionRunResult> runs)
        {
            Dish = dish;
            TotalElapsedTime = totalElapsedTime;
            var copy = new List<ProductionRunResult>(runs.Count);
            for (int i = 0; i < runs.Count; i++)
            {
                copy.Add(runs[i]);
            }

            Runs = copy.AsReadOnly();
        }

        public DishRuntimeData Dish { get; }
        public float TotalElapsedTime { get; }
        public IReadOnlyList<ProductionRunResult> Runs { get; }
    }
}
