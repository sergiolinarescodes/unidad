using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Per-agent queue behavior configuration. Controls how actions are queued and interrupted.
    /// </summary>
    public struct ActionQueueConfig : IComponentData
    {
        public ActionQueueMode Mode;
        public InterruptPolicy InterruptPolicy;
        public float InterruptPriorityThreshold;
        [MarshalAs(UnmanagedType.U1)]
        public bool AllowRescore;

        public static ActionQueueConfig Default => new ActionQueueConfig
        {
            Mode = ActionQueueMode.SingleAction,
            InterruptPolicy = InterruptPolicy.Immediate,
            InterruptPriorityThreshold = 0f,
            AllowRescore = true
        };
    }

    public enum ActionQueueMode : byte
    {
        /// <summary>Scoring picks one action, interrupts previous. Default, backward-compatible.</summary>
        SingleAction = 0,
        /// <summary>Strategy can enqueue a sequence of actions (plan).</summary>
        QueueFromStrategy = 1,
        /// <summary>Game code enqueues actions directly.</summary>
        QueueManual = 2
    }

    public enum InterruptPolicy : byte
    {
        /// <summary>Drop everything when a higher-priority event fires.</summary>
        Immediate = 0,
        /// <summary>Wait for current action to complete, then handle interrupt.</summary>
        FinishCurrent = 1,
        /// <summary>Complete the entire queued plan before accepting new actions.</summary>
        FinishQueue = 2,
        /// <summary>Only interrupt if new action score exceeds threshold.</summary>
        PriorityBased = 3
    }

    /// <summary>
    /// One entry in the action queue. Used by QueueFromStrategy and QueueManual modes.
    /// </summary>
    public struct ActionQueueEntry : IBufferElementData
    {
        public int ActionId;
        public int ActionType;
        public int SequenceIndex;
        public float3 TargetPosition;
        public Entity TargetEntity;
        public ActionQueueEntryStatus Status;
    }

    public enum ActionQueueEntryStatus : byte
    {
        Pending = 0,
        Active = 1,
        Completed = 2,
        Skipped = 3
    }

    /// <summary>Progress along the current action queue.</summary>
    public struct ActionQueueProgress : IComponentData
    {
        public int CurrentIndex;
        public int TotalEntries;
        [MarshalAs(UnmanagedType.U1)]
        public bool QueuePaused;
    }

    // --- Queue events (1-frame) ---
    public struct QueueAdvanced : IComponentData, IEnableableComponent { }
    public struct QueueCompleted : IComponentData, IEnableableComponent { }
    public struct QueueInterrupted : IComponentData, IEnableableComponent { }
}
