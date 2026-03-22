using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Cleanup pipeline for despawning agents. Runs OrderLast in SimulationSystemGroup.
    /// When AgentData.LifecycleState == Despawning:
    /// 1. Release POI claims
    /// 2. Clear nav path
    /// 3. Clear action queue
    /// 4. Fire AgentDespawning event
    /// 5. Destroy entity
    ///
    /// Game code sets LifecycleState = Despawning to trigger this pipeline.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct AgentDespawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AgentData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (agent, entity) in
                SystemAPI.Query<RefRO<AgentData>>()
                    .WithEntityAccess())
            {
                if (agent.ValueRO.LifecycleState != AgentLifecycleState.Despawning)
                    continue;

                // 1. Release POI claim if active
                if (SystemAPI.HasComponent<POIClaim>(entity))
                {
                    var claim = SystemAPI.GetComponent<POIClaim>(entity);
                    if (claim.POIEntity != Entity.Null &&
                        SystemAPI.HasComponent<PointOfInterest>(claim.POIEntity))
                    {
                        var poi = SystemAPI.GetComponent<PointOfInterest>(claim.POIEntity);
                        poi.CurrentUsers = math.max(0, poi.CurrentUsers - 1);
                        SystemAPI.SetComponent(claim.POIEntity, poi);
                    }
                }

                // 2. Clear nav path
                if (SystemAPI.HasComponent<NavAgent>(entity))
                {
                    SystemAPI.SetComponent(entity, new NavAgent
                    {
                        GraphId = SystemAPI.GetComponent<NavAgent>(entity).GraphId,
                        CurrentNodeId = -1,
                        Status = NavAgentStatus.Idle
                    });
                }
                if (SystemAPI.HasBuffer<PathNodeElement>(entity))
                    SystemAPI.GetBuffer<PathNodeElement>(entity).Clear();

                // 3. Clear action queue
                if (SystemAPI.HasBuffer<ActionQueueEntry>(entity))
                    SystemAPI.GetBuffer<ActionQueueEntry>(entity).Clear();

                // 4. Fire despawning event
                SystemAPI.SetComponentEnabled<AgentDespawning>(entity, true);

                // 5. Schedule entity destruction
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
