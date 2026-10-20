using System;
using System.Collections.Generic;

namespace Unidad.Core.Content
{
    /// <summary>
    /// Several sources as one: <c>layers[0]</c> is the base and later layers sit on top; each has a label ("base",
    /// "mod:&lt;id&gt;"). Read and Exists use the topmost layer that has the path; List is the union over the layers.
    /// </summary>
    public sealed class LayeredContentSource : IContentSource
    {
        private readonly (string Label, IContentSource Source)[] _layers;

        public LayeredContentSource(IReadOnlyList<(string Label, IContentSource Source)> layers)
        {
            if (layers == null) throw new ArgumentNullException(nameof(layers));
            if (layers.Count == 0) throw new ArgumentException("A layered source has at least one layer.", nameof(layers));
            _layers = new (string, IContentSource)[layers.Count];
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].Source == null) throw new ArgumentException($"Layer {i} ('{layers[i].Label}') has no source.", nameof(layers));
                _layers[i] = layers[i];
            }
        }

        /// <summary>The label of the topmost layer that has <paramref name="path"/>; null when none has it.</summary>
        public string LayerOf(string path) => Top(path)?.Label;

        public string Read(string path)
        {
            var layer = Top(path);
            if (layer == null) throw new ContentException(path ?? "(null)", "does not exist.");
            return layer.Value.Source.Read(path);
        }

        public bool Exists(string path) => Top(path) != null;

        public IReadOnlyList<string> List(string folder, string pattern)
        {
            var found = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var layer in _layers) found.UnionWith(layer.Source.List(folder, pattern));
            return new List<string>(found);
        }

        private (string Label, IContentSource Source)? Top(string path)
        {
            for (int i = _layers.Length - 1; i >= 0; i--)
                if (_layers[i].Source.Exists(path)) return _layers[i];
            return null;
        }
    }
}
