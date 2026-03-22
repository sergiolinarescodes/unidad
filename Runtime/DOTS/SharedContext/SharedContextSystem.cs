using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Writes all global SharedContextEntry values into a flat NativeArray indexed by key.
    /// This array is passed to scoring jobs as [ReadOnly] — zero per-entity memory cost.
    /// Runs early in SimulationSystemGroup after event clears.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SharedContextBroadcastSystem : ISystem
    {
        NativeArray<float> _broadcastArray;
        EntityQuery _globalContextQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SharedContextBroadcastConfig>();

            _globalContextQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SharedContextData>()
                .Build(ref state);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_broadcastArray.IsCreated)
                _broadcastArray.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SharedContextBroadcastConfig>();

            // Ensure broadcast array is correctly sized
            if (!_broadcastArray.IsCreated || _broadcastArray.Length != config.MaxKeys)
            {
                if (_broadcastArray.IsCreated)
                    _broadcastArray.Dispose();
                _broadcastArray = new NativeArray<float>(config.MaxKeys, Allocator.Persistent);
            }

            // Clear
            for (int i = 0; i < _broadcastArray.Length; i++)
                _broadcastArray[i] = 0f;

            // Find the global context entity (ArchetypeId == -1) and copy entries
            foreach (var (contextData, entries) in
                SystemAPI.Query<RefRO<SharedContextData>, DynamicBuffer<SharedContextEntry>>())
            {
                if (contextData.ValueRO.ArchetypeId != -1)
                    continue;

                for (int i = 0; i < entries.Length; i++)
                {
                    int key = entries[i].Key;
                    if (key >= 0 && key < _broadcastArray.Length)
                        _broadcastArray[key] = entries[i].Value;
                }
            }
        }

        /// <summary>
        /// Returns the current broadcast array for read-only access by other systems.
        /// Only valid during the frame it was written.
        /// </summary>
        public NativeArray<float> GetBroadcastArray()
        {
            return _broadcastArray;
        }
    }

    /// <summary>
    /// Refreshes per-agent AgentContextSnapshot buffers based on each agent's
    /// ContextRefreshPolicy. Handles archetype-filtered context access.
    /// Runs after broadcast, before NeedDecaySystem.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SharedContextBroadcastSystem))]
    public partial struct SharedContextRefreshSystem : ISystem
    {
        EntityQuery _contextQuery;

        public void OnCreate(ref SystemState state)
        {
            _contextQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SharedContextData>()
                .Build(ref state);

            state.RequireForUpdate<ContextRefreshPolicy>();
        }

        public void OnUpdate(ref SystemState state)
        {
            double elapsedTime = SystemAPI.Time.ElapsedTime;

            var contextEntities = _contextQuery.ToEntityArray(Allocator.Temp);
            var contextDatas = _contextQuery.ToComponentDataArray<SharedContextData>(Allocator.Temp);

            foreach (var (policy, agent, snapshot, entity) in
                SystemAPI.Query<
                    RefRW<ContextRefreshPolicy>,
                    RefRO<AgentData>,
                    DynamicBuffer<AgentContextSnapshot>>()
                    .WithEntityAccess())
            {
                // EveryFrame agents read from broadcast array directly — no snapshot needed
                if (policy.ValueRO.Mode == ContextRefreshMode.EveryFrame)
                    continue;

                // Check if refresh is due
                bool shouldRefresh = false;

                switch (policy.ValueRO.Mode)
                {
                    case ContextRefreshMode.Interval:
                        shouldRefresh = (elapsedTime - policy.ValueRO.LastRefreshTime)
                            >= policy.ValueRO.RefreshInterval;
                        break;

                    case ContextRefreshMode.OnScoring:
                        // Handled by ScoringSystem — it enables ContextRefreshRequest before scoring
                        break;

                    case ContextRefreshMode.OnMilestone:
                        // Handled by event subscription system — milestones enable ContextRefreshRequest
                        break;

                    case ContextRefreshMode.Manual:
                        // Only refreshes when ContextRefreshRequest is explicitly enabled
                        break;
                }

                // Check explicit refresh request
                if (SystemAPI.IsComponentEnabled<ContextRefreshRequest>(entity))
                {
                    shouldRefresh = true;
                    SystemAPI.SetComponentEnabled<ContextRefreshRequest>(entity, false);
                }

                if (!shouldRefresh)
                    continue;

                // Refresh snapshot: copy accessible entries from global + archetype-specific contexts
                snapshot.Clear();
                int archetypeId = agent.ValueRO.ArchetypeId;

                for (int c = 0; c < contextDatas.Length; c++)
                {
                    var cd = contextDatas[c];
                    bool isGlobal = cd.ArchetypeId == -1;
                    bool isArchetypeMatch = cd.ArchetypeId == archetypeId;

                    if (!isGlobal && !isArchetypeMatch)
                        continue;

                    Entity contextEntity = contextEntities[c];
                    var entries = SystemAPI.GetBuffer<SharedContextEntry>(contextEntity);

                    if (isGlobal)
                    {
                        // Global entries: copy all
                        for (int e = 0; e < entries.Length; e++)
                        {
                            snapshot.Add(new AgentContextSnapshot
                            {
                                Key = entries[e].Key,
                                Value = entries[e].Value
                            });
                        }
                    }
                    else
                    {
                        // Archetype-specific: check access rules
                        var rules = SystemAPI.GetBuffer<ContextAccessRule>(contextEntity);
                        for (int e = 0; e < entries.Length; e++)
                        {
                            if (SharedContextUtility.HasAccess(in rules, archetypeId, entries[e].Key))
                            {
                                snapshot.Add(new AgentContextSnapshot
                                {
                                    Key = entries[e].Key,
                                    Value = entries[e].Value
                                });
                            }
                        }
                    }
                }

                policy.ValueRW.LastRefreshTime = elapsedTime;
                SystemAPI.SetComponentEnabled<ContextRefreshed>(entity, true);
            }

            contextEntities.Dispose();
            contextDatas.Dispose();
        }
    }
}
