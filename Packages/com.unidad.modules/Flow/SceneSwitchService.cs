using Unidad.Core.EventBus;
using Unidad.Core.Systems;

namespace Unidad.Flow
{
    internal sealed class SceneSwitchService : SystemServiceBase, ISceneSwitchService
    {
        private readonly IFlowService _flow;

        public SceneSwitchService(IEventBus bus, IFlowService flow) : base(bus) => _flow = flow;

        public SceneId? Pending { get; private set; }

        public bool SwitchTo(SceneId scene) => SwitchTo(scene, null);

        public bool SwitchTo(SceneId scene, ISceneTicket ticket)
        {
            if (Pending.HasValue || _flow.State is not (FlowState.Play or FlowState.Paused)) return false;
            Pending = scene;
            Publish(new SceneSwitchRequestedEvent(scene, ticket));
            return true;
        }
    }
}
