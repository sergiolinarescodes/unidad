using Unidad.Core.EventBus;
using Unity.Entities;

namespace Unidad.Core.DOTS.Bridge
{
    /// <summary>
    /// Managed system that bridges ECS enableable event tags to the managed IEventBus.
    /// Only needed in hybrid scenarios where MonoBehaviour code consumes ECS events.
    /// Not Burst-compiled — this is the only file referencing Unidad.Core.Runtime.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class EventBusBridgeSystem : SystemBase
    {
        IEventBus _eventBus;

        public void SetEventBus(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        protected override void OnUpdate()
        {
            if (_eventBus == null)
                return;

            BridgeTimerEvents();
            BridgeResourceEvents();
            BridgeStateMachineEvents();
            BridgeCommandQueueEvents();
        }

        void BridgeTimerEvents()
        {
            foreach (var (timer, owner) in
                SystemAPI.Query<RefRO<TimerData>, RefRO<TimerOwner>>()
                    .WithAll<TimerCompleted>())
            {
                _eventBus.Publish(new BridgedTimerCompleted
                {
                    OwnerEntity = owner.ValueRO.Owner,
                    Duration = timer.ValueRO.Duration
                });
            }
        }

        void BridgeResourceEvents()
        {
            foreach (var (changes, entity) in
                SystemAPI.Query<DynamicBuffer<ResourceChangeRecord>>()
                    .WithAll<ResourceChanged>()
                    .WithEntityAccess())
            {
                for (int i = 0; i < changes.Length; i++)
                {
                    var record = changes[i];
                    _eventBus.Publish(new BridgedResourceChanged
                    {
                        Entity = entity,
                        ResourceId = record.ResourceId,
                        OldValue = record.OldValue,
                        NewValue = record.NewValue
                    });
                }
            }

            foreach (var (changes, entity) in
                SystemAPI.Query<DynamicBuffer<ResourceChangeRecord>>()
                    .WithAll<ResourceDepleted>()
                    .WithEntityAccess())
            {
                for (int i = 0; i < changes.Length; i++)
                {
                    _eventBus.Publish(new BridgedResourceDepleted
                    {
                        Entity = entity,
                        ResourceId = changes[i].ResourceId
                    });
                }
            }
        }

        void BridgeStateMachineEvents()
        {
            foreach (var (sm, entity) in
                SystemAPI.Query<RefRO<StateMachineData>>()
                    .WithAll<StateEntered>()
                    .WithEntityAccess())
            {
                _eventBus.Publish(new BridgedStateEntered
                {
                    Entity = entity,
                    CurrentState = sm.ValueRO.CurrentState,
                    PreviousState = sm.ValueRO.PreviousState
                });
            }
        }

        void BridgeCommandQueueEvents()
        {
            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<CommandQueueData>>()
                    .WithAll<QueueEmpty>()
                    .WithEntityAccess())
            {
                _eventBus.Publish(new BridgedQueueEmpty { Entity = entity });
            }
        }
    }

    // Bridged event structs — consumed by MonoBehaviour code via IEventBus
    public struct BridgedTimerCompleted
    {
        public Entity OwnerEntity;
        public float Duration;
    }

    public struct BridgedResourceChanged
    {
        public Entity Entity;
        public int ResourceId;
        public float OldValue;
        public float NewValue;
    }

    public struct BridgedResourceDepleted
    {
        public Entity Entity;
        public int ResourceId;
    }

    public struct BridgedStateEntered
    {
        public Entity Entity;
        public int CurrentState;
        public int PreviousState;
    }

    public struct BridgedQueueEmpty
    {
        public Entity Entity;
    }
}
