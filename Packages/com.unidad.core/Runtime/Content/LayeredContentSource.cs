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
        public LayeredContentSource(IReadOnlyList<(string Label, IContentSource Source)> layers) => throw new NotImplementedException();

        /// <summary>The label of the topmost layer that has <paramref name="path"/>; null when none has it.</summary>
        public string LayerOf(string path) => throw new NotImplementedException();

        public string Read(string path) => throw new NotImplementedException();

        public bool Exists(string path) => throw new NotImplementedException();

        public IReadOnlyList<string> List(string folder, string pattern) => throw new NotImplementedException();
    }
}
