using System.Runtime.InteropServices;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Maps an ActionType to navigation and execution parameters.
    /// Stored as a buffer on a singleton entity alongside ActionBridgeConfig.
    /// The framework's ActionBridgeSystem reads this to automatically handle
    /// Starting → Navigating → Executing phase transitions.
    ///
    /// ActionTypes not in this buffer execute in place with DefaultInPlaceDuration.
    /// Set HandledByFramework = false for actions that need custom game logic.
    /// </summary>
    public struct ActionTargetMappingElement : IBufferElementData
    {
        public int ActionType;
        public int TargetPOIType;
        public float ExecutionDuration;
        [MarshalAs(UnmanagedType.U1)]
        public bool HandledByFramework;
    }

    /// <summary>
    /// Singleton config for ActionBridgeSystem. Create via ActionTargetMappingBuilder.
    /// The system only runs when this singleton exists (opt-in).
    /// </summary>
    public struct ActionBridgeConfig : IComponentData
    {
        public float DefaultInPlaceDuration;
        [MarshalAs(UnmanagedType.U1)]
        public bool LockScoringDuringExecution;

        public static ActionBridgeConfig Default => new ActionBridgeConfig
        {
            DefaultInPlaceDuration = 3f,
            LockScoringDuringExecution = true
        };
    }
}
