using Unity.Entities;

namespace Unidad.Core.DOTS
{
    public struct ResourceElement : IBufferElementData
    {
        public int ResourceId;
        public float CurrentValue;
        public float InitialValue;
        public float BaseMin;
        public float BaseMax;
    }

    public struct ResourceMaxModifier : IBufferElementData
    {
        public int ResourceId;
        public ModifierElement Modifier;
    }

    public struct ResourceMinModifier : IBufferElementData
    {
        public int ResourceId;
        public ModifierElement Modifier;
    }

    public struct ResourceChanged : IComponentData, IEnableableComponent { }
    public struct ResourceDepleted : IComponentData, IEnableableComponent { }
    public struct ResourceFilled : IComponentData, IEnableableComponent { }

    public struct ResourceChangeRecord : IBufferElementData
    {
        public int ResourceId;
        public float OldValue;
        public float NewValue;
        public float EffectiveMax;
        public float EffectiveMin;
    }
}
