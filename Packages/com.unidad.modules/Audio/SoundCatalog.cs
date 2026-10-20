using System;
using System.Collections.Generic;
using Unidad.Core.Content;

namespace Unidad.Audio
{
    /// <summary>Every sound row, merged from the "sounds" catalogue of every content folder.</summary>
    public sealed class SoundCatalog
    {
        public const string CatalogueName = "sounds";

        public SoundCatalog(IEnumerable<SoundCue> rows) => throw new NotImplementedException();

        /// <summary>A new empty catalogue.</summary>
        public static SoundCatalog Empty() => throw new NotImplementedException();

        /// <summary>The "sounds" catalogue of <paramref name="source"/>; a volume outside 0..1 is a ContentException.</summary>
        public static SoundCatalog Load(IContentSource source) => throw new NotImplementedException();

        public int Count => throw new NotImplementedException();

        public IReadOnlyCollection<string> Ids => throw new NotImplementedException();

        public bool TryGet(string id, out SoundCue cue) => throw new NotImplementedException();
    }
}
