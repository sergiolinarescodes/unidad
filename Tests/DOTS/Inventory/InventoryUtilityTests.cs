using NUnit.Framework;
using Unity.Entities;

namespace Unidad.Core.DOTS.Tests
{
    [TestFixture]
    public class InventoryUtilityTests : DOTSTestFixture
    {
        Entity _entity;
        const int MaxStack = 10;

        Entity CreateInventoryEntity(int baseSlots, int maxSlots, int slotBufferSize)
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<InventoryData>(),
                ComponentType.ReadWrite<InventorySlotElement>(),
                ComponentType.ReadWrite<InventoryCapacityModifier>());
            Manager.SetComponentData(e, new InventoryData
            {
                BaseSlotCount = baseSlots,
                MaxSlotCount = maxSlots
            });
            // Pre-populate slot buffer with empty slots
            var slots = Manager.GetBuffer<InventorySlotElement>(e);
            for (int i = 0; i < slotBufferSize; i++)
                slots.Add(InventorySlotElement.Empty);
            return e;
        }

        // --- GetEffectiveCapacity ---

        [Test]
        public void GetEffectiveCapacity_NoModifiers_ReturnsBase()
        {
            var e = CreateInventoryEntity(5, 10, 5);
            var data = Manager.GetComponentData<InventoryData>(e);
            var capMods = Manager.GetBuffer<InventoryCapacityModifier>(e);
            Assert.AreEqual(5, InventoryUtility.GetEffectiveCapacity(in data, in capMods));
        }

        [Test]
        public void GetEffectiveCapacity_WithAddModifier()
        {
            var e = CreateInventoryEntity(5, 10, 8);
            var data = Manager.GetComponentData<InventoryData>(e);
            var capMods = Manager.GetBuffer<InventoryCapacityModifier>(e);
            capMods.Add(new InventoryCapacityModifier
            {
                Modifier = new ModifierElement { Id = 1, Op = ModifierOp.Add, Value = 3f, IsActive = true }
            });
            Assert.AreEqual(8, InventoryUtility.GetEffectiveCapacity(in data, in capMods));
        }

        [Test]
        public void GetEffectiveCapacity_ClampedToMax()
        {
            var e = CreateInventoryEntity(5, 6, 5);
            var data = Manager.GetComponentData<InventoryData>(e);
            var capMods = Manager.GetBuffer<InventoryCapacityModifier>(e);
            capMods.Add(new InventoryCapacityModifier
            {
                Modifier = new ModifierElement { Id = 1, Op = ModifierOp.Add, Value = 100f, IsActive = true }
            });
            Assert.AreEqual(6, InventoryUtility.GetEffectiveCapacity(in data, in capMods));
        }

        [Test]
        public void GetEffectiveCapacity_ClampedToZero()
        {
            var e = CreateInventoryEntity(5, 10, 5);
            var data = Manager.GetComponentData<InventoryData>(e);
            var capMods = Manager.GetBuffer<InventoryCapacityModifier>(e);
            capMods.Add(new InventoryCapacityModifier
            {
                Modifier = new ModifierElement { Id = 1, Op = ModifierOp.Add, Value = -100f, IsActive = true }
            });
            Assert.AreEqual(0, InventoryUtility.GetEffectiveCapacity(in data, in capMods));
        }

        // --- Add ---

        [Test]
        public void Add_EmptyInventory_PlacesInFirstSlot()
        {
            var e = CreateInventoryEntity(3, 10, 3);
            var slots = Manager.GetBuffer<InventorySlotElement>(e);
            var data = Manager.GetComponentData<InventoryData>(e);
            var capMods = Manager.GetBuffer<InventoryCapacityModifier>(e);

            int overflow = InventoryUtility.Add(ref slots, in data, in capMods, 1, MaxStack, 5);

            Assert.AreEqual(0, overflow);
            Assert.AreEqual(1, slots[0].ItemId);
            Assert.AreEqual(5, slots[0].Count);
        }

        [Test]
        public void Add_PartialStackFillFirst()
        {
            var e = CreateInventoryEntity(3, 10, 3);
            var slots = Manager.GetBuffer<InventorySlotElement>(e);
            slots[0] = new InventorySlotElement { ItemId = 1, Count = 7 };
            var data = Manager.GetComponentData<InventoryData>(e);
            var capMods = Manager.GetBuffer<InventoryCapacityModifier>(e);

            int overflow = InventoryUtility.Add(ref slots, in data, in capMods, 1, MaxStack, 5);

            Assert.AreEqual(0, overflow);
            Assert.AreEqual(10, slots[0].Count); // filled to max
            Assert.AreEqual(1, slots[1].ItemId);
            Assert.AreEqual(2, slots[1].Count);  // remaining
        }

        [Test]
        public void Add_Overflow_ReturnsRemainder()
        {
            var e = CreateInventoryEntity(1, 10, 1);
            var slots = Manager.GetBuffer<InventorySlotElement>(e);
            var data = Manager.GetComponentData<InventoryData>(e);
            var capMods = Manager.GetBuffer<InventoryCapacityModifier>(e);

            int overflow = InventoryUtility.Add(ref slots, in data, in capMods, 1, MaxStack, 15);

            Assert.AreEqual(5, overflow); // 1 slot * 10 max = 10, overflow = 5
        }

        [Test]
        public void Add_MultiStack()
        {
            var e = CreateInventoryEntity(3, 10, 3);
            var slots = Manager.GetBuffer<InventorySlotElement>(e);
            var data = Manager.GetComponentData<InventoryData>(e);
            var capMods = Manager.GetBuffer<InventoryCapacityModifier>(e);

            int overflow = InventoryUtility.Add(ref slots, in data, in capMods, 1, MaxStack, 25);

            Assert.AreEqual(0, overflow);
            Assert.AreEqual(10, slots[0].Count);
            Assert.AreEqual(10, slots[1].Count);
            Assert.AreEqual(5, slots[2].Count);
        }

        [Test]
        public void Add_FullInventory_ReturnsAllAsOverflow()
        {
            var e = CreateInventoryEntity(1, 10, 1);
            var slots = Manager.GetBuffer<InventorySlotElement>(e);
            slots[0] = new InventorySlotElement { ItemId = 1, Count = MaxStack };
            var data = Manager.GetComponentData<InventoryData>(e);
            var capMods = Manager.GetBuffer<InventoryCapacityModifier>(e);

            int overflow = InventoryUtility.Add(ref slots, in data, in capMods, 1, MaxStack, 5);

            Assert.AreEqual(5, overflow);
        }

        [Test]
        public void Add_RespectsEffectiveCapacity()
        {
            // 3 buffer slots but effective cap = 1
            var e = CreateInventoryEntity(1, 10, 3);
            var slots = Manager.GetBuffer<InventorySlotElement>(e);
            var data = Manager.GetComponentData<InventoryData>(e);
            var capMods = Manager.GetBuffer<InventoryCapacityModifier>(e);

            int overflow = InventoryUtility.Add(ref slots, in data, in capMods, 1, MaxStack, 15);

            Assert.AreEqual(5, overflow); // only 1 effective slot * 10 max
        }

        // --- TryRemove ---

        [Test]
        public void TryRemove_SufficientItems_ReturnsTrue()
        {
            var e = CreateInventoryEntity(3, 10, 3);
            var slots = Manager.GetBuffer<InventorySlotElement>(e);
            slots[0] = new InventorySlotElement { ItemId = 1, Count = 8 };
            var data = Manager.GetComponentData<InventoryData>(e);
            var capMods = Manager.GetBuffer<InventoryCapacityModifier>(e);

            Assert.IsTrue(InventoryUtility.TryRemove(ref slots, in data, in capMods, 1, 5));
            Assert.AreEqual(3, slots[0].Count);
        }

        [Test]
        public void TryRemove_InsufficientItems_ReturnsFalse_NoMutation()
        {
            var e = CreateInventoryEntity(3, 10, 3);
            var slots = Manager.GetBuffer<InventorySlotElement>(e);
            slots[0] = new InventorySlotElement { ItemId = 1, Count = 3 };
            var data = Manager.GetComponentData<InventoryData>(e);
            var capMods = Manager.GetBuffer<InventoryCapacityModifier>(e);

            Assert.IsFalse(InventoryUtility.TryRemove(ref slots, in data, in capMods, 1, 10));
            Assert.AreEqual(3, slots[0].Count); // unchanged
        }

        [Test]
        public void TryRemove_BackToFront_RemovesFromLastSlotFirst()
        {
            var e = CreateInventoryEntity(3, 10, 3);
            var slots = Manager.GetBuffer<InventorySlotElement>(e);
            slots[0] = new InventorySlotElement { ItemId = 1, Count = 5 };
            slots[2] = new InventorySlotElement { ItemId = 1, Count = 5 };
            var data = Manager.GetComponentData<InventoryData>(e);
            var capMods = Manager.GetBuffer<InventoryCapacityModifier>(e);

            InventoryUtility.TryRemove(ref slots, in data, in capMods, 1, 3);

            Assert.AreEqual(5, slots[0].Count);  // untouched
            Assert.AreEqual(2, slots[2].Count);   // reduced
        }

        [Test]
        public void TryRemove_ClearsEmptySlots()
        {
            var e = CreateInventoryEntity(3, 10, 3);
            var slots = Manager.GetBuffer<InventorySlotElement>(e);
            slots[0] = new InventorySlotElement { ItemId = 1, Count = 3 };
            var data = Manager.GetComponentData<InventoryData>(e);
            var capMods = Manager.GetBuffer<InventoryCapacityModifier>(e);

            InventoryUtility.TryRemove(ref slots, in data, in capMods, 1, 3);

            Assert.IsTrue(slots[0].IsEmpty);
        }

        // --- SwapSlots ---

        [Test]
        public void SwapSlots_BasicSwap()
        {
            var e = CreateInventoryEntity(2, 10, 2);
            var slots = Manager.GetBuffer<InventorySlotElement>(e);
            slots[0] = new InventorySlotElement { ItemId = 1, Count = 5 };
            slots[1] = new InventorySlotElement { ItemId = 2, Count = 3 };

            InventoryUtility.SwapSlots(ref slots, 0, ref slots, 1);

            Assert.AreEqual(2, slots[0].ItemId);
            Assert.AreEqual(3, slots[0].Count);
            Assert.AreEqual(1, slots[1].ItemId);
            Assert.AreEqual(5, slots[1].Count);
        }

        [Test]
        public void SwapSlots_WithEmpty()
        {
            var e = CreateInventoryEntity(2, 10, 2);
            var slots = Manager.GetBuffer<InventorySlotElement>(e);
            slots[0] = new InventorySlotElement { ItemId = 1, Count = 5 };

            InventoryUtility.SwapSlots(ref slots, 0, ref slots, 1);

            Assert.IsTrue(slots[0].IsEmpty);
            Assert.AreEqual(1, slots[1].ItemId);
        }

        [Test]
        public void SwapSlots_BetweenEntities()
        {
            var e1 = CreateInventoryEntity(1, 10, 1);
            var e2 = CreateInventoryEntity(1, 10, 1);
            var s1 = Manager.GetBuffer<InventorySlotElement>(e1);
            var s2 = Manager.GetBuffer<InventorySlotElement>(e2);
            s1[0] = new InventorySlotElement { ItemId = 1, Count = 5 };
            s2[0] = new InventorySlotElement { ItemId = 2, Count = 3 };

            InventoryUtility.SwapSlots(ref s1, 0, ref s2, 0);

            Assert.AreEqual(2, s1[0].ItemId);
            Assert.AreEqual(1, s2[0].ItemId);
        }

        // --- GetCount ---

        [Test]
        public void GetCount_TotalAcrossSlots()
        {
            var e = CreateInventoryEntity(3, 10, 3);
            var slots = Manager.GetBuffer<InventorySlotElement>(e);
            slots[0] = new InventorySlotElement { ItemId = 1, Count = 5 };
            slots[1] = new InventorySlotElement { ItemId = 2, Count = 3 };
            slots[2] = new InventorySlotElement { ItemId = 1, Count = 7 };

            Assert.AreEqual(12, InventoryUtility.GetCount(in slots, 1, 3));
        }

        [Test]
        public void GetCount_AbsentItem_ReturnsZero()
        {
            var e = CreateInventoryEntity(3, 10, 3);
            var slots = Manager.GetBuffer<InventorySlotElement>(e);

            Assert.AreEqual(0, InventoryUtility.GetCount(in slots, 99, 3));
        }

        [Test]
        public void GetCount_RespectsEffectiveCapacity()
        {
            var e = CreateInventoryEntity(3, 10, 3);
            var slots = Manager.GetBuffer<InventorySlotElement>(e);
            slots[0] = new InventorySlotElement { ItemId = 1, Count = 5 };
            slots[1] = new InventorySlotElement { ItemId = 1, Count = 5 };
            slots[2] = new InventorySlotElement { ItemId = 1, Count = 5 };

            // Only count first 2 slots
            Assert.AreEqual(10, InventoryUtility.GetCount(in slots, 1, 2));
        }

        // --- GetUsedSlotCount ---

        [Test]
        public void GetUsedSlotCount_Basic()
        {
            var e = CreateInventoryEntity(3, 10, 3);
            var slots = Manager.GetBuffer<InventorySlotElement>(e);
            slots[0] = new InventorySlotElement { ItemId = 1, Count = 5 };
            slots[2] = new InventorySlotElement { ItemId = 2, Count = 3 };

            Assert.AreEqual(2, InventoryUtility.GetUsedSlotCount(in slots, 3));
        }

        [Test]
        public void GetUsedSlotCount_Empty_ReturnsZero()
        {
            var e = CreateInventoryEntity(3, 10, 3);
            var slots = Manager.GetBuffer<InventorySlotElement>(e);

            Assert.AreEqual(0, InventoryUtility.GetUsedSlotCount(in slots, 3));
        }

        // --- IsFull ---

        [Test]
        public void IsFull_AllSlotsFilled_ReturnsTrue()
        {
            var e = CreateInventoryEntity(2, 10, 2);
            var slots = Manager.GetBuffer<InventorySlotElement>(e);
            slots[0] = new InventorySlotElement { ItemId = 1, Count = 1 };
            slots[1] = new InventorySlotElement { ItemId = 2, Count = 1 };

            Assert.IsTrue(InventoryUtility.IsFull(in slots, 2));
        }

        [Test]
        public void IsFull_HasEmptySlot_ReturnsFalse()
        {
            var e = CreateInventoryEntity(2, 10, 2);
            var slots = Manager.GetBuffer<InventorySlotElement>(e);
            slots[0] = new InventorySlotElement { ItemId = 1, Count = 1 };

            Assert.IsFalse(InventoryUtility.IsFull(in slots, 2));
        }

        [Test]
        public void IsFull_ZeroCapacity_ReturnsFalse()
        {
            var e = CreateInventoryEntity(0, 10, 0);
            var slots = Manager.GetBuffer<InventorySlotElement>(e);

            Assert.IsFalse(InventoryUtility.IsFull(in slots, 0));
        }
    }
}
