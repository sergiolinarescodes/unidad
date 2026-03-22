using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Push-model event subscription system. Checks which event types fired this frame,
    /// then applies reactions to agents subscribed to those events.
    ///
    /// Runs after all event-producing systems, before AgentActivitySystem.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct EventSubscriptionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EventSubscriptionElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;

            foreach (var (subscriptions, entity) in
                SystemAPI.Query<DynamicBuffer<EventSubscriptionElement>>()
                    .WithEntityAccess())
            {
                for (int i = 0; i < subscriptions.Length; i++)
                {
                    var sub = subscriptions[i];
                    if (!EventFired(em, entity, sub.EventType, sub.EventParam))
                        continue;

                    ApplyReaction(em, entity, sub.Reaction, sub.EventParam);
                }
            }
        }

        bool EventFired(EntityManager em, Entity entity, AgentEventType eventType, int param)
        {
            switch (eventType)
            {
                case AgentEventType.ResourceDepleted:
                    return em.HasComponent<ResourceDepleted>(entity) &&
                           em.IsComponentEnabled<ResourceDepleted>(entity);

                case AgentEventType.ResourceChanged:
                    return em.HasComponent<ResourceChanged>(entity) &&
                           em.IsComponentEnabled<ResourceChanged>(entity);

                case AgentEventType.NeedBecameCritical:
                    if (!em.HasComponent<NeedUrgencyChanged>(entity) ||
                        !em.IsComponentEnabled<NeedUrgencyChanged>(entity))
                        return false;
                    if (em.HasBuffer<NeedUrgencyChangeRecord>(entity))
                    {
                        var changes = em.GetBuffer<NeedUrgencyChangeRecord>(entity);
                        for (int c = 0; c < changes.Length; c++)
                        {
                            if (changes[c].NewUrgency == NeedUrgency.Critical)
                                return true;
                        }
                    }
                    return false;

                case AgentEventType.NavPathInvalidated:
                    return em.HasComponent<PathInvalidated>(entity) &&
                           em.IsComponentEnabled<PathInvalidated>(entity);

                case AgentEventType.ActionCompleted:
                    return em.HasComponent<ActionCompleted>(entity) &&
                           em.IsComponentEnabled<ActionCompleted>(entity);

                case AgentEventType.ActionFailed:
                    return em.HasComponent<ActionInterrupted>(entity) &&
                           em.IsComponentEnabled<ActionInterrupted>(entity);

                default:
                    return false;
            }
        }

        void ApplyReaction(EntityManager em, Entity entity, AgentReaction reaction, int param)
        {
            switch (reaction)
            {
                case AgentReaction.RefreshContext:
                    if (em.HasComponent<ContextRefreshRequest>(entity))
                        em.SetComponentEnabled<ContextRefreshRequest>(entity, true);
                    break;

                case AgentReaction.ForceRescore:
                    if (em.HasComponent<ForceRescoreTag>(entity))
                        em.SetComponentEnabled<ForceRescoreTag>(entity, true);
                    break;

                case AgentReaction.InterruptAction:
                case AgentReaction.ForceInterrupt:
                    if (em.HasComponent<AgentActionState>(entity))
                    {
                        var actionState = em.GetComponentData<AgentActionState>(entity);
                        if (actionState.Phase != AgentActionPhase.None)
                        {
                            actionState.Phase = AgentActionPhase.Interrupted;
                            actionState.CurrentActionId = -1;
                            em.SetComponentData(entity, actionState);
                            if (em.HasComponent<ActionInterrupted>(entity))
                                em.SetComponentEnabled<ActionInterrupted>(entity, true);
                        }
                    }
                    break;

                case AgentReaction.ClearQueue:
                    if (em.HasBuffer<ActionQueueEntry>(entity))
                        em.GetBuffer<ActionQueueEntry>(entity).Clear();
                    if (em.HasComponent<ActionQueueProgress>(entity))
                        em.SetComponentData(entity, new ActionQueueProgress());
                    break;

                case AgentReaction.PauseQueue:
                    if (em.HasComponent<ActionQueueProgress>(entity))
                    {
                        var progress = em.GetComponentData<ActionQueueProgress>(entity);
                        progress.QueuePaused = true;
                        em.SetComponentData(entity, progress);
                    }
                    break;

                case AgentReaction.ResumeQueue:
                    if (em.HasComponent<ActionQueueProgress>(entity))
                    {
                        var progress = em.GetComponentData<ActionQueueProgress>(entity);
                        progress.QueuePaused = false;
                        em.SetComponentData(entity, progress);
                    }
                    break;

                case AgentReaction.TransitionState:
                    if (em.HasComponent<StateMachineData>(entity))
                    {
                        var sm = em.GetComponentData<StateMachineData>(entity);
                        sm.TransitionRequested = true;
                        sm.RequestedState = param;
                        em.SetComponentData(entity, sm);
                    }
                    break;

                case AgentReaction.RequestPath:
                    if (em.HasComponent<PathRequest>(entity))
                        em.SetComponentEnabled<PathRequest>(entity, true);
                    break;
            }
        }
    }
}
