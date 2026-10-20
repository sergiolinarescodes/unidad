namespace Unidad.Flow
{
    public readonly record struct FlowStateChangedEvent(FlowState From, FlowState To);

    public readonly record struct SceneSwitchRequestedEvent(SceneId To, ISceneTicket Ticket);
}
