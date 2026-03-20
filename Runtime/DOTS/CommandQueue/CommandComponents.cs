using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    public enum CommandType : byte
    {
        None,
        Wait,
        // Game-specific types should start at 32+
    }

    public enum CommandStatus : byte
    {
        Pending,
        Running,
        Completed,
        Failed
    }

    public struct CommandQueueData : IComponentData
    {
        [MarshalAs(UnmanagedType.U1)]
        public bool IsPaused;
        public int CurrentIndex;
    }

    public struct CommandEntry : IBufferElementData
    {
        public CommandType Type;
        public CommandStatus Status;
        public float Duration;
        public float Elapsed;
        public float3 TargetPosition;
        public int IntParam;
        public float FloatParam;
    }

    public struct CommandCompleted : IComponentData, IEnableableComponent { }
    public struct CommandFailed : IComponentData, IEnableableComponent { }
    public struct QueueEmpty : IComponentData, IEnableableComponent { }
}
