using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct EntityPoolSystem : ISystem
    {
        EntityQuery _prototypeQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Build a query for prototypes (used to look up prototype by PoolId)
            _prototypeQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PoolPrototype>()
                .Build(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // === Phase 1: Clear previous frame's 1-frame events ===
            new ClearEventsJob().ScheduleParallel();
            state.Dependency.Complete();

            // === Phase 2: Process returns ===
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (pooled, entity) in
                SystemAPI.Query<RefRO<Pooled>>()
                    .WithAll<ReturnRequest>()
                    .WithEntityAccess())
            {
                ecb.SetComponentEnabled<PoolActive>(entity, false);
                ecb.AddComponent<Disabled>(entity);
                ecb.SetComponentEnabled<PoolReturned>(entity, true);
                ecb.SetComponentEnabled<ReturnRequest>(entity, false);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // === Phase 3: Process acquisitions ===
            // Gather available (disabled) pooled entities
            var availableQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Pooled, Disabled>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Build(ref state);

            var availableEntities = availableQuery.ToEntityArray(Allocator.Temp);
            var availablePooled = availableQuery.ToComponentDataArray<Pooled>(Allocator.Temp);

            // Gather prototypes for fallback instantiation
            var prototypeEntities = _prototypeQuery.ToEntityArray(Allocator.Temp);
            var prototypeData = _prototypeQuery.ToComponentDataArray<PoolPrototype>(Allocator.Temp);

            // Track which available entities have been claimed this frame
            var claimed = new NativeArray<bool>(availableEntities.Length, Allocator.Temp);

            var acquireEcb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (request, requestEntity) in
                SystemAPI.Query<RefRO<AcquireRequest>>()
                    .WithEntityAccess())
            {
                int poolId = request.ValueRO.PoolId;
                Entity acquired = Entity.Null;

                // Find an available entity from the pool
                for (int i = 0; i < availableEntities.Length; i++)
                {
                    if (!claimed[i] && availablePooled[i].PoolId == poolId)
                    {
                        acquired = availableEntities[i];
                        claimed[i] = true;
                        break;
                    }
                }

                if (acquired != Entity.Null)
                {
                    // Re-activate existing pooled entity
                    acquireEcb.RemoveComponent<Disabled>(acquired);
                    acquireEcb.SetComponentEnabled<PoolActive>(acquired, true);
                    acquireEcb.SetComponentEnabled<PoolAcquired>(acquired, true);
                    // Clear stale PoolReturned (ClearEventsJob skips Disabled entities)
                    acquireEcb.SetComponentEnabled<PoolReturned>(acquired, false);
                }
                else
                {
                    // No available entity — instantiate from prototype
                    Entity prototype = FindPrototype(prototypeEntities, prototypeData, poolId);
                    if (prototype != Entity.Null)
                    {
                        Entity spawned = acquireEcb.Instantiate(prototype);
                        acquireEcb.AddComponent(spawned, new Pooled { PoolId = poolId });
                        acquireEcb.AddComponent<PoolActive>(spawned);
                        acquireEcb.SetComponentEnabled<PoolActive>(spawned, true);
                        acquireEcb.AddComponent<PoolAcquired>(spawned);
                        acquireEcb.SetComponentEnabled<PoolAcquired>(spawned, true);
                        acquireEcb.AddComponent<PoolReturned>(spawned);
                        acquireEcb.SetComponentEnabled<PoolReturned>(spawned, false);
                        acquireEcb.AddComponent<ReturnRequest>(spawned);
                        acquireEcb.SetComponentEnabled<ReturnRequest>(spawned, false);
                    }
                }

                // Destroy the request entity
                acquireEcb.DestroyEntity(requestEntity);
            }

            acquireEcb.Playback(state.EntityManager);
            acquireEcb.Dispose();

            claimed.Dispose();
            availableEntities.Dispose();
            availablePooled.Dispose();
            prototypeEntities.Dispose();
            prototypeData.Dispose();
        }

        static Entity FindPrototype(
            NativeArray<Entity> prototypeEntities,
            NativeArray<PoolPrototype> prototypeData,
            int poolId)
        {
            for (int i = 0; i < prototypeData.Length; i++)
            {
                if (prototypeData[i].PoolId == poolId)
                    return prototypeEntities[i];
            }
            return Entity.Null;
        }

        [BurstCompile]
        partial struct ClearEventsJob : IJobEntity
        {
            void Execute(
                EnabledRefRW<PoolAcquired> acquired,
                EnabledRefRW<PoolReturned> returned)
            {
                acquired.ValueRW = false;
                returned.ValueRW = false;
            }
        }
    }
}
