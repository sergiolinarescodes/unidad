using Unidad.Core.Patterns.Modifier;

namespace Unidad.Core.Resource
{
    public interface IResourceService
    {
        void Define(ResourceId id, ResourceDefinition definition);
        float Get(ResourceId id);
        float GetBase(ResourceId id);
        void Set(ResourceId id, float value);
        void Add(ResourceId id, float amount);
        bool TrySpend(ResourceId id, float amount);
        float GetMax(ResourceId id);
        float GetMin(ResourceId id);
        ModifierStack<float> GetMaxModifiers(ResourceId id);
        ModifierStack<float> GetMinModifiers(ResourceId id);
        bool Has(ResourceId id);
    }
}
