using System.Collections.Generic;
using System.Linq;

namespace Unidad.Core.Patterns.Scoring
{
    /// <summary>
    /// Default implementation of contributor registry with weighted aggregation.
    /// </summary>
    public class ContributorRegistry<TContext> : IContributorRegistry<TContext>
    {
        private readonly List<IContributor<TContext>> _contributors = new();

        public IReadOnlyList<IContributor<TContext>> Contributors => _contributors;

        public void Register(IContributor<TContext> contributor)
        {
            if (_contributors.Any(c => c.Id == contributor.Id))
                return;
            _contributors.Add(contributor);
        }

        public void Unregister(string contributorId)
        {
            _contributors.RemoveAll(c => c.Id == contributorId);
        }

        /// <summary>
        /// Evaluate all contributors and return weighted sum.
        /// </summary>
        public float EvaluateAll(TContext context)
        {
            float total = 0f;
            foreach (var contributor in _contributors)
            {
                total += contributor.Evaluate(context) * contributor.Weight;
            }
            return total;
        }

        /// <summary>
        /// Evaluate all contributors with detailed breakdown.
        /// </summary>
        public IReadOnlyList<(string ContributorId, float Score)> EvaluateDetailed(TContext context)
        {
            var results = new List<(string, float)>();
            foreach (var contributor in _contributors)
            {
                var score = contributor.Evaluate(context) * contributor.Weight;
                results.Add((contributor.Id, score));
            }
            return results;
        }
    }
}
