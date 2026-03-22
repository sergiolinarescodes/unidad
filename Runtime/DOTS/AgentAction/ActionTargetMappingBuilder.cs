using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Fluent builder for creating the ActionTargetMapping singleton entity.
    /// Configures how the framework's ActionBridgeSystem maps ActionTypes to
    /// POI targets and execution durations.
    ///
    /// Usage:
    ///   ActionTargetMappingBuilder.Create(em)
    ///       .WithConfig(lockScoring: true, defaultDuration: 3f)
    ///       .Map(ActionTypeFarm,   POIFarm,   duration: 5f)
    ///       .Map(ActionTypeGather, POIForest, duration: 4f)
    ///       .MapInPlace(ActionTypeIdle, duration: 3f)
    ///       .Build();
    /// </summary>
    public struct ActionTargetMappingBuilder
    {
        EntityManager _em;
        ActionBridgeConfig _config;
        List<ActionTargetMappingElement> _mappings;

        public static ActionTargetMappingBuilder Create(EntityManager em)
        {
            return new ActionTargetMappingBuilder
            {
                _em = em,
                _config = ActionBridgeConfig.Default,
                _mappings = new List<ActionTargetMappingElement>(16)
            };
        }

        public ActionTargetMappingBuilder WithConfig(
            bool lockScoring = true, float defaultDuration = 3f)
        {
            _config.LockScoringDuringExecution = lockScoring;
            _config.DefaultInPlaceDuration = defaultDuration;
            return this;
        }

        /// <summary>Map an ActionType to navigate to a POI type, then execute for duration.</summary>
        public ActionTargetMappingBuilder Map(
            int actionType, int poiType, float duration, bool handledByFramework = true)
        {
            _mappings.Add(new ActionTargetMappingElement
            {
                ActionType = actionType,
                TargetPOIType = poiType,
                ExecutionDuration = duration,
                HandledByFramework = handledByFramework
            });
            return this;
        }

        /// <summary>Map an ActionType to execute in place (no navigation needed).</summary>
        public ActionTargetMappingBuilder MapInPlace(
            int actionType, float duration, bool handledByFramework = true)
        {
            _mappings.Add(new ActionTargetMappingElement
            {
                ActionType = actionType,
                TargetPOIType = -1,
                ExecutionDuration = duration,
                HandledByFramework = handledByFramework
            });
            return this;
        }

        public Entity Build()
        {
            var types = new NativeList<ComponentType>(4, Allocator.Temp);
            types.Add(ComponentType.ReadWrite<ActionBridgeConfig>());
            types.Add(ComponentType.ReadWrite<ActionTargetMappingElement>());

            var archetype = _em.CreateArchetype(types.AsArray());
            var entity = _em.CreateEntity(archetype);
            types.Dispose();

            _em.SetComponentData(entity, _config);

            var buffer = _em.GetBuffer<ActionTargetMappingElement>(entity);
            for (int i = 0; i < _mappings.Count; i++)
                buffer.Add(_mappings[i]);

            return entity;
        }
    }
}
