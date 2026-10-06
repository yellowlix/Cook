using System.Collections.Generic;
using Cook.Core;
using UnityEngine;

namespace Cook.Configuration
{
    [CreateAssetMenu(menuName = "Cook/Dish Catalog")]
    public sealed class DishCatalogDefinition : ScriptableObject
    {
        [SerializeField] private List<DishDefinition> dishes = new List<DishDefinition>();

        public IReadOnlyList<DishDefinition> Dishes => dishes;

        public bool TryBuildRuntime(out IReadOnlyList<DishRuntimeData> runtime, out string error)
        {
            runtime = null;
            if (dishes == null || dishes.Count == 0)
            {
                error = "Dish catalog cannot be empty.";
                return false;
            }

            var result = new List<DishRuntimeData>(dishes.Count);
            var ids = new HashSet<string>();
            for (int i = 0; i < dishes.Count; i++)
            {
                DishDefinition dish = dishes[i];
                if (dish == null)
                {
                    error = $"Dish {i + 1} is missing.";
                    return false;
                }

                if (!dish.TryBuildRuntime(out DishRuntimeData data, out error))
                {
                    error = $"Dish {i + 1}: {error}";
                    return false;
                }

                if (!ids.Add(data.Id))
                {
                    error = $"Duplicate dish id: {data.Id}.";
                    return false;
                }

                result.Add(data);
            }

            runtime = result.AsReadOnly();
            error = string.Empty;
            return true;
        }
    }
}
