using System;
using Cook.Core;
using UnityEngine;

namespace Cook.Configuration
{
    [CreateAssetMenu(menuName = "Cook/Production Operation")]
    public sealed class ProductionOperationDefinition : ScriptableObject
    {
        [SerializeField] private string operationId;
        [SerializeField] private ProductionStation station;
        [SerializeField] private ProductionInteractionMode interactionMode;
        [SerializeField] private Sprite icon;

        public string OperationId => operationId;
        public Sprite Icon => icon;

        public ProductionOperationData ToRuntime()
        {
            if (string.IsNullOrWhiteSpace(operationId))
            {
                throw new InvalidOperationException("Production operation id cannot be blank.");
            }

            if (!Enum.IsDefined(typeof(ProductionStation), station) ||
                !Enum.IsDefined(typeof(ProductionInteractionMode), interactionMode))
            {
                throw new InvalidOperationException($"{operationId} has an invalid station or interaction mode.");
            }

            return new ProductionOperationData(station, interactionMode);
        }
    }
}
