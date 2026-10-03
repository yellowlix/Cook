using System;
using System.Collections.Generic;
using Cook.Core;
using UnityEngine;

namespace Cook.Configuration
{
    [CreateAssetMenu(menuName = "Cook/Recipe Definition")]
    // 菜谱资源只负责编辑器配置，所有运行时判定由 CookingSession 执行。
    public sealed class RecipeDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class RoundDefinition
        {
            [SerializeField] private List<OperationDefinition> operations = new List<OperationDefinition>();
            [SerializeField] private bool useCustomTimeThresholds;
            [SerializeField] private float excellentTime;
            [SerializeField] private float greatTime;
            [SerializeField] private float goodTime;

            internal IReadOnlyList<OperationDefinition> Operations => operations;
            internal bool UseCustomTimeThresholds => useCustomTimeThresholds;
            internal float ExcellentTime => excellentTime;
            internal float GreatTime => greatTime;
            internal float GoodTime => goodTime;
        }

        [SerializeField] private string recipeId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField, Min(0f)] private float stationChangeAllowance = 0.25f;
        [SerializeField] private List<RoundDefinition> rounds = new List<RoundDefinition>();

        public Sprite Icon => icon;
        public string RecipeId => recipeId;
        public string DisplayName => displayName;

        public bool TryBuildRuntime(out RecipeRuntimeData runtime, out string error)
        {
            runtime = null;
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                error = "Recipe id cannot be blank.";
                return false;
            }

            if (rounds == null || rounds.Count == 0)
            {
                error = "Recipe must contain at least one round.";
                return false;
            }

            var runtimeRounds = new List<RoundRuntimeData>(rounds.Count);
            for (int roundIndex = 0; roundIndex < rounds.Count; roundIndex++)
            {
                RoundDefinition definition = rounds[roundIndex];
                if (definition == null)
                {
                    error = $"Round {roundIndex + 1} is null.";
                    return false;
                }

                if (definition.Operations == null || definition.Operations.Count < 2 || definition.Operations.Count > 3)
                {
                    error = $"Round {roundIndex + 1} must contain 2 or 3 operations.";
                    return false;
                }

                var operations = new List<OperationRuntimeData>(definition.Operations.Count);
                for (int operationIndex = 0; operationIndex < definition.Operations.Count; operationIndex++)
                {
                    OperationDefinition operation = definition.Operations[operationIndex];
                    if (operation == null)
                    {
                        error = $"Round {roundIndex + 1} operation {operationIndex + 1} is null.";
                        return false;
                    }

                    try
                    {
                        operations.Add(operation.ToRuntime());
                    }
                    catch (Exception exception)
                    {
                        error = $"Round {roundIndex + 1} operation {operationIndex + 1}: {exception.Message}";
                        return false;
                    }
                }

                float excellent;
                float great;
                float good;
                if (definition.UseCustomTimeThresholds)
                {
                    excellent = definition.ExcellentTime;
                    great = definition.GreatTime;
                    good = definition.GoodTime;
                    if (excellent <= 0f || excellent > great || great > good)
                    {
                        error = $"Round {roundIndex + 1} thresholds must satisfy " +
                                "0 < Excellent <= Great <= Good.";
                        return false;
                    }
                }
                else
                {
                    float standardTime = 0f;
                    for (int i = 0; i < operations.Count; i++)
                    {
                        standardTime += operations[i].StandardDuration;
                        if (i > 0 && operations[i - 1].Station != operations[i].Station)
                        {
                            standardTime += stationChangeAllowance;
                        }
                    }

                    excellent = standardTime * 0.85f;
                    great = standardTime;
                    good = standardTime * 1.4f;
                }

                runtimeRounds.Add(new RoundRuntimeData(operations, excellent, great, good));
            }

            runtime = new RecipeRuntimeData(recipeId, displayName, runtimeRounds);
            error = string.Empty;
            return true;
        }

        private void OnValidate()
        {
            stationChangeAllowance = Mathf.Max(0f, stationChangeAllowance);
        }
    }
}
