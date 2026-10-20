using System;
using System.Collections.Generic;
using Unidad.Adapter.Models;
using Unidad.Core.Registry;

namespace Unidad.Adapter
{
    /// <summary>Every <see cref="IModelSource"/> merged in resolve order; an id defined twice fails the boot.</summary>
    internal sealed class ModelCatalog : IModelCatalog
    {
        private readonly OrderedRegistry<ModelEntry> _models = new();
        private readonly Dictionary<string, string> _sourceById = new(StringComparer.Ordinal);

        public ModelCatalog(IReadOnlyList<IModelSource> sources)
        {
            foreach (var source in sources)
            {
                foreach (var entry in source.Models)
                {
                    if (string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.AssetKey))
                        throw new InvalidOperationException($"{source.Source}: a model has an empty id or asset key.");
                    if (!(entry.Scale > 0f))
                        throw new InvalidOperationException($"{source.Source}: model '{entry.Id}' has scale {entry.Scale}; it must be positive.");
                    if (_sourceById.TryGetValue(entry.Id, out var first))
                        throw new InvalidOperationException($"Model '{entry.Id}' is defined by {first} and {source.Source}.");
                    _sourceById.Add(entry.Id, source.Source);
                    _models.Add(entry.Id, entry);
                }
            }
        }

        public IReadOnlyList<string> Ids => _models.Ids;

        public bool Has(string id) => _models.Contains(id);

        public ModelEntry Get(string id) =>
            _models.TryGet(id, out var entry)
                ? entry
                : throw new KeyNotFoundException($"No model '{id}'. Known: {string.Join(", ", _models.Ids)}.");
    }
}
