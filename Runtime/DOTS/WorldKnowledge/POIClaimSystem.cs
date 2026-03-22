using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Sequential POI claiming system. Processes claims after scoring/action selection.
    /// First-come-first-served by entity index. Rejects claims when POI is at capacity.
    /// Also handles claim release on ActionCompleted or AgentDespawning.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct POIClaimSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PointOfInterest>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Release claims from agents that completed or are despawning
            foreach (var (claim, actionState, agent, entity) in
                SystemAPI.Query<
                    RefRW<POIClaim>,
                    RefRO<AgentActionState>,
                    RefRO<AgentData>>()
                    .WithEntityAccess())
            {
                if (claim.ValueRO.POIEntity == Entity.Null)
                    continue;

                bool shouldRelease = false;

                // Release on action completion or interruption
                if (SystemAPI.IsComponentEnabled<ActionCompleted>(entity) ||
                    SystemAPI.IsComponentEnabled<ActionInterrupted>(entity))
                    shouldRelease = true;

                // Release on despawn
                if (agent.ValueRO.LifecycleState == AgentLifecycleState.Despawning)
                    shouldRelease = true;

                if (shouldRelease && SystemAPI.HasComponent<PointOfInterest>(claim.ValueRO.POIEntity))
                {
                    var poi = SystemAPI.GetComponent<PointOfInterest>(claim.ValueRO.POIEntity);
                    poi.CurrentUsers = math.max(0, poi.CurrentUsers - 1);
                    SystemAPI.SetComponent(claim.ValueRO.POIEntity, poi);

                    claim.ValueRW.POIEntity = Entity.Null;
                    claim.ValueRW.POIType = 0;
                }
            }

            // Process new claims from agents starting actions with a target POI
            foreach (var (claim, actionState, agentTarget, entity) in
                SystemAPI.Query<
                    RefRW<POIClaim>,
                    RefRO<AgentActionState>,
                    RefRO<AgentTarget>>()
                    .WithAll<ActionStarted>()
                    .WithEntityAccess())
            {
                // Only claim if action is starting and target is a valid entity
                if (actionState.ValueRO.Phase != AgentActionPhase.Starting)
                    continue;

                Entity targetEntity = agentTarget.ValueRO.TargetEntity;
                if (targetEntity == Entity.Null)
                    continue;

                if (!SystemAPI.HasComponent<PointOfInterest>(targetEntity))
                    continue;

                var poi = SystemAPI.GetComponent<PointOfInterest>(targetEntity);

                if (poi.CurrentUsers < poi.Capacity && poi.IsActive)
                {
                    // Claim accepted
                    poi.CurrentUsers++;
                    SystemAPI.SetComponent(targetEntity, poi);

                    claim.ValueRW.POIEntity = targetEntity;
                    claim.ValueRW.POIType = poi.POIType;
                }
                else
                {
                    // Claim rejected — POI at capacity
                    SystemAPI.SetComponentEnabled<POIClaimRejected>(entity, true);
                }
            }
        }
    }
}
