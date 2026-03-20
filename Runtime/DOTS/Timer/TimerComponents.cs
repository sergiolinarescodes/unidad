using System.Runtime.InteropServices;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    public struct TimerData : IComponentData
    {
        public float Duration;
        public float Elapsed;
        [MarshalAs(UnmanagedType.U1)]
        public bool Paused;
        [MarshalAs(UnmanagedType.U1)]
        public bool Loop;
    }

    public struct TimerCompleted : IComponentData, IEnableableComponent { }

    public struct TimerCancelled : IComponentData, IEnableableComponent { }

    public struct TimerOwner : IComponentData
    {
        public Entity Owner;
    }
}
