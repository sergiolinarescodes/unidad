using System;
using System.Collections.Generic;

namespace Unidad.Core.Patterns.Scoring
{
    /// <summary>
    /// Default implementation of contributor registry with weighted aggregation.
    /// </summary>
    public class ContributorRegistry<TContext> : IContributorRegistry<TContext>
    {
        public IReadOnlyList<IContributor<TContext>> Contributors => throw new NotImplementedException();

        public void Register(IContributor<TContext> contributor) => throw new NotImplementedException();

        public void Unregister(string contributorId) => throw new NotImplementedException();

        public float EvaluateAll(TContext context) => throw new NotImplementedException();

        public IReadOnlyList<(string ContributorId, float Score)> EvaluateDetailed(TContext context) => throw new NotImplementedException();
    }
}
