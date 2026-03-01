using System;

namespace Unidad.Core.Testing
{
    /// <summary>
    /// A parameter that is editable from the Editor Window.
    /// Supports type, range, and default value for dynamic UI generation.
    /// </summary>
    public sealed record ScenarioParameter(
        string Name,
        string Label,
        Type ValueType,
        object DefaultValue,
        object MinValue = null,
        object MaxValue = null
    );
}
