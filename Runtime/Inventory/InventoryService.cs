using System;
using System.Collections.Generic;
using Unidad.Core.EventBus;
using Unidad.Core.Patterns.Modifier;
using Unidad.Core.Systems;

namespace Unidad.Core.Inventory
{
    internal sealed class InventoryService : SystemServiceBase, IInventoryService
    {
        private readonly Dictionary<string, InventoryEntry> _inventories = new();
        private readonly Dictionary<string, ItemDefinition> _itemDefinitions = new();

        public InventoryService(IEventBus eventBus) : base(eventBus) { }

        public void Create(InventoryId id, InventoryDefinition definition)
        {
            if (_inventories.ContainsKey(id.Value))
                throw new InvalidOperationException($"Inventory '{id.Value}' already exists.");

            var entry = new InventoryEntry(definition);
            _inventories[id.Value] = entry;
            Publish(new InventoryCreatedEvent(id, definition.SlotCount));
        }

        public bool Exists(InventoryId id) => _inventories.ContainsKey(id.Value);

        public void Remove(InventoryId id)
        {
            if (!_inventories.Remove(id.Value))
                throw new KeyNotFoundException($"Inventory '{id.Value}' does not exist.");
            Publish(new InventoryRemovedEvent(id));
        }

        public void DefineItem(ItemDefinition definition)
        {
            _itemDefinitions[definition.Id.Value] = definition;
        }

        public bool HasItemDefinition(ItemId itemId) => _itemDefinitions.ContainsKey(itemId.Value);

        public int Add(InventoryId inventoryId, ItemId itemId, int count = 1)
        {
            var entry = GetEntry(inventoryId);
            var itemDef = GetItemDefinition(itemId);
            var effectiveCapacity = GetEffectiveCapacity(entry);
            var remaining = count;

            // First pass: fill existing partial stacks
            for (int i = 0; i < effectiveCapacity && remaining > 0; i++)
            {
                var slot = entry.Slots[i];
                if (slot.IsEmpty || slot.ItemId != itemId) continue;

                var space = itemDef.MaxStackSize - slot.Count;
                if (space <= 0) continue;

                var toAdd = Math.Min(remaining, space);
                var oldSlot = slot;
                var newSlot = new InventorySlot(itemId, slot.Count + toAdd);
                entry.Slots[i] = newSlot;
                remaining -= toAdd;

                Publish(new SlotChangedEvent(inventoryId, i, oldSlot, newSlot));
                Publish(new ItemAddedEvent(inventoryId, itemId, toAdd, i));
            }

            // Second pass: fill empty slots
            for (int i = 0; i < effectiveCapacity && remaining > 0; i++)
            {
                if (!entry.Slots[i].IsEmpty) continue;

                var toAdd = Math.Min(remaining, itemDef.MaxStackSize);
                var oldSlot = entry.Slots[i];
                var newSlot = new InventorySlot(itemId, toAdd);
                entry.Slots[i] = newSlot;
                remaining -= toAdd;

                Publish(new SlotChangedEvent(inventoryId, i, oldSlot, newSlot));
                Publish(new ItemAddedEvent(inventoryId, itemId, toAdd, i));
            }

            if (remaining > 0)
                Publish(new InventoryFullEvent(inventoryId, itemId, remaining));

            return remaining;
        }

        public bool TryRemove(InventoryId inventoryId, ItemId itemId, int count = 1)
        {
            var entry = GetEntry(inventoryId);
            var effectiveCapacity = GetEffectiveCapacity(entry);

            // Check if we have enough
            var totalCount = 0;
            for (int i = 0; i < effectiveCapacity; i++)
            {
                if (entry.Slots[i].ItemId == itemId)
                    totalCount += entry.Slots[i].Count;
            }

            if (totalCount < count) return false;

            var remaining = count;

            // Remove from slots (back to front to empty trailing slots first)
            for (int i = effectiveCapacity - 1; i >= 0 && remaining > 0; i--)
            {
                var slot = entry.Slots[i];
                if (slot.ItemId != itemId) continue;

                var toRemove = Math.Min(remaining, slot.Count);
                var oldSlot = slot;
                var newCount = slot.Count - toRemove;
                var newSlot = newCount > 0 ? new InventorySlot(itemId, newCount) : InventorySlot.Empty;
                entry.Slots[i] = newSlot;
                remaining -= toRemove;

                Publish(new SlotChangedEvent(inventoryId, i, oldSlot, newSlot));
                Publish(new ItemRemovedEvent(inventoryId, itemId, toRemove, i));
            }

            return true;
        }

        public int GetCount(InventoryId inventoryId, ItemId itemId)
        {
            var entry = GetEntry(inventoryId);
            var effectiveCapacity = GetEffectiveCapacity(entry);
            var total = 0;
            for (int i = 0; i < effectiveCapacity; i++)
            {
                if (entry.Slots[i].ItemId == itemId)
                    total += entry.Slots[i].Count;
            }
            return total;
        }

        public bool Contains(InventoryId inventoryId, ItemId itemId, int count = 1)
        {
            return GetCount(inventoryId, itemId) >= count;
        }

        public InventorySlot GetSlot(InventoryId inventoryId, int slotIndex)
        {
            var entry = GetEntry(inventoryId);
            var effectiveCapacity = GetEffectiveCapacity(entry);
            if (slotIndex < 0 || slotIndex >= effectiveCapacity)
                throw new ArgumentOutOfRangeException(nameof(slotIndex));
            return entry.Slots[slotIndex];
        }

        public int GetSlotCount(InventoryId inventoryId)
        {
            return GetEffectiveCapacity(GetEntry(inventoryId));
        }

        public int GetUsedSlotCount(InventoryId inventoryId)
        {
            var entry = GetEntry(inventoryId);
            var effectiveCapacity = GetEffectiveCapacity(entry);
            var used = 0;
            for (int i = 0; i < effectiveCapacity; i++)
            {
                if (!entry.Slots[i].IsEmpty)
                    used++;
            }
            return used;
        }

        public int GetFreeSlotCount(InventoryId inventoryId)
        {
            return GetSlotCount(inventoryId) - GetUsedSlotCount(inventoryId);
        }

        public bool IsFull(InventoryId inventoryId)
        {
            return GetFreeSlotCount(inventoryId) == 0;
        }

        public void SwapSlots(InventoryId srcInv, int srcSlot, InventoryId dstInv, int dstSlot)
        {
            var srcEntry = GetEntry(srcInv);
            var dstEntry = GetEntry(dstInv);
            var srcCapacity = GetEffectiveCapacity(srcEntry);
            var dstCapacity = GetEffectiveCapacity(dstEntry);

            if (srcSlot < 0 || srcSlot >= srcCapacity)
                throw new ArgumentOutOfRangeException(nameof(srcSlot));
            if (dstSlot < 0 || dstSlot >= dstCapacity)
                throw new ArgumentOutOfRangeException(nameof(dstSlot));

            var oldSrc = srcEntry.Slots[srcSlot];
            var oldDst = dstEntry.Slots[dstSlot];

            srcEntry.Slots[srcSlot] = oldDst;
            dstEntry.Slots[dstSlot] = oldSrc;

            if (oldSrc != srcEntry.Slots[srcSlot])
                Publish(new SlotChangedEvent(srcInv, srcSlot, oldSrc, srcEntry.Slots[srcSlot]));
            if (oldDst != dstEntry.Slots[dstSlot])
                Publish(new SlotChangedEvent(dstInv, dstSlot, oldDst, dstEntry.Slots[dstSlot]));
        }

        public void MoveSlot(InventoryId srcInv, int srcSlot, InventoryId dstInv, int dstSlot)
        {
            var srcEntry = GetEntry(srcInv);
            var dstEntry = GetEntry(dstInv);
            var srcCapacity = GetEffectiveCapacity(srcEntry);
            var dstCapacity = GetEffectiveCapacity(dstEntry);

            if (srcSlot < 0 || srcSlot >= srcCapacity)
                throw new ArgumentOutOfRangeException(nameof(srcSlot));
            if (dstSlot < 0 || dstSlot >= dstCapacity)
                throw new ArgumentOutOfRangeException(nameof(dstSlot));

            if (!dstEntry.Slots[dstSlot].IsEmpty)
                throw new InvalidOperationException($"Destination slot {dstSlot} is not empty.");

            var oldSrc = srcEntry.Slots[srcSlot];
            var oldDst = dstEntry.Slots[dstSlot];

            dstEntry.Slots[dstSlot] = oldSrc;
            srcEntry.Slots[srcSlot] = InventorySlot.Empty;

            Publish(new SlotChangedEvent(srcInv, srcSlot, oldSrc, InventorySlot.Empty));
            Publish(new SlotChangedEvent(dstInv, dstSlot, oldDst, dstEntry.Slots[dstSlot]));
        }

        public ModifierStack<float> GetCapacityModifiers(InventoryId inventoryId)
        {
            return GetEntry(inventoryId).CapacityModifiers;
        }

        private InventoryEntry GetEntry(InventoryId id)
        {
            if (!_inventories.TryGetValue(id.Value, out var entry))
                throw new KeyNotFoundException($"Inventory '{id.Value}' does not exist.");
            return entry;
        }

        private ItemDefinition GetItemDefinition(ItemId id)
        {
            if (!_itemDefinitions.TryGetValue(id.Value, out var def))
                throw new KeyNotFoundException($"Item '{id.Value}' is not defined.");
            return def;
        }

        private static int GetEffectiveCapacity(InventoryEntry entry)
        {
            var raw = entry.CapacityModifiers.Evaluate(entry.Definition.SlotCount);
            var effective = (int)raw;
            // Never exceed the underlying array length
            return Math.Min(effective, entry.Slots.Length);
        }

        private sealed class InventoryEntry
        {
            public readonly InventoryDefinition Definition;
            public readonly InventorySlot[] Slots;
            public readonly ModifierStack<float> CapacityModifiers = new();

            public InventoryEntry(InventoryDefinition definition)
            {
                Definition = definition;
                // Allocate double the base capacity to allow modifier growth
                Slots = new InventorySlot[definition.SlotCount * 2];
                for (int i = 0; i < Slots.Length; i++)
                    Slots[i] = InventorySlot.Empty;
            }
        }
    }
}
