using Unity.Entities;

namespace Unidad.Core.DOTS
{
    public struct StateMachineData : IComponentData
    {
        public int CurrentState;
        public int PreviousState;
        public bool TransitionRequested;
        public int RequestedState;
    }

    public struct StateEntered : IComponentData, IEnableableComponent { }

    public struct StateExited : IComponentData, IEnableableComponent { }
}
