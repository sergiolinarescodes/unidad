using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS.Tests
{
    [TestFixture]
    public class EconomySystemTests : DOTSTestFixture
    {
        SimulationSystemGroup _group;

        public override void SetUp()
        {
            base.SetUp();
            var clearSys = GetOrCreateSystem<EconomyEventClearSystem>();
            var econSys = GetOrCreateSystem<EconomySystem>();
            _group = CreateSimGroup(clearSys, econSys);
        }

        Entity CreateMarket(int marketId, float updateInterval, params MarketItemElement[] items)
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<MarketData>(),
                ComponentType.ReadWrite<MarketPriceChanged>());

            Manager.SetComponentData(e, new MarketData
            {
                MarketId = marketId,
                PriceUpdateInterval = updateInterval,
                LastUpdateTime = 0.0
            });
            SetEnabled<MarketPriceChanged>(e, false);

            var buf = AddBuffer<MarketItemElement>(e);
            foreach (var item in items) buf.Add(item);
            AddBuffer<TradeRecord>(e);

            return e;
        }

        Entity CreateProducer(int marketId, int itemId, float rate)
        {
            var e = CreateEntity(ComponentType.ReadWrite<ProducerData>());
            Manager.SetComponentData(e, new ProducerData
            {
                MarketId = marketId,
                ProducedItemId = itemId,
                ProductionRate = rate,
                ProductionCost = 1f
            });
            return e;
        }

        Entity CreateConsumer(int marketId, int itemId, float rate)
        {
            var e = CreateEntity(ComponentType.ReadWrite<ConsumerData>());
            Manager.SetComponentData(e, new ConsumerData
            {
                MarketId = marketId,
                ConsumedItemId = itemId,
                ConsumptionRate = rate,
                WillingnessToPay = 10f
            });
            return e;
        }

        [Test]
        public void SupplyDemand_Aggregated()
        {
            var market = CreateMarket(1, 0f, new MarketItemElement
            {
                ItemId = 1, BasePrice = 10f, CurrentPrice = 10f, PriceElasticity = 1f
            });
            CreateProducer(1, 1, rate: 5f);
            CreateProducer(1, 1, rate: 3f);
            CreateConsumer(1, 1, rate: 4f);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            var items = Manager.GetBuffer<MarketItemElement>(market);
            Assert.AreEqual(8f, items[0].Supply, 0.01f, "5 + 3 = 8 supply");
            Assert.AreEqual(4f, items[0].Demand, 0.01f, "4 demand");
        }

        [Test]
        public void Price_IncreasesWhenDemandHigh()
        {
            var market = CreateMarket(1, 0f, new MarketItemElement
            {
                ItemId = 1, BasePrice = 10f, CurrentPrice = 10f, PriceElasticity = 1f
            });
            CreateProducer(1, 1, rate: 1f);
            CreateConsumer(1, 1, rate: 5f); // Demand >> Supply

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            var items = Manager.GetBuffer<MarketItemElement>(market);
            Assert.Greater(items[0].CurrentPrice, 10f,
                $"Price should increase: {items[0].CurrentPrice:F2}");
        }

        [Test]
        public void Price_DecreasesWhenSupplyHigh()
        {
            var market = CreateMarket(1, 0f, new MarketItemElement
            {
                ItemId = 1, BasePrice = 10f, CurrentPrice = 10f, PriceElasticity = 1f
            });
            CreateProducer(1, 1, rate: 10f); // Supply >> Demand
            CreateConsumer(1, 1, rate: 1f);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            var items = Manager.GetBuffer<MarketItemElement>(market);
            Assert.Less(items[0].CurrentPrice, 10f,
                $"Price should decrease: {items[0].CurrentPrice:F2}");
        }

        [Test]
        public void PriceChanged_EventFires()
        {
            var market = CreateMarket(1, 0f, new MarketItemElement
            {
                ItemId = 1, BasePrice = 10f, CurrentPrice = 10f, PriceElasticity = 1f
            });
            CreateProducer(1, 1, rate: 1f);
            CreateConsumer(1, 1, rate: 1f);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<MarketPriceChanged>(market));
        }

        [Test]
        public void NoUpdate_BeforeInterval()
        {
            var market = CreateMarket(1, 5f, new MarketItemElement
            {
                ItemId = 1, BasePrice = 10f, CurrentPrice = 10f, PriceElasticity = 1f
            });
            CreateProducer(1, 1, rate: 1f);
            CreateConsumer(1, 1, rate: 10f);

            SetWorldTime(1.0, 1.0f); // Only 1s, interval is 5s
            UpdateGroup(_group);

            Assert.IsFalse(IsEnabled<MarketPriceChanged>(market),
                "Should not update before interval");
        }

        [Test]
        public void Utility_RecordTrade()
        {
            var market = CreateMarket(1, 0f);
            var records = Manager.GetBuffer<TradeRecord>(market);

            EconomyUtility.RecordTrade(ref records, itemId: 1, quantity: 5f, pricePerUnit: 12f, time: 1.0);

            Assert.AreEqual(1, records.Length);
            Assert.AreEqual(1, records[0].ItemId);
            Assert.AreEqual(5f, records[0].Quantity, 0.01f);
            Assert.AreEqual(12f, records[0].PricePerUnit, 0.01f);
        }
    }
}
