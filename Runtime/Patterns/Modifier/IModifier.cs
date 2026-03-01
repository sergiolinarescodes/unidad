namespace Unidad.Core.Patterns.Modifier
{
    /// <summary>
    /// Generic modifier pattern for extensible behavior modification.
    /// Modifiers can intercept and alter values, actions, or state.
    /// </summary>
    /// <typeparam name="TValue">The type of value being modified.</typeparam>
    public interface IModifier<TValue>
    {
        /// <summary>Unique identifier for this modifier.</summary>
        string Id { get; }

        /// <summary>Priority for ordering. Higher priority executes first.</summary>
        int Priority { get; }

        /// <summary>Apply this modifier to the given value.</summary>
        TValue Apply(TValue value);

        /// <summary>Whether this modifier is currently active.</summary>
        bool IsActive { get; }
    }

    /// <summary>
    /// Modifier with context for conditional application.
    /// </summary>
    public interface IModifier<TValue, in TContext>
    {
        string Id { get; }
        int Priority { get; }
        TValue Apply(TValue value, TContext context);
        bool IsActive { get; }
    }
}
