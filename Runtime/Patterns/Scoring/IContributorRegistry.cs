using System.Collections.Generic;

namespace Unidad.Core.Patterns.Scoring
{
    /// <summary>
    /// Registry for scoring contributors.
    /// Manages a collection of contributors and provides aggregate scoring.
    /// </summary>
    public interface IContributorRegistry<TContext>
    {
        void Register(IContributor<TContext> contributor);
        void Unregister(string contributorId);
        IReadOnlyList<IContributor<TContext>> Contributors { get; }
        float EvaluateAll(TContext context);
        IReadOnlyList<(string ContributorId, float Score)> EvaluateDetailed(TContext context);
    }
}
