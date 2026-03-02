using System.Collections.Generic;

namespace Unidad.Core.Progression
{
    /// <summary>
    /// Cost expressed as a string ResourceId + float Amount.
    /// Uses string to avoid coupling to the Resource system.
    /// </summary>
    public readonly record struct ResourceCost(string ResourceIdValue, float Amount);

    /// <summary>
    /// Definition of a node in a progression tree.
    /// </summary>
    public sealed record ProgressionNodeDefinition(
        ProgressionNodeId Id,
        string DisplayName,
        IReadOnlyList<ProgressionNodeId> Prerequisites,
        IReadOnlyList<ResourceCost> Costs);
}
