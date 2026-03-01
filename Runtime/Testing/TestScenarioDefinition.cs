namespace Unidad.Core.Testing
{
    /// <summary>
    /// Declarative definition of a test scenario.
    /// Contains all metadata needed to display in Editor and execute in NUnit.
    /// </summary>
    public sealed record TestScenarioDefinition(
        string Id,
        string Name,
        string Description,
        ScenarioParameter[] Parameters
    );
}
