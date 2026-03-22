using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Aggregates supply/demand from producers/consumers, updates market prices at intervals.
    /// Broadcasts current prices to SharedContext for agent scoring.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct EconomySystem : ISystem
    {
        EntityQuery _producerQuery;
        EntityQuery _consumerQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MarketData>();

            _producerQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ProducerData>()
                .Build(ref state);

            _consumerQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ConsumerData>()
                .Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            double elapsedTime = SystemAPI.Time.ElapsedTime;
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Gather all producers and consumers
            var producers = _producerQuery.ToComponentDataArray<ProducerData>(Allocator.Temp);
            var consumers = _consumerQuery.ToComponentDataArray<ConsumerData>(Allocator.Temp);

            foreach (var (market, entity) in
                SystemAPI.Query<RefRW<MarketData>>()
                    .WithEntityAccess())
            {
                // Check if update interval has passed
                if (elapsedTime - market.ValueRO.LastUpdateTime < market.ValueRO.PriceUpdateInterval)
                    continue;

                market.ValueRW.LastUpdateTime = elapsedTime;
                int marketId = market.ValueRO.MarketId;

                var items = em.GetBuffer<MarketItemElement>(entity);

                // Aggregate supply and demand per item
                for (int i = 0; i < items.Length; i++)
                {
                    var item = items[i];
                    float supply = 0f;
                    float demand = 0f;

                    for (int p = 0; p < producers.Length; p++)
                    {
                        if (producers[p].MarketId == marketId &&
                            producers[p].ProducedItemId == item.ItemId)
                            supply += producers[p].ProductionRate;
                    }

                    for (int c = 0; c < consumers.Length; c++)
                    {
                        if (consumers[c].MarketId == marketId &&
                            consumers[c].ConsumedItemId == item.ItemId)
                            demand += consumers[c].ConsumptionRate;
                    }

                    item.Supply = supply;
                    item.Demand = demand;

                    // Price adjustment: price moves toward BasePrice * (Demand / Supply)
                    // Elasticity controls how fast it moves
                    float safeSupply = math.max(supply, 0.01f);
                    float targetPrice = item.BasePrice * (demand / safeSupply);
                    float elasticity = math.clamp(item.PriceElasticity, 0.01f, 1f);
                    item.CurrentPrice = math.lerp(item.CurrentPrice, targetPrice, elasticity);
                    item.CurrentPrice = math.max(item.CurrentPrice, 0.01f); // Never free

                    items[i] = item;
                }

                ecb.SetComponentEnabled<MarketPriceChanged>(entity, true);

                // Broadcast prices to SharedContext if available
                if (SystemAPI.HasSingleton<SharedContextBroadcastConfig>())
                {
                    foreach (var (ctxData, ctxEntity) in
                        SystemAPI.Query<RefRO<SharedContextData>>()
                            .WithEntityAccess())
                    {
                        if (ctxData.ValueRO.ArchetypeId != -1)
                            continue;

                        var ctxEntries = em.GetBuffer<SharedContextEntry>(ctxEntity);
                        for (int i = 0; i < items.Length; i++)
                        {
                            SharedContextUtility.Set(ref ctxEntries,
                                1000 + items[i].ItemId, items[i].CurrentPrice, elapsedTime);
                        }
                        break;
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            producers.Dispose();
            consumers.Dispose();
        }
    }

    /// <summary>Clears MarketPriceChanged events.</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct EconomyEventClearSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MarketData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<MarketData>>()
                    .WithEntityAccess())
            {
                ecb.SetComponentEnabled<MarketPriceChanged>(entity, false);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
