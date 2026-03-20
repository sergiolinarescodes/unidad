using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(EntityPoolSystem))]
    public partial struct EntityPoolPrewarmSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PoolPrototype>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (prototype, entity) in
                SystemAPI.Query<RefRW<PoolPrototype>>()
                    .WithEntityAccess())
            {
                int count = prototype.ValueRO.PrewarmCount;
                if (count <= 0)
                    continue;

                int poolId = prototype.ValueRO.PoolId;

                for (int i = 0; i < count; i++)
                {
                    Entity spawned = ecb.Instantiate(entity);
                    ecb.AddComponent(spawned, new Pooled { PoolId = poolId });
                    ecb.AddComponent<PoolActive>(spawned);
                    ecb.SetComponentEnabled<PoolActive>(spawned, false);
                    ecb.AddComponent<PoolAcquired>(spawned);
                    ecb.SetComponentEnabled<PoolAcquired>(spawned, false);
                    ecb.AddComponent<PoolReturned>(spawned);
                    ecb.SetComponentEnabled<PoolReturned>(spawned, false);
                    ecb.AddComponent<ReturnRequest>(spawned);
                    ecb.SetComponentEnabled<ReturnRequest>(spawned, false);
                    ecb.AddComponent<Disabled>(spawned);
                }

                // Zero out to prevent re-prewarm
                prototype.ValueRW.PrewarmCount = 0;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
