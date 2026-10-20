using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unidad.Adapter.Stage
{
    /// <summary>The bound <see cref="IAnchorRegistry"/>: named transforms in registration order.</summary>
    internal sealed class AnchorRegistry : IAnchorRegistry
    {
        private readonly Dictionary<string, Transform> _anchors = new(StringComparer.Ordinal);
        private readonly List<string> _ids = new();

        public IReadOnlyList<string> Ids => _ids;

        public void Register(string id, Transform anchor)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("An anchor id is required.", nameof(id));
            if (!anchor) throw new ArgumentNullException(nameof(anchor));
            if (_anchors.ContainsKey(id)) throw new InvalidOperationException($"Anchor '{id}' is already registered.");
            _anchors.Add(id, anchor);
            _ids.Add(id);
        }

        public Transform Get(string id) =>
            _anchors.TryGetValue(id, out var anchor) ? anchor : throw new KeyNotFoundException($"No anchor '{id}'. Known: {string.Join(", ", _ids)}.");

        public bool TryGet(string id, out Transform anchor) => _anchors.TryGetValue(id, out anchor);
    }
}
