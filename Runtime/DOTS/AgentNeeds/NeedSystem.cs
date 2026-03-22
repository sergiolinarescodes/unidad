using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Clears need urgency events from the previous frame.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct NeedEventClearSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NeedElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in
                SystemAPI.Query<DynamicBuffer<NeedElement>>()
                    .WithEntityAccess())
            {
                var changes = em.GetBuffer<NeedUrgencyChangeRecord>(entity);
                changes.Clear();
                ecb.SetComponentEnabled<NeedUrgencyChanged>(entity, false);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Decays needs over time using ResourceUtility.Add().
    /// Uses main-thread foreach to avoid job dependency issues.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct NeedDecaySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NeedElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0f) return;

            state.EntityManager.CompleteAllTrackedJobs();

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (decayMods, entity) in
                SystemAPI.Query<DynamicBuffer<NeedDecayModifier>>()
                    .WithAll<NeedElement, ResourceElement>()
                    .WithNone<AgentIsSuspended>()
                    .WithEntityAccess())
            {
                var needs = em.GetBuffer<NeedElement>(entity);
                var resources = em.GetBuffer<ResourceElement>(entity);
                var resourceChanges = em.GetBuffer<ResourceChangeRecord>(entity);
                var maxMods = em.GetBuffer<ResourceMaxModifier>(entity);
                var minMods = em.GetBuffer<ResourceMinModifier>(entity);
                var urgencyChanges = em.GetBuffer<NeedUrgencyChangeRecord>(entity);

                for (int i = 0; i < needs.Length; i++)
                {
                    var need = needs[i];

                    float effectiveDecay = NeedUtility.GetEffectiveDecayRate(
                        need.ResourceId, need.DecayRate, in decayMods);

                    ResourceUtility.Add(ref resources, ref resourceChanges,
                        in maxMods, in minMods,
                        need.ResourceId, -effectiveDecay * dt);

                    float currentValue = ResourceUtility.Get(in resources, need.ResourceId);
                    NeedUrgency newUrgency = NeedUtility.EvaluateUrgency(
                        currentValue, need.CriticalThreshold, need.LowThreshold, need.HighThreshold);

                    if (newUrgency != need.CurrentUrgency)
                    {
                        urgencyChanges.Add(new NeedUrgencyChangeRecord
                        {
                            ResourceId = need.ResourceId,
                            OldUrgency = need.CurrentUrgency,
                            NewUrgency = newUrgency
                        });

                        ecb.SetComponentEnabled<NeedUrgencyChanged>(entity, true);

                        need.CurrentUrgency = newUrgency;
                        needs[i] = need;
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
