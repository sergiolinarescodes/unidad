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
    ///
    /// Configurable via WorldKnowledgeConfig singleton:
    ///   CellSize — spatial hash cell size (default 10)
    ///   Is2D     — collapse Y dimension for ground-plane games (~11x fewer cell lookups)
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SharedContextRefreshSystem))]
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
            float cellSize = 10f;
            bool is2D = false;
            if (SystemAPI.HasSingleton<WorldKnowledgeConfig>())
            {
                var config = SystemAPI.GetSingleton<WorldKnowledgeConfig>();
                cellSize = config.CellSize > 0f ? config.CellSize : 10f;
                is2D = config.Is2D;
            }

            var poiEntities = _poiQuery.ToEntityArray(Allocator.Temp);
            var poiTransforms = _poiQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var poiData = _poiQuery.ToComponentDataArray<PointOfInterest>(Allocator.Temp);

            var agentEntities = _agentQuery.ToEntityArray(Allocator.Temp);
            var agentTransforms = _agentQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var agentData = _agentQuery.ToComponentDataArray<AgentData>(Allocator.Temp);

            var poiHash = new NativeParallelMultiHashMap<int, int>(
                math.max(poiEntities.Length * 2, 64), Allocator.Temp);
            for (int i = 0; i < poiEntities.Length; i++)
            {
                if (poiData[i].IsActive)
                {
                    var pos = is2D ? FlattenY(poiTransforms[i].Position) : poiTransforms[i].Position;
                    poiHash.Add(SpatialHashGrid.HashPosition(pos, cellSize), i);
                }
            }

            var agentHash = new NativeParallelMultiHashMap<int, int>(
                math.max(agentEntities.Length * 2, 64), Allocator.Temp);
            for (int i = 0; i < agentEntities.Length; i++)
            {
                var pos = is2D ? FlattenY(agentTransforms[i].Position) : agentTransforms[i].Position;
                agentHash.Add(SpatialHashGrid.HashPosition(pos, cellSize), i);
            }

            foreach (var (awareness, transform, knownPOIs, knownAgents, entity) in
                SystemAPI.Query<
                    RefRO<AwarenessData>,
                    RefRO<LocalTransform>,
                    DynamicBuffer<KnownPOIElement>,
                    DynamicBuffer<KnownAgentElement>>()
                    .WithNone<AgentIsSuspended>()
                    .WithEntityAccess())
            {
                float3 agentPos = transform.ValueRO.Position;
                float range = awareness.ValueRO.AwarenessRange;
                float rangeSq = range * range;
                int maxPOIs = awareness.ValueRO.MaxKnownPOIs;
                int maxAgents = awareness.ValueRO.MaxKnownAgents;

                float3 queryPos = is2D ? FlattenY(agentPos) : agentPos;
                int3 minCell = SpatialHashGrid.CellCoord(queryPos - range, cellSize);
                int3 maxCell = SpatialHashGrid.CellCoord(queryPos + range, cellSize);

                if (is2D)
                {
                    minCell.y = 0;
                    maxCell.y = 0;
                }

                knownPOIs.Clear();
                bool poiFull = false;
                for (int cz = minCell.z; cz <= maxCell.z && !poiFull; cz++)
                for (int cy = minCell.y; cy <= maxCell.y && !poiFull; cy++)
                for (int cx = minCell.x; cx <= maxCell.x && !poiFull; cx++)
                {
                    int hash = SpatialHashGrid.HashCell(new int3(cx, cy, cz));
                    if (poiHash.TryGetFirstValue(hash, out int poiIdx, out var it))
                    {
                        do
                        {
                            if (poiIdx >= poiEntities.Length) continue;

                            float distSq = math.distancesq(agentPos, poiTransforms[poiIdx].Position);
                            if (distSq <= rangeSq)
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
                                if (knownPOIs.Length >= maxPOIs)
                                {
                                    poiFull = true;
                                    break;
                                }
                            }
                        } while (poiHash.TryGetNextValue(out poiIdx, ref it));
                    }
                }

                knownAgents.Clear();
                bool agentFull = false;
                for (int cz = minCell.z; cz <= maxCell.z && !agentFull; cz++)
                for (int cy = minCell.y; cy <= maxCell.y && !agentFull; cy++)
                for (int cx = minCell.x; cx <= maxCell.x && !agentFull; cx++)
                {
                    int hash = SpatialHashGrid.HashCell(new int3(cx, cy, cz));
                    if (agentHash.TryGetFirstValue(hash, out int aIdx, out var it))
                    {
                        do
                        {
                            if (aIdx >= agentEntities.Length) continue;
                            if (agentEntities[aIdx] == entity) continue;

                            float distSq = math.distancesq(agentPos, agentTransforms[aIdx].Position);
                            if (distSq <= rangeSq)
                            {
                                knownAgents.Add(new KnownAgentElement
                                {
                                    AgentEntity = agentEntities[aIdx],
                                    ArchetypeId = agentData[aIdx].ArchetypeId,
                                    Position = agentTransforms[aIdx].Position,
                                    Distance = math.sqrt(distSq)
                                });
                                if (knownAgents.Length >= maxAgents)
                                {
                                    agentFull = true;
                                    break;
                                }
                            }
                        } while (agentHash.TryGetNextValue(out aIdx, ref it));
                    }
                }

                SystemAPI.SetComponentEnabled<KnowledgeRefreshed>(entity, true);
            }

            poiEntities.Dispose();
            poiTransforms.Dispose();
            poiData.Dispose();
            agentEntities.Dispose();
            agentTransforms.Dispose();
            agentData.Dispose();
            poiHash.Dispose();
            agentHash.Dispose();
        }

        static float3 FlattenY(float3 pos) => new float3(pos.x, 0f, pos.z);
    }
}
