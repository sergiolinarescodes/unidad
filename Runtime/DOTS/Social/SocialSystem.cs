using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>Clears interaction events from previous frame.</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct InteractionEventClearSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<InteractionState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<InteractionState>>()
                    .WithEntityAccess())
            {
                ecb.SetComponentEnabled<InteractionStarted>(entity, false);
                ecb.SetComponentEnabled<InteractionCompleted>(entity, false);
                ecb.SetComponentEnabled<InteractionRejected>(entity, false);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Matches interaction requests with target agents.
    /// Checks that target is not already interacting, sets both agents to Active phase.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct InteractionRequestSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<InteractionState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (request, iState, agentData, entity) in
                SystemAPI.Query<
                    RefRO<InteractionRequest>,
                    RefRW<InteractionState>,
                    RefRO<AgentData>>()
                    .WithAll<InteractionRequest>()
                    .WithEntityAccess())
            {
                Entity target = request.ValueRO.TargetAgent;

                // Validate target exists and has InteractionState
                if (target == Entity.Null || !em.HasComponent<InteractionState>(target))
                {
                    ecb.SetComponentEnabled<InteractionRequest>(entity, false);
                    ecb.SetComponentEnabled<InteractionRejected>(entity, true);
                    continue;
                }

                // Check target is not already interacting
                var targetState = em.GetComponentData<InteractionState>(target);
                if (targetState.Phase != InteractionPhase.None)
                {
                    ecb.SetComponentEnabled<InteractionRequest>(entity, false);
                    ecb.SetComponentEnabled<InteractionRejected>(entity, true);
                    continue;
                }

                // Match! Set both agents to Active
                iState.ValueRW.PartnerEntity = target;
                iState.ValueRW.InteractionType = request.ValueRO.InteractionType;
                iState.ValueRW.Phase = InteractionPhase.Active;

                targetState.PartnerEntity = entity;
                targetState.InteractionType = request.ValueRO.InteractionType;
                targetState.Phase = InteractionPhase.Active;
                em.SetComponentData(target, targetState);

                ecb.SetComponentEnabled<InteractionRequest>(entity, false);
                ecb.SetComponentEnabled<InteractionStarted>(entity, true);
                ecb.SetComponentEnabled<InteractionStarted>(target, true);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Monitors active interactions. When an agent's action completes while interacting,
    /// finalizes the interaction: updates relationships, fires InteractionCompleted.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct InteractionExecutionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<InteractionState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            double elapsedTime = SystemAPI.Time.ElapsedTime;

            foreach (var (iState, agentData, entity) in
                SystemAPI.Query<
                    RefRW<InteractionState>,
                    RefRO<AgentData>>()
                    .WithEntityAccess())
            {
                if (iState.ValueRO.Phase != InteractionPhase.Active)
                    continue;

                // Check if action completed
                bool actionDone = em.HasComponent<ActionCompleted>(entity) &&
                                  em.IsComponentEnabled<ActionCompleted>(entity);

                if (!actionDone)
                    continue;

                Entity partner = iState.ValueRO.PartnerEntity;

                // Update relationships
                if (em.HasBuffer<RelationshipElement>(entity))
                {
                    var relationships = em.GetBuffer<RelationshipElement>(entity);
                    int partnerId = em.HasComponent<AgentData>(partner)
                        ? em.GetComponentData<AgentData>(partner).AgentId
                        : 0;
                    SocialUtility.ModifyTrust(ref relationships, partnerId, 0.1f, elapsedTime);
                }

                // Complete this agent's interaction
                iState.ValueRW.Phase = InteractionPhase.None;
                iState.ValueRW.PartnerEntity = Entity.Null;
                ecb.SetComponentEnabled<InteractionCompleted>(entity, true);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
