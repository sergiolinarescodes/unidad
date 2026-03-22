using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Fluent builder for spawning fully configured agent entities.
    /// Each .With*() method is optional — build only the modules you need.
    /// Handles archetype construction, buffer initialization, and strategy assignment.
    /// </summary>
    public struct AgentBuilder
    {
        EntityManager _em;
        int _agentId, _archetypeId, _strategyId;

        // Rendering
        bool _hasRenderable;
        int _meshId, _materialId;
        bool _hasColor;
        float4 _color;

        // Transform
        bool _hasTransform;
        float3 _position;
        quaternion _rotation;
        float _scale;

        // Navigation
        bool _hasNavigation;
        int _graphId;
        float _moveSpeed, _stoppingDistance;

        // Awareness
        bool _hasAwareness;
        float _awarenessRange;
        int _maxPOIs, _maxAgents;

        // Context
        bool _hasContext;
        ContextRefreshMode _contextMode;
        float _contextInterval;

        // Feedback
        bool _hasFeedback;

        // Schedule
        bool _hasSchedule;
        int _scheduleId;

        // Memory
        bool _hasMemory;
        int _maxMemories;
        float _memoryDecayRate;

        // Social
        bool _hasRelationships;

        // Needs
        struct NeedDef { public int ResourceId; public float Initial, Max, DecayRate, Critical, Low, High; }
        List<NeedDef> _needs;

        // Custom components
        List<ComponentType> _extraTypes;
        List<Action<EntityManager, Entity>> _extraSetters;

        public static AgentBuilder Create(EntityManager em)
        {
            return new AgentBuilder
            {
                _em = em,
                _agentId = -1,
                _archetypeId = 0,
                _strategyId = -1,
                _rotation = quaternion.identity,
                _scale = 1f,
                _color = new float4(1, 1, 1, 1),
                _moveSpeed = 3f,
                _stoppingDistance = 0.5f,
                _contextMode = ContextRefreshMode.EveryFrame
            };
        }

        public AgentBuilder WithArchetype(int archetypeId, int agentId)
        {
            _archetypeId = archetypeId;
            _agentId = agentId;
            return this;
        }

        public AgentBuilder WithStrategy(int strategyId)
        {
            _strategyId = strategyId;
            return this;
        }

        public AgentBuilder WithRenderable(int meshId, int materialId)
        {
            _hasRenderable = true;
            _meshId = meshId;
            _materialId = materialId;
            return this;
        }

        public AgentBuilder WithColor(float4 color)
        {
            _hasColor = true;
            _color = color;
            return this;
        }

        public AgentBuilder WithTransform(float3 position, quaternion rotation, float scale = 1f)
        {
            _hasTransform = true;
            _position = position;
            _rotation = rotation;
            _scale = scale;
            return this;
        }

        public AgentBuilder AtPosition(float3 position)
        {
            _hasTransform = true;
            _position = position;
            return this;
        }

        public AgentBuilder WithNavigation(int graphId, float moveSpeed = 3f, float stoppingDistance = 0.5f)
        {
            _hasNavigation = true;
            _graphId = graphId;
            _moveSpeed = moveSpeed;
            _stoppingDistance = stoppingDistance;
            return this;
        }

        public AgentBuilder WithAwareness(float range, int maxPOIs = 8, int maxAgents = 4)
        {
            _hasAwareness = true;
            _awarenessRange = range;
            _maxPOIs = maxPOIs;
            _maxAgents = maxAgents;
            return this;
        }

        public AgentBuilder WithContextRefresh(ContextRefreshMode mode, float refreshInterval = 0f)
        {
            _hasContext = true;
            _contextMode = mode;
            _contextInterval = refreshInterval;
            return this;
        }

        public AgentBuilder WithNeed(int resourceId, float initial, float max, float decayRate,
            float critical, float low, float high)
        {
            _needs ??= new List<NeedDef>(4);
            _needs.Add(new NeedDef
            {
                ResourceId = resourceId, Initial = initial, Max = max, DecayRate = decayRate,
                Critical = critical, Low = low, High = high
            });
            return this;
        }

        public AgentBuilder WithSchedule(int scheduleId)
        {
            _hasSchedule = true;
            _scheduleId = scheduleId;
            return this;
        }

        public AgentBuilder WithMemory(int maxMemories = 32, float decayRate = 0.01f)
        {
            _hasMemory = true;
            _maxMemories = maxMemories;
            _memoryDecayRate = decayRate;
            return this;
        }

        public AgentBuilder WithRelationships()
        {
            _hasRelationships = true;
            return this;
        }

        public AgentBuilder WithFeedback()
        {
            _hasFeedback = true;
            return this;
        }

        public AgentBuilder With<T>(T value) where T : unmanaged, IComponentData
        {
            _extraTypes ??= new List<ComponentType>(4);
            _extraSetters ??= new List<Action<EntityManager, Entity>>(4);
            _extraTypes.Add(ComponentType.ReadWrite<T>());
            _extraSetters.Add((em, entity) => em.SetComponentData(entity, value));
            return this;
        }

        public AgentBuilder WithTag<T>() where T : unmanaged, IComponentData
        {
            _extraTypes ??= new List<ComponentType>(4);
            _extraTypes.Add(ComponentType.ReadWrite<T>());
            return this;
        }

        public Entity Build()
        {
            var types = new NativeList<ComponentType>(64, Allocator.Temp);

            // Core (always present)
            types.Add(ComponentType.ReadWrite<AgentData>());
            types.Add(ComponentType.ReadWrite<AgentTarget>());
            types.Add(ComponentType.ReadWrite<AgentLocomotion>());
            types.Add(ComponentType.ReadWrite<AgentActivity>());
            types.Add(ComponentType.ReadWrite<LocalTransform>());
            types.Add(ComponentType.ReadWrite<LocalToWorld>());

            // Lifecycle events
            types.Add(ComponentType.ReadWrite<AgentSpawned>());
            types.Add(ComponentType.ReadWrite<AgentActivated>());
            types.Add(ComponentType.ReadWrite<AgentSuspended>());
            types.Add(ComponentType.ReadWrite<AgentDespawning>());
            types.Add(ComponentType.ReadWrite<ActivityChanged>());
            types.Add(ComponentType.ReadWrite<AgentIsSuspended>());

            // Strategy + Scoring (always present on agents)
            types.Add(ComponentType.ReadWrite<ScoringResult>());
            types.Add(ComponentType.ReadWrite<ActionSelectionChanged>());
            types.Add(ComponentType.ReadWrite<StrategyAssignRequest>());
            types.Add(ComponentType.ReadWrite<StrategyAssigned>());
            types.Add(ComponentType.ReadWrite<AgentActionState>());
            types.Add(ComponentType.ReadWrite<AgentPreconditions>());
            types.Add(ComponentType.ReadWrite<ActionStarted>());
            types.Add(ComponentType.ReadWrite<ActionCompleted>());
            types.Add(ComponentType.ReadWrite<ActionInterrupted>());
            types.Add(ComponentType.ReadWrite<ActionQueueConfig>());
            types.Add(ComponentType.ReadWrite<ActionQueueProgress>());
            types.Add(ComponentType.ReadWrite<QueueAdvanced>());
            types.Add(ComponentType.ReadWrite<QueueCompleted>());
            types.Add(ComponentType.ReadWrite<QueueInterrupted>());
            types.Add(ComponentType.ReadWrite<ForceRescoreTag>());
            types.Add(ComponentType.ReadWrite<StateMachineData>());
            types.Add(ComponentType.ReadWrite<StateEntered>());
            types.Add(ComponentType.ReadWrite<StateExited>());

            // Buffers
            types.Add(ComponentType.ReadWrite<ConsiderationElement>());
            types.Add(ComponentType.ReadWrite<ActionTimestampElement>());
            types.Add(ComponentType.ReadWrite<StrategyParamElement>());
            types.Add(ComponentType.ReadWrite<ActionEffectElement>());
            types.Add(ComponentType.ReadWrite<ActionCompletionRecord>());
            types.Add(ComponentType.ReadWrite<ActionQueueEntry>());
            types.Add(ComponentType.ReadWrite<AgentContextSnapshot>());

            // Resources (for needs)
            types.Add(ComponentType.ReadWrite<ResourceElement>());
            types.Add(ComponentType.ReadWrite<ResourceChangeRecord>());
            types.Add(ComponentType.ReadWrite<ResourceMaxModifier>());
            types.Add(ComponentType.ReadWrite<ResourceMinModifier>());
            types.Add(ComponentType.ReadWrite<ResourceChanged>());
            types.Add(ComponentType.ReadWrite<ResourceDepleted>());
            types.Add(ComponentType.ReadWrite<ResourceFilled>());

            // Needs
            types.Add(ComponentType.ReadWrite<NeedElement>());
            types.Add(ComponentType.ReadWrite<NeedDecayModifier>());
            types.Add(ComponentType.ReadWrite<NeedUrgencyChanged>());
            types.Add(ComponentType.ReadWrite<NeedUrgencyChangeRecord>());

            // Context
            types.Add(ComponentType.ReadWrite<ContextRefreshPolicy>());
            types.Add(ComponentType.ReadWrite<ContextRefreshRequest>());
            types.Add(ComponentType.ReadWrite<ContextRefreshed>());

            // Rendering
            if (_hasRenderable)
            {
                types.Add(ComponentType.ReadWrite<InstanceColor>());
                types.Add(ComponentType.ReadWrite<BaseColor>());
            }

            // Navigation
            if (_hasNavigation)
            {
                types.Add(ComponentType.ReadWrite<NavAgent>());
                types.Add(ComponentType.ReadWrite<PathRequest>());
                types.Add(ComponentType.ReadWrite<PathProgress>());
                types.Add(ComponentType.ReadWrite<PathNodeElement>());
                types.Add(ComponentType.ReadWrite<PathFound>());
                types.Add(ComponentType.ReadWrite<PathNotFound>());
                types.Add(ComponentType.ReadWrite<PathCompleted>());
                types.Add(ComponentType.ReadWrite<NavNodeReached>());
                types.Add(ComponentType.ReadWrite<PathInvalidated>());
            }

            // Awareness
            if (_hasAwareness)
            {
                types.Add(ComponentType.ReadWrite<AwarenessData>());
                types.Add(ComponentType.ReadWrite<KnownPOIElement>());
                types.Add(ComponentType.ReadWrite<KnownAgentElement>());
                types.Add(ComponentType.ReadWrite<POIClaim>());
                types.Add(ComponentType.ReadWrite<POIClaimRejected>());
                types.Add(ComponentType.ReadWrite<KnowledgeRefreshed>());
            }

            // Feedback
            if (_hasFeedback)
            {
                types.Add(ComponentType.ReadWrite<AgentFeedback>());
                types.Add(ComponentType.ReadWrite<ActionFeedbackElement>());
                types.Add(ComponentType.ReadWrite<FeedbackEvaluated>());
                types.Add(ComponentType.ReadWrite<StrategyUnderperforming>());
            }

            // Schedule
            if (_hasSchedule)
            {
                types.Add(ComponentType.ReadWrite<ScheduleData>());
                types.Add(ComponentType.ReadWrite<ScheduleSlotChanged>());
            }

            // Memory
            if (_hasMemory)
            {
                types.Add(ComponentType.ReadWrite<MemoryConfig>());
                types.Add(ComponentType.ReadWrite<MemoryElement>());
                types.Add(ComponentType.ReadWrite<MemoryAdded>());
                types.Add(ComponentType.ReadWrite<MemoryForgotten>());
            }

            // Social
            if (_hasRelationships)
            {
                types.Add(ComponentType.ReadWrite<InteractionRequest>());
                types.Add(ComponentType.ReadWrite<InteractionResponse>());
                types.Add(ComponentType.ReadWrite<InteractionState>());
                types.Add(ComponentType.ReadWrite<RelationshipElement>());
                types.Add(ComponentType.ReadWrite<InteractionStarted>());
                types.Add(ComponentType.ReadWrite<InteractionCompleted>());
                types.Add(ComponentType.ReadWrite<InteractionRejected>());
            }

            // Custom
            if (_extraTypes != null)
                foreach (var t in _extraTypes) types.Add(t);

            var archetype = _em.CreateArchetype(types.AsArray());
            var entity = _em.CreateEntity(archetype);
            types.Dispose();

            // Disable all enableable tags (they default to enabled on creation)
            _em.SetComponentEnabled<AgentSpawned>(entity, false);
            _em.SetComponentEnabled<AgentActivated>(entity, false);
            _em.SetComponentEnabled<AgentSuspended>(entity, false);
            _em.SetComponentEnabled<AgentDespawning>(entity, false);
            _em.SetComponentEnabled<ActivityChanged>(entity, false);
            _em.SetComponentEnabled<AgentIsSuspended>(entity, false);
            _em.SetComponentEnabled<ActionSelectionChanged>(entity, false);
            _em.SetComponentEnabled<StrategyAssignRequest>(entity, false);
            _em.SetComponentEnabled<StrategyAssigned>(entity, false);
            _em.SetComponentEnabled<ActionStarted>(entity, false);
            _em.SetComponentEnabled<ActionCompleted>(entity, false);
            _em.SetComponentEnabled<ActionInterrupted>(entity, false);
            _em.SetComponentEnabled<QueueAdvanced>(entity, false);
            _em.SetComponentEnabled<QueueCompleted>(entity, false);
            _em.SetComponentEnabled<QueueInterrupted>(entity, false);
            _em.SetComponentEnabled<ForceRescoreTag>(entity, false);
            _em.SetComponentEnabled<NeedUrgencyChanged>(entity, false);
            _em.SetComponentEnabled<ContextRefreshRequest>(entity, false);
            _em.SetComponentEnabled<ContextRefreshed>(entity, false);
            _em.SetComponentEnabled<ResourceChanged>(entity, false);
            _em.SetComponentEnabled<ResourceDepleted>(entity, false);
            _em.SetComponentEnabled<ResourceFilled>(entity, false);

            var pos = _hasTransform ? _position : float3.zero;
            var rot = _hasTransform ? _rotation : quaternion.identity;
            var scl = _hasTransform ? _scale : 1f;

            _em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(pos, rot, scl));
            _em.SetComponentData(entity, new LocalToWorld
            {
                Value = float4x4.TRS(pos, rot, new float3(scl))
            });

            _em.SetComponentData(entity, new AgentData
            {
                AgentId = _agentId,
                ArchetypeId = _archetypeId,
                StrategyId = _strategyId,
                LifecycleState = AgentLifecycleState.Initializing
            });

            _em.SetComponentData(entity, new AgentLocomotion
            {
                BaseMoveSpeed = _moveSpeed,
                CurrentMoveSpeed = _moveSpeed,
                StoppingDistance = _stoppingDistance
            });

            _em.SetComponentData(entity, new AgentActionState { CurrentActionId = -1 });
            _em.SetComponentData(entity, ActionQueueConfig.Default);
            _em.SetComponentData(entity, new ScoringResult { BestActionId = -1, PreviousBestActionId = -1 });

            _em.SetComponentData(entity, new ContextRefreshPolicy
            {
                Mode = _hasContext ? _contextMode : ContextRefreshMode.EveryFrame,
                RefreshInterval = _contextInterval
            });

            // Rendering
            if (_hasRenderable)
            {
                _em.AddSharedComponentManaged(entity, new InstanceRenderable
                {
                    MeshId = _meshId, MaterialId = _materialId
                });
                var c = _hasColor ? _color : new float4(1, 1, 1, 1);
                _em.SetComponentData(entity, new InstanceColor { Value = c });
                _em.SetComponentData(entity, new BaseColor { Value = c });
            }

            // Navigation
            if (_hasNavigation)
            {
                _em.SetComponentData(entity, new NavAgent
                {
                    GraphId = _graphId,
                    CurrentNodeId = -1,
                    Status = NavAgentStatus.Idle
                });
                _em.SetComponentEnabled<PathRequest>(entity, false);
                _em.SetComponentEnabled<PathFound>(entity, false);
                _em.SetComponentEnabled<PathNotFound>(entity, false);
                _em.SetComponentEnabled<PathCompleted>(entity, false);
                _em.SetComponentEnabled<NavNodeReached>(entity, false);
                _em.SetComponentEnabled<PathInvalidated>(entity, false);
            }

            // Awareness
            if (_hasAwareness)
            {
                _em.SetComponentData(entity, new AwarenessData
                {
                    AwarenessRange = _awarenessRange,
                    SpatialHashCellSize = 10f,
                    MaxKnownPOIs = _maxPOIs,
                    MaxKnownAgents = _maxAgents
                });
                _em.SetComponentEnabled<POIClaimRejected>(entity, false);
                _em.SetComponentEnabled<KnowledgeRefreshed>(entity, false);
            }

            // Needs
            if (_needs != null && _needs.Count > 0)
            {
                var resources = _em.GetBuffer<ResourceElement>(entity);
                var needBuffer = _em.GetBuffer<NeedElement>(entity);

                for (int i = 0; i < _needs.Count; i++)
                {
                    var n = _needs[i];
                    resources.Add(new ResourceElement
                    {
                        ResourceId = n.ResourceId,
                        CurrentValue = n.Initial,
                        InitialValue = n.Initial,
                        BaseMin = 0f,
                        BaseMax = n.Max
                    });
                    needBuffer.Add(new NeedElement
                    {
                        ResourceId = n.ResourceId,
                        DecayRate = n.DecayRate,
                        CriticalThreshold = n.Critical,
                        LowThreshold = n.Low,
                        HighThreshold = n.High,
                        CurrentUrgency = NeedUtility.EvaluateUrgency(n.Initial, n.Critical, n.Low, n.High)
                    });
                }
            }

            // Feedback
            if (_hasFeedback)
            {
                _em.SetComponentEnabled<FeedbackEvaluated>(entity, false);
                _em.SetComponentEnabled<StrategyUnderperforming>(entity, false);
            }

            // Schedule
            if (_hasSchedule)
            {
                _em.SetComponentData(entity, new ScheduleData
                {
                    ScheduleId = _scheduleId,
                    CurrentSlotIndex = -1
                });
                _em.SetComponentEnabled<ScheduleSlotChanged>(entity, false);
            }

            // Memory
            if (_hasMemory)
            {
                _em.SetComponentData(entity, new MemoryConfig
                {
                    MaxMemories = _maxMemories,
                    DecayRate = _memoryDecayRate,
                    ImportanceThreshold = 0.05f
                });
                _em.SetComponentEnabled<MemoryAdded>(entity, false);
                _em.SetComponentEnabled<MemoryForgotten>(entity, false);
            }

            // Social
            if (_hasRelationships)
            {
                _em.SetComponentData(entity, new InteractionState { Phase = InteractionPhase.None });
                _em.SetComponentEnabled<InteractionRequest>(entity, false);
                _em.SetComponentEnabled<InteractionResponse>(entity, false);
                _em.SetComponentEnabled<InteractionStarted>(entity, false);
                _em.SetComponentEnabled<InteractionCompleted>(entity, false);
                _em.SetComponentEnabled<InteractionRejected>(entity, false);
            }

            // Strategy assignment request
            if (_strategyId >= 0)
            {
                _em.SetComponentData(entity, new StrategyAssignRequest { StrategyId = _strategyId });
                _em.SetComponentEnabled<StrategyAssignRequest>(entity, true);
            }

            _em.SetComponentEnabled<AgentSpawned>(entity, true);

            if (_extraSetters != null)
                foreach (var setter in _extraSetters) setter(_em, entity);

            return entity;
        }
    }
}
