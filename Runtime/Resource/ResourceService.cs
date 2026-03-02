using System;
using System.Collections.Generic;
using Unidad.Core.EventBus;
using Unidad.Core.Patterns.Modifier;
using Unidad.Core.Systems;

namespace Unidad.Core.Resource
{
    internal sealed class ResourceService : SystemServiceBase, IResourceService
    {
        private readonly Dictionary<string, ResourceEntry> _resources = new();

        public ResourceService(IEventBus eventBus) : base(eventBus) { }

        public void Define(ResourceId id, ResourceDefinition definition)
        {
            if (_resources.ContainsKey(id.Value))
                throw new InvalidOperationException($"Resource '{id.Value}' is already defined.");

            var entry = new ResourceEntry(definition);
            entry.CurrentValue = Clamp(definition.InitialValue, entry);
            _resources[id.Value] = entry;
        }

        public float Get(ResourceId id) => GetEntry(id).CurrentValue;

        public float GetBase(ResourceId id) => GetEntry(id).Definition.InitialValue;

        public void Set(ResourceId id, float value)
        {
            var entry = GetEntry(id);
            var oldValue = entry.CurrentValue;
            entry.CurrentValue = Clamp(value, entry);
            PublishChanges(id, oldValue, entry);
        }

        public void Add(ResourceId id, float amount)
        {
            var entry = GetEntry(id);
            var oldValue = entry.CurrentValue;
            entry.CurrentValue = Clamp(entry.CurrentValue + amount, entry);
            PublishChanges(id, oldValue, entry);
        }

        public bool TrySpend(ResourceId id, float amount)
        {
            var entry = GetEntry(id);
            var min = GetEffectiveMin(entry);
            if (entry.CurrentValue - amount < min)
                return false;

            var oldValue = entry.CurrentValue;
            entry.CurrentValue = Clamp(entry.CurrentValue - amount, entry);
            PublishChanges(id, oldValue, entry);
            return true;
        }

        public float GetMax(ResourceId id) => GetEffectiveMax(GetEntry(id));

        public float GetMin(ResourceId id) => GetEffectiveMin(GetEntry(id));

        public ModifierStack<float> GetMaxModifiers(ResourceId id) => GetEntry(id).MaxModifiers;

        public ModifierStack<float> GetMinModifiers(ResourceId id) => GetEntry(id).MinModifiers;

        public bool Has(ResourceId id) => _resources.ContainsKey(id.Value);

        private ResourceEntry GetEntry(ResourceId id)
        {
            if (!_resources.TryGetValue(id.Value, out var entry))
                throw new KeyNotFoundException($"Resource '{id.Value}' is not defined.");
            return entry;
        }

        private static float GetEffectiveMax(ResourceEntry entry) =>
            entry.MaxModifiers.Evaluate(entry.Definition.Max);

        private static float GetEffectiveMin(ResourceEntry entry) =>
            entry.MinModifiers.Evaluate(entry.Definition.Min);

        private static float Clamp(float value, ResourceEntry entry)
        {
            var max = GetEffectiveMax(entry);
            var min = GetEffectiveMin(entry);
            return Math.Max(min, Math.Min(max, value));
        }

        private void PublishChanges(ResourceId id, float oldValue, ResourceEntry entry)
        {
            if (Math.Abs(oldValue - entry.CurrentValue) < float.Epsilon) return;

            var max = GetEffectiveMax(entry);
            Publish(new ResourceChangedEvent(id, oldValue, entry.CurrentValue, max));

            var min = GetEffectiveMin(entry);
            if (entry.CurrentValue <= min && oldValue > min)
                Publish(new ResourceDepletedEvent(id));

            if (entry.CurrentValue >= max && oldValue < max)
                Publish(new ResourceFilledEvent(id));
        }

        private sealed class ResourceEntry
        {
            public readonly ResourceDefinition Definition;
            public readonly ModifierStack<float> MaxModifiers = new();
            public readonly ModifierStack<float> MinModifiers = new();
            public float CurrentValue;

            public ResourceEntry(ResourceDefinition definition)
            {
                Definition = definition;
            }
        }
    }
}
