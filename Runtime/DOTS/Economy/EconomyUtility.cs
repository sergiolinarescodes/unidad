using Unity.Burst;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    public static class EconomyUtility
    {
        public static float GetPrice(in DynamicBuffer<MarketItemElement> items, int itemId)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].ItemId == itemId)
                    return items[i].CurrentPrice;
            }
            return 0f;
        }

        public static int FindItem(in DynamicBuffer<MarketItemElement> items, int itemId)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].ItemId == itemId)
                    return i;
            }
            return -1;
        }

        public static void RecordTrade(ref DynamicBuffer<TradeRecord> records,
            int itemId, float quantity, float pricePerUnit, double time)
        {
            records.Add(new TradeRecord
            {
                ItemId = itemId,
                Quantity = quantity,
                PricePerUnit = pricePerUnit,
                Time = time
            });
        }
    }
}
