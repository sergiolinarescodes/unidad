using Unity.Entities;

namespace Unidad.Core.DOTS
{
    public struct TimerData : IComponentData
    {
        public float Duration;
        public float Elapsed;
        public bool Paused;
        public bool Loop;
    }

    public struct TimerCompleted : IComponentData, IEnableableComponent { }

    public struct TimerCancelled : IComponentData, IEnableableComponent { }

    public struct TimerOwner : IComponentData
    {
        public Entity Owner;
    }
}
