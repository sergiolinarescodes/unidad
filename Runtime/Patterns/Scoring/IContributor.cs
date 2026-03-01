namespace Unidad.Core.Patterns.Scoring
{
    /// <summary>
    /// Generic scoring contributor pattern.
    /// Used for AI decision-making, priority systems, or any scoring-based selection.
    /// Each contributor evaluates a context and returns a score.
    /// </summary>
    /// <typeparam name="TContext">The context to evaluate (e.g., potential target, action).</typeparam>
    public interface IContributor<in TContext>
    {
        /// <summary>Unique identifier for this contributor.</summary>
        string Id { get; }

        /// <summary>
        /// Evaluate the context and return a score.
        /// Higher scores indicate higher preference.
        /// Return 0 for neutral, negative for avoidance.
        /// </summary>
        float Evaluate(TContext context);

        /// <summary>Weight multiplier for this contributor's score.</summary>
        float Weight { get; }
    }
}
