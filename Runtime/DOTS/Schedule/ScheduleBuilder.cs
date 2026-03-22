using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Fluent builder for creating schedule definition entities.
    /// </summary>
    public struct ScheduleBuilder
    {
        EntityManager _em;
        int _scheduleId;
        FixedString64Bytes _debugName;
        List<ScheduleSlotElement> _slots;

        public static ScheduleBuilder Create(EntityManager em)
        {
            return new ScheduleBuilder
            {
                _em = em,
                _slots = new List<ScheduleSlotElement>(8)
            };
        }

        public ScheduleBuilder WithId(int scheduleId, string debugName = "")
        {
            _scheduleId = scheduleId;
            _debugName = new FixedString64Bytes(debugName);
            return this;
        }

        public ScheduleBuilder AddSlot(float startHour, float endHour, int stateId,
            int strategyOverride = -1, int priorityAction = -1)
        {
            _slots.Add(new ScheduleSlotElement
            {
                StartTime = startHour,
                EndTime = endHour,
                RequiredStateId = stateId,
                StrategyOverrideId = strategyOverride,
                PriorityActionId = priorityAction
            });
            return this;
        }

        public Entity Build()
        {
            var types = new NativeList<ComponentType>(4, Allocator.Temp);
            types.Add(ComponentType.ReadWrite<ScheduleDefinition>());
            types.Add(ComponentType.ReadWrite<ScheduleSlotElement>());

            var archetype = _em.CreateArchetype(types.AsArray());
            var entity = _em.CreateEntity(archetype);
            types.Dispose();

            _em.SetComponentData(entity, new ScheduleDefinition
            {
                ScheduleId = _scheduleId,
                DebugName = _debugName
            });

            var slots = _em.GetBuffer<ScheduleSlotElement>(entity);
            for (int i = 0; i < _slots.Count; i++)
                slots.Add(_slots[i]);

            return entity;
        }
    }
}
