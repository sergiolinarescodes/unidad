using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Core agent identity. Every agent entity has this.
    /// ArchetypeId maps to game-defined agent types (e.g., citizen=1, worker=2).
    /// StrategyId selects which strategy set governs behavior.
    /// </summary>
    public struct AgentData : IComponentData
    {
        public int AgentId;
        public int ArchetypeId;
        public int StrategyId;
        public AgentLifecycleState LifecycleState;
    }

    public enum AgentLifecycleState : byte
    {
        Initializing = 0,
        Active = 1,
        Suspended = 2,
        Despawning = 3
    }

    /// <summary>
    /// Agent's current target entity and position. Used by action and navigation systems.
    /// TargetEntity may be Entity.Null if the target is a world position only.
    /// </summary>
    public struct AgentTarget : IComponentData
    {
        public Entity TargetEntity;
        public float3 TargetPosition;
        public int TargetType;
    }

    /// <summary>
    /// Agent's locomotion state. MoveSpeed is the agent's base desired speed;
    /// actual movement is composited with modifiers via ModifierUtility.
    /// </summary>
    public struct AgentLocomotion : IComponentData
    {
        public float BaseMoveSpeed;
        public float CurrentMoveSpeed;
        public float StoppingDistance;
        public float3 DesiredDirection;
        [MarshalAs(UnmanagedType.U1)]
        public bool IsMoving;
    }

    // --- Lifecycle event tags (1-frame, cleared by AgentEventClearSystem) ---

    public struct AgentSpawned : IComponentData, IEnableableComponent { }
    public struct AgentActivated : IComponentData, IEnableableComponent { }
    public struct AgentSuspended : IComponentData, IEnableableComponent { }
    public struct AgentDespawning : IComponentData, IEnableableComponent { }


}
