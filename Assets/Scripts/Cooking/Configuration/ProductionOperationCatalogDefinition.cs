using System;
using System.Collections.Generic;
using Cook.Core;
using UnityEngine;

namespace Cook.Configuration
{
    [CreateAssetMenu(menuName = "Cook/Production Operation Catalog")]
    public sealed class ProductionOperationCatalogDefinition : ScriptableObject
    {
        [SerializeField] private List<ProductionOperationDefinition> operations =
            new List<ProductionOperationDefinition>();

        public IReadOnlyList<ProductionOperationDefinition> Operations => operations;

        public bool TryValidate(out string error)
        {
            if (operations == null || operations.Count != 9)
            {
                error = "Production catalog must contain all nine station and interaction combinations.";
                return false;
            }

            var ids = new HashSet<string>();
            var combinations = new HashSet<ProductionOperationData>();
            for (int i = 0; i < operations.Count; i++)
            {
                ProductionOperationDefinition definition = operations[i];
                if (definition == null)
                {
                    error = $"Production operation {i + 1} is missing.";
                    return false;
                }

                ProductionOperationData operation;
                try
                {
                    operation = definition.ToRuntime();
                }
                catch (Exception exception)
                {
                    error = $"Production operation {i + 1}: {exception.Message}";
                    return false;
                }

                if (!ids.Add(definition.OperationId) || !combinations.Add(operation))
                {
                    error = $"Duplicate production operation: {definition.OperationId}.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
