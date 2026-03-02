using System;
using System.Collections.Generic;
using Unidad.Core.EventBus;
using Unidad.Core.Testing;
using UnityEngine;

namespace Unidad.Core.Inventory.Scenarios
{
    /// <summary>
    /// Visual scenario: displays a grid of colored quads representing inventory slots.
    /// Defines item types, adds items, and shows occupied/empty slots.
    /// </summary>
    internal sealed class InventoryScenario : DataDrivenScenario
    {
        private static readonly ScenarioParameter SlotCountParam = new(
            "slotCount", "Slot Count", typeof(int), 8, 2, 16);

        private static readonly ScenarioParameter ItemTypesParam = new(
            "itemTypes", "Item Types", typeof(int), 3, 1, 5);

        private static readonly ScenarioParameter ItemsPerTypeParam = new(
            "itemsPerType", "Items Per Type", typeof(int), 5, 1, 20);

        private IEventBus _eventBus;
        private InventoryService _inventoryService;
        private readonly List<IDisposable> _subscriptions = new();
        private InventoryId _inventoryId;
        private readonly List<GameObject> _slotVisuals = new();
        private int _expectedSlotCount;
        private bool _inventoryCreated;
        private int _totalItemsAdded;
        private int _totalOverflow;

        private static readonly Color EmptySlotColor = new(0.3f, 0.3f, 0.3f);
        private static readonly Color[] ItemColors =
        {
            new(0.9f, 0.2f, 0.2f),
            new(0.2f, 0.5f, 0.9f),
            new(0.2f, 0.8f, 0.3f),
            new(0.9f, 0.8f, 0.1f),
            new(0.7f, 0.3f, 0.9f)
        };

        private static readonly string[] ItemNames = { "sword", "potion", "shield", "gem", "scroll" };

        public InventoryScenario() : base(new TestScenarioDefinition(
            "inventory-slots",
            "Inventory Slots (Visual)",
            "Displays a grid of colored quads representing inventory slots. " +
            "Defines item types with different stack sizes, adds items, and shows occupied/empty slots.",
            new[] { SlotCountParam, ItemTypesParam, ItemsPerTypeParam }
        )) { }

        protected override void ExecuteInternal(ScenarioParameterOverrides overrides)
        {
            var slotCount = Mathf.Clamp(ResolveParam<int>(overrides, "slotCount"), 2, 16);
            var itemTypes = Mathf.Clamp(ResolveParam<int>(overrides, "itemTypes"), 1, 5);
            var itemsPerType = Mathf.Clamp(ResolveParam<int>(overrides, "itemsPerType"), 1, 20);

            _expectedSlotCount = slotCount;
            _inventoryCreated = false;
            _totalItemsAdded = 0;
            _totalOverflow = 0;
            _slotVisuals.Clear();

            _eventBus = new EventBus.EventBus();
            _inventoryService = new InventoryService(_eventBus);

            // Subscribe to events
            _subscriptions.Add(_eventBus.Subscribe<InventoryCreatedEvent>(evt =>
            {
                Debug.Log($"[InventoryScenario] Inventory created: {evt.InventoryId} slots={evt.SlotCount}");
            }));
            _subscriptions.Add(_eventBus.Subscribe<ItemAddedEvent>(evt =>
            {
                Debug.Log($"[InventoryScenario] Item added: {evt.ItemId} x{evt.Count} -> slot {evt.SlotIndex}");
            }));
            _subscriptions.Add(_eventBus.Subscribe<InventoryFullEvent>(evt =>
            {
                Debug.Log($"[InventoryScenario] FULL! Overflow: {evt.ItemId} x{evt.OverflowCount}");
            }));
            _subscriptions.Add(_eventBus.Subscribe<SlotChangedEvent>(evt =>
            {
                Debug.Log($"[InventoryScenario] Slot {evt.SlotIndex}: {evt.OldSlot} -> {evt.NewSlot}");
            }));

            // Create inventory
            _inventoryId = new InventoryId("player-bag");
            _inventoryService.Create(_inventoryId, new InventoryDefinition(slotCount));
            _inventoryCreated = _inventoryService.Exists(_inventoryId);

            // Define item types with varying stack sizes
            for (int i = 0; i < itemTypes; i++)
            {
                var itemId = new ItemId(ItemNames[i % ItemNames.Length]);
                var maxStack = (i + 1) * 5; // 5, 10, 15, 20, 25
                _inventoryService.DefineItem(new ItemDefinition(itemId, ItemNames[i % ItemNames.Length], maxStack));
            }

            // Add items
            for (int i = 0; i < itemTypes; i++)
            {
                var itemId = new ItemId(ItemNames[i % ItemNames.Length]);
                var overflow = _inventoryService.Add(_inventoryId, itemId, itemsPerType);
                _totalItemsAdded += itemsPerType - overflow;
                _totalOverflow += overflow;
            }

            // Build visual grid
            var cols = Mathf.CeilToInt(Mathf.Sqrt(slotCount));
            var spacing = 1.2f;
            var startX = -(cols - 1) * spacing * 0.5f;

            for (int i = 0; i < slotCount; i++)
            {
                var col = i % cols;
                var row = i / cols;

                var slot = _inventoryService.GetSlot(_inventoryId, i);
                var color = slot.IsEmpty ? EmptySlotColor : ItemColors[GetItemColorIndex(slot.ItemId)];

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = $"[Scenario] Slot {i} ({slot})";
                quad.transform.SetParent(SceneRoot.transform);
                quad.transform.localPosition = new Vector3(startX + col * spacing, -row * spacing, 0f);
                SetColor(quad, color);
                _slotVisuals.Add(quad);
            }

            Debug.Log($"[InventoryScenario] Complete — {slotCount} slots, {itemTypes} item types, " +
                      $"{_totalItemsAdded} items added, {_totalOverflow} overflow, " +
                      $"used={_inventoryService.GetUsedSlotCount(_inventoryId)} free={_inventoryService.GetFreeSlotCount(_inventoryId)}");
        }

        protected override ScenarioVerificationResult VerifyInternal(ScenarioParameterOverrides overrides)
        {
            var checks = new List<ScenarioVerificationResult.CheckResult>
            {
                new("Scene root created", SceneRoot != null,
                    SceneRoot != null ? null : "No scene root"),
                new("Inventory created", _inventoryCreated,
                    _inventoryCreated ? null : "Inventory was not created"),
                new($"All {_expectedSlotCount} slot visuals spawned",
                    _slotVisuals.Count == _expectedSlotCount,
                    _slotVisuals.Count == _expectedSlotCount ? null
                        : $"Expected {_expectedSlotCount}, got {_slotVisuals.Count}"),
                new("Slot count matches",
                    _inventoryService != null && _inventoryService.GetSlotCount(_inventoryId) == _expectedSlotCount,
                    _inventoryService != null && _inventoryService.GetSlotCount(_inventoryId) == _expectedSlotCount
                        ? null : "Slot count mismatch")
            };
            return new ScenarioVerificationResult(checks);
        }

        protected override void OnCleanup()
        {
            foreach (var sub in _subscriptions) sub.Dispose();
            _subscriptions.Clear();
            _slotVisuals.Clear();

            _eventBus?.ClearAllSubscriptions();
            _eventBus = null;
            _inventoryService = null;
        }

        private int GetItemColorIndex(ItemId itemId)
        {
            for (int i = 0; i < ItemNames.Length; i++)
            {
                if (ItemNames[i] == itemId.Value) return i;
            }
            return 0;
        }

        private static void SetColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            renderer.sharedMaterial = mat;
        }
    }
}
