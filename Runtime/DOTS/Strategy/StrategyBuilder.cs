using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Fluent builder for creating strategy definition entities.
    /// Strategies define which actions an agent can take and how they are scored.
    /// </summary>
    public struct StrategyBuilder
    {
        EntityManager _em;
        int _strategyId;
        FixedString64Bytes _debugName;

        struct ActionDef
        {
            public int ActionId, ActionType, PreconditionFlags;
            public float PriorityBonus, Cooldown;
        }

        struct ConsiderationDef
        {
            public int ActionId, InputParam;
            public ScoringInputType InputType;
            public ResponseCurveType CurveType;
            public float A, B, C, D;
        }

        struct EffectDef
        {
            public int ActionId, TargetResourceId;
            public ActionEffectType EffectType;
            public float Value;
        }

        struct ParamDef { public int ParamId; public float Value; }

        List<ActionDef> _actions;
        List<ConsiderationDef> _considerations;
        List<EffectDef> _effects;
        List<ParamDef> _params;

        // Current action being defined
        int _currentActionId;

        public static StrategyBuilder Create(EntityManager em)
        {
            return new StrategyBuilder
            {
                _em = em,
                _actions = new List<ActionDef>(8),
                _considerations = new List<ConsiderationDef>(16),
                _effects = new List<EffectDef>(8),
                _params = new List<ParamDef>(4),
                _currentActionId = -1
            };
        }

        public StrategyBuilder WithId(int strategyId, string debugName = "")
        {
            _strategyId = strategyId;
            _debugName = new FixedString64Bytes(debugName);
            return this;
        }

        public StrategyBuilder DefineAction(int actionId, int actionType,
            float priorityBonus = 0f, float cooldown = 0f, int preconditionFlags = 0)
        {
            _currentActionId = actionId;
            _actions.Add(new ActionDef
            {
                ActionId = actionId,
                ActionType = actionType,
                PriorityBonus = priorityBonus,
                Cooldown = cooldown,
                PreconditionFlags = preconditionFlags
            });
            return this;
        }

        public StrategyBuilder WithConsideration(ScoringInputType inputType, int inputParam,
            ResponseCurveType curveType, float a = 1f, float b = 0f, float c = 0f, float d = 0f)
        {
            _considerations.Add(new ConsiderationDef
            {
                ActionId = _currentActionId,
                InputType = inputType,
                InputParam = inputParam,
                CurveType = curveType,
                A = a, B = b, C = c, D = d
            });
            return this;
        }

        public StrategyBuilder WithEffect(ActionEffectType effectType, int targetResourceId, float value)
        {
            _effects.Add(new EffectDef
            {
                ActionId = _currentActionId,
                EffectType = effectType,
                TargetResourceId = targetResourceId,
                Value = value
            });
            return this;
        }

        public StrategyBuilder WithCooldown(float cooldown)
        {
            if (_actions.Count > 0)
            {
                var last = _actions[_actions.Count - 1];
                last.Cooldown = cooldown;
                _actions[_actions.Count - 1] = last;
            }
            return this;
        }

        public StrategyBuilder EndAction()
        {
            _currentActionId = -1;
            return this;
        }

        public StrategyBuilder WithParam(int paramId, float value)
        {
            _params.Add(new ParamDef { ParamId = paramId, Value = value });
            return this;
        }

        public Entity Build()
        {
            var types = new NativeList<ComponentType>(16, Allocator.Temp);
            types.Add(ComponentType.ReadWrite<StrategyDefinition>());
            types.Add(ComponentType.ReadWrite<StrategyActionElement>());
            types.Add(ComponentType.ReadWrite<StrategyConsiderationTemplate>());
            types.Add(ComponentType.ReadWrite<StrategyActionEffectTemplate>());
            types.Add(ComponentType.ReadWrite<StrategyParamElement>());
            types.Add(ComponentType.ReadWrite<StrategyActionPlanEntry>());

            var archetype = _em.CreateArchetype(types.AsArray());
            var entity = _em.CreateEntity(archetype);
            types.Dispose();

            _em.SetComponentData(entity, new StrategyDefinition
            {
                StrategyId = _strategyId,
                DebugName = _debugName
            });

            // Actions
            var actionBuf = _em.GetBuffer<StrategyActionElement>(entity);
            for (int i = 0; i < _actions.Count; i++)
            {
                var a = _actions[i];
                actionBuf.Add(new StrategyActionElement
                {
                    ActionId = a.ActionId,
                    ActionType = a.ActionType,
                    PriorityBonus = a.PriorityBonus,
                    Cooldown = a.Cooldown,
                    PreconditionFlags = a.PreconditionFlags
                });
            }

            // Considerations (sorted by ActionId for contiguous-run processing)
            _considerations.Sort((a, b) => a.ActionId.CompareTo(b.ActionId));
            var consBuf = _em.GetBuffer<StrategyConsiderationTemplate>(entity);
            for (int i = 0; i < _considerations.Count; i++)
            {
                var c = _considerations[i];
                consBuf.Add(new StrategyConsiderationTemplate
                {
                    ActionId = c.ActionId,
                    InputType = c.InputType,
                    InputParam = c.InputParam,
                    CurveType = c.CurveType,
                    CurveA = c.A, CurveB = c.B, CurveC = c.C, CurveD = c.D
                });
            }

            // Effects
            var effectBuf = _em.GetBuffer<StrategyActionEffectTemplate>(entity);
            for (int i = 0; i < _effects.Count; i++)
            {
                var e = _effects[i];
                effectBuf.Add(new StrategyActionEffectTemplate
                {
                    ActionId = e.ActionId,
                    EffectType = e.EffectType,
                    TargetResourceId = e.TargetResourceId,
                    Value = e.Value
                });
            }

            // Default params
            var paramBuf = _em.GetBuffer<StrategyParamElement>(entity);
            for (int i = 0; i < _params.Count; i++)
            {
                paramBuf.Add(new StrategyParamElement
                {
                    ParamId = _params[i].ParamId,
                    Value = _params[i].Value
                });
            }

            return entity;
        }
    }
}
