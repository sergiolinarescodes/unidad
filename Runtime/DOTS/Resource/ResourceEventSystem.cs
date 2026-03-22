using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct ResourceEventClearSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ResourceElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in
                SystemAPI.Query<DynamicBuffer<ResourceElement>>()
                    .WithEntityAccess())
            {
                var changes = em.GetBuffer<ResourceChangeRecord>(entity);
                changes.Clear();
                ecb.SetComponentEnabled<ResourceChanged>(entity, false);
                ecb.SetComponentEnabled<ResourceDepleted>(entity, false);
                ecb.SetComponentEnabled<ResourceFilled>(entity, false);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct ResourceEventSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ResourceElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in
                SystemAPI.Query<DynamicBuffer<ResourceElement>>()
                    .WithEntityAccess())
            {
                var changes = em.GetBuffer<ResourceChangeRecord>(entity);

                if (changes.Length == 0)
                    continue;

                ecb.SetComponentEnabled<ResourceChanged>(entity, true);

                for (int i = 0; i < changes.Length; i++)
                {
                    var record = changes[i];

                    if (record.NewValue <= record.EffectiveMin && record.OldValue > record.EffectiveMin)
                        ecb.SetComponentEnabled<ResourceDepleted>(entity, true);

                    if (record.NewValue >= record.EffectiveMax && record.OldValue < record.EffectiveMax)
                        ecb.SetComponentEnabled<ResourceFilled>(entity, true);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
