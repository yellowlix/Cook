using System;
using System.Collections.Generic;

namespace Cook.Core
{
    public enum ProductionStation { SoupPot, CuttingBoard, FryingPan }
    public enum ProductionInteractionMode { Click, TripleClick, Hold }

    public readonly struct ProductionOperationData
    {
        public ProductionOperationData(ProductionStation station, ProductionInteractionMode interactionMode)
        {
            Station = station;
            InteractionMode = interactionMode;
        }

        public ProductionStation Station { get; }
        public ProductionInteractionMode InteractionMode { get; }
    }

    public sealed class ProductionRunData
    {
        public ProductionRunData(IReadOnlyList<ProductionOperationData> guide)
        {
            if (guide == null)
            {
                throw new ArgumentNullException(nameof(guide));
            }

            if (guide.Count > 3)
            {
                throw new ArgumentException("A run can guide at most three operations.", nameof(guide));
            }

            var copy = new List<ProductionOperationData>(guide.Count);
            for (int i = 0; i < guide.Count; i++)
            {
                copy.Add(guide[i]);
            }

            Guide = copy.AsReadOnly();
        }

        public IReadOnlyList<ProductionOperationData> Guide { get; }
    }

    public sealed class DishRuntimeData
    {
        public DishRuntimeData(string id, string displayName, int requiredShopLevel,
            IReadOnlyList<ProductionRunData> runs)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Dish id cannot be blank.", nameof(id));
            }

            if (requiredShopLevel < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredShopLevel));
            }

            if (runs == null)
            {
                throw new ArgumentNullException(nameof(runs));
            }

            if (runs.Count < 3 || runs.Count > 7)
            {
                throw new ArgumentException("A dish must contain three to seven runs.", nameof(runs));
            }

            var copy = new List<ProductionRunData>(runs.Count);
            for (int i = 0; i < runs.Count; i++)
            {
                if (runs[i] == null)
                {
                    throw new ArgumentException($"Run {i + 1} cannot be null.", nameof(runs));
                }

                copy.Add(runs[i]);
            }

            Id = id;
            DisplayName = displayName;
            RequiredShopLevel = requiredShopLevel;
            Runs = copy.AsReadOnly();
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int RequiredShopLevel { get; }
        public IReadOnlyList<ProductionRunData> Runs { get; }
        public float RunTimeLimit => 60f / Runs.Count;
    }
}
