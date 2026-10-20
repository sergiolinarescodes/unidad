using System;
using System.Collections.Generic;
using Unidad.Core.Content;

namespace Unidad.Audio
{
    /// <summary>Every sound row, merged from the "sounds" catalogue of every content folder.</summary>
    public sealed class SoundCatalog
    {
        public const string CatalogueName = "sounds";

        private readonly Dictionary<string, SoundCue> _rows = new(StringComparer.Ordinal);

        public SoundCatalog(IEnumerable<SoundCue> rows)
        {
            if (rows == null) return;
            foreach (var row in rows) _rows[row.Id] = row;
        }

        /// <summary>A new empty catalogue.</summary>
        public static SoundCatalog Empty() => new(Array.Empty<SoundCue>());

        /// <summary>The "sounds" catalogue of <paramref name="source"/>; a volume outside 0..1 is a ContentException.</summary>
        public static SoundCatalog Load(IContentSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var paths = ContentJson.CataloguePaths(source, CatalogueName);
            foreach (var path in paths)
                foreach (var row in ContentJson.Load<List<SoundRow>>(source, path))
                    if (row != null && !(row.Volume >= 0f && row.Volume <= 1f))
                        throw new ContentException(path, $"'{row.Id}' has volume {row.Volume}; a volume is 0..1.");
            var registry = ContentJson.Catalogue<SoundRow>(source, CatalogueName, row => row.Id, required: false);
            var cues = new List<SoundCue>(registry.Count);
            foreach (var row in registry.Values) cues.Add(new SoundCue(row.Id, row.Stream, row.Bus, row.Volume));
            return new SoundCatalog(cues);
        }

        public int Count => _rows.Count;

        public IReadOnlyCollection<string> Ids => _rows.Keys;

        public bool TryGet(string id, out SoundCue cue)
        {
            if (id != null) return _rows.TryGetValue(id, out cue);
            cue = default;
            return false;
        }
    }
}
