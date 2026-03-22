using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Builds spatial hash grids for POIs and agents, then refreshes each agent's
    /// KnownPOIElement and KnownAgentElement buffers.
    /// Runs early in simulation so scoring systems have current world knowledge.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WorldKnowledgeSystem : ISystem
    {
        EntityQuery _poiQuery;
        EntityQuery _agentQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _poiQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PointOfInterest, LocalTransform>()
                .Build(ref state);

            _agentQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<AgentData, LocalTransform>()
                .Build(ref state);

            state.RequireForUpdate<AwarenessData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var poiEntities = _poiQuery.ToEntityArray(Allocator.Temp);
            var poiTransforms = _poiQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var poiData = _poiQuery.ToComponentDataArray<PointOfInterest>(Allocator.Temp);

            var agentEntities = _agentQuery.ToEntityArray(Allocator.Temp);
            var agentTransforms = _agentQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var agentData = _agentQuery.ToComponentDataArray<AgentData>(Allocator.Temp);

            float defaultCellSize = 10f;

            // Build spatial hash for POIs
            var poiHash = new NativeParallelMultiHashMap<int, int>(
                math.max(poiEntities.Length * 2, 64), Allocator.Temp);
            for (int i = 0; i < poiEntities.Length; i++)
            {
                if (poiData[i].IsActive)
                    poiHash.Add(SpatialHashGrid.HashPosition(poiTransforms[i].Position, defaultCellSize), i);
            }

            // Build spatial hash for agents
            var agentHash = new NativeParallelMultiHashMap<int, int>(
                math.max(agentEntities.Length * 2, 64), Allocator.Temp);
            for (int i = 0; i < agentEntities.Length; i++)
                agentHash.Add(SpatialHashGrid.HashPosition(agentTransforms[i].Position, defaultCellSize), i);

            // Refresh each agent's knowledge
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (awareness, transform, knownPOIs, knownAgents, entity) in
                SystemAPI.Query<
                    RefRO<AwarenessData>,
                    RefRO<LocalTransform>,
                    DynamicBuffer<KnownPOIElement>,
                    DynamicBuffer<KnownAgentElement>>()
                    .WithEntityAccess())
            {
                float3 agentPos = transform.ValueRO.Position;
                float range = awareness.ValueRO.AwarenessRange;
                float rangeSq = range * range;

                // Refresh known POIs
                knownPOIs.Clear();
                int3 minCell = SpatialHashGrid.CellCoord(agentPos - range, defaultCellSize);
                int3 maxCell = SpatialHashGrid.CellCoord(agentPos + range, defaultCellSize);

                for (int cz = minCell.z; cz <= maxCell.z; cz++)
                for (int cy = minCell.y; cy <= maxCell.y; cy++)
                for (int cx = minCell.x; cx <= maxCell.x; cx++)
                {
                    int hash = SpatialHashGrid.HashCell(new int3(cx, cy, cz));
                    if (poiHash.TryGetFirstValue(hash, out int poiIdx, out var it))
                    {
                        do
                        {
                            if (poiIdx >= poiEntities.Length) continue;

                            float distSq = math.distancesq(agentPos, poiTransforms[poiIdx].Position);
                            if (distSq <= rangeSq && knownPOIs.Length < awareness.ValueRO.MaxKnownPOIs)
                            {
                                knownPOIs.Add(new KnownPOIElement
                                {
                                    POIEntity = poiEntities[poiIdx],
                                    POIType = poiData[poiIdx].POIType,
                                    Position = poiTransforms[poiIdx].Position,
                                    Distance = math.sqrt(distSq),
                                    CurrentUsers = poiData[poiIdx].CurrentUsers,
                                    Capacity = poiData[poiIdx].Capacity
                                });
                            }
                        } while (poiHash.TryGetNextValue(out poiIdx, ref it));
                    }
                }

                // Refresh known agents
                knownAgents.Clear();
                for (int cz = minCell.z; cz <= maxCell.z; cz++)
                for (int cy = minCell.y; cy <= maxCell.y; cy++)
                for (int cx = minCell.x; cx <= maxCell.x; cx++)
                {
                    int hash = SpatialHashGrid.HashCell(new int3(cx, cy, cz));
                    if (agentHash.TryGetFirstValue(hash, out int aIdx, out var it))
                    {
                        do
                        {
                            if (aIdx >= agentEntities.Length) continue;
                            if (agentEntities[aIdx] == entity) continue;

                            float distSq = math.distancesq(agentPos, agentTransforms[aIdx].Position);
                            if (distSq <= rangeSq && knownAgents.Length < awareness.ValueRO.MaxKnownAgents)
                            {
                                knownAgents.Add(new KnownAgentElement
                                {
                                    AgentEntity = agentEntities[aIdx],
                                    ArchetypeId = agentData[aIdx].ArchetypeId,
                                    Position = agentTransforms[aIdx].Position,
                                    Distance = math.sqrt(distSq)
                                });
                            }
                        } while (agentHash.TryGetNextValue(out aIdx, ref it));
                    }
                }

                ecb.SetComponentEnabled<KnowledgeRefreshed>(entity, true);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            poiEntities.Dispose();
            poiTransforms.Dispose();
            poiData.Dispose();
            agentEntities.Dispose();
            agentTransforms.Dispose();
            agentData.Dispose();
            poiHash.Dispose();
            agentHash.Dispose();
        }
    }
}
