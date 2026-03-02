namespace Unidad.Core.Progression
{
    public readonly record struct NodeUnlockedEvent(ProgressionTreeId TreeId, ProgressionNodeId NodeId);
    public readonly record struct NodeBecameAvailableEvent(ProgressionTreeId TreeId, ProgressionNodeId NodeId);
    public readonly record struct NodeRelockedEvent(ProgressionTreeId TreeId, ProgressionNodeId NodeId);
    public readonly record struct TreeResetEvent(ProgressionTreeId TreeId);
    public readonly record struct TreeCreatedEvent(ProgressionTreeId TreeId);
}
