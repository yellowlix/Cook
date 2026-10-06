using System;
using System.Collections.Generic;
using Cook.Core;
using UnityEngine;

namespace Cook.Configuration
{
    [CreateAssetMenu(menuName = "Cook/Dish Definition")]
    public sealed class DishDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class RunDefinition
        {
            [SerializeField] private List<ProductionOperationDefinition> guide =
                new List<ProductionOperationDefinition>();

            public IReadOnlyList<ProductionOperationDefinition> Guide => guide;
        }

        [SerializeField] private string dishId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField, Min(1)] private int requiredShopLevel = 1;
        [SerializeField] private List<RunDefinition> runs = new List<RunDefinition>();

        public string DishId => dishId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int RequiredShopLevel => requiredShopLevel;

        public bool TryBuildRuntime(out DishRuntimeData runtime, out string error)
        {
            runtime = null;
            if (string.IsNullOrWhiteSpace(dishId))
            {
                error = "Dish id cannot be blank.";
                return false;
            }

            if (runs == null || runs.Count < 3 || runs.Count > 7)
            {
                error = "A dish must contain three to seven runs.";
                return false;
            }

            var runtimeRuns = new List<ProductionRunData>(runs.Count);
            for (int runIndex = 0; runIndex < runs.Count; runIndex++)
            {
                RunDefinition run = runs[runIndex];
                if (run == null || run.Guide == null || run.Guide.Count > 3)
                {
                    error = $"Run {runIndex + 1} must guide zero to three operations.";
                    return false;
                }

                var guide = new List<ProductionOperationData>(run.Guide.Count);
                for (int operationIndex = 0; operationIndex < run.Guide.Count; operationIndex++)
                {
                    ProductionOperationDefinition definition = run.Guide[operationIndex];
                    if (definition == null)
                    {
                        error = $"Run {runIndex + 1} operation {operationIndex + 1} is missing.";
                        return false;
                    }

                    try
                    {
                        guide.Add(definition.ToRuntime());
                    }
                    catch (Exception exception)
                    {
                        error = $"Run {runIndex + 1} operation {operationIndex + 1}: {exception.Message}";
                        return false;
                    }
                }

                runtimeRuns.Add(new ProductionRunData(guide));
            }

            try
            {
                runtime = new DishRuntimeData(dishId, displayName, requiredShopLevel, runtimeRuns);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
