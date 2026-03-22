using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Market entity — tracks supply/demand and prices for items.
    /// </summary>
    public struct MarketData : IComponentData
    {
        public int MarketId;
        public float PriceUpdateInterval;
        public double LastUpdateTime;
    }

    /// <summary>
    /// One item tracked by a market. Stored as buffer on market entity.
    /// </summary>
    public struct MarketItemElement : IBufferElementData
    {
        public int ItemId;
        public float BasePrice;
        public float CurrentPrice;
        public float Supply;
        public float Demand;
        public float PriceElasticity;
    }

    /// <summary>
    /// Entity that produces an item. Aggregated by EconomySystem into market Supply.
    /// </summary>
    public struct ProducerData : IComponentData
    {
        public int MarketId;
        public int ProducedItemId;
        public float ProductionRate;
        public float ProductionCost;
    }

    /// <summary>
    /// Entity that consumes an item. Aggregated by EconomySystem into market Demand.
    /// </summary>
    public struct ConsumerData : IComponentData
    {
        public int MarketId;
        public int ConsumedItemId;
        public float ConsumptionRate;
        public float WillingnessToPay;
    }

    /// <summary>
    /// Transaction history on a market entity.
    /// </summary>
    public struct TradeRecord : IBufferElementData
    {
        public int ItemId;
        public float Quantity;
        public float PricePerUnit;
        public double Time;
    }

    /// <summary>1-frame: market prices changed this frame.</summary>
    public struct MarketPriceChanged : IComponentData, IEnableableComponent { }
}
