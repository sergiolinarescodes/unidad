using System;
using System.Collections.Generic;

namespace Unidad.Core.Motion
{
    /// <summary>How fast a line types: characters per second, and extra pauses after a sentence end and after a comma.</summary>
    public readonly record struct TypewriterSpec(float CharsPerSecond, float SentencePause, float CommaPause = 0f);

    /// <summary>
    /// Reveals a line character by character. Markup (a '&lt;' up to the next '&gt;') costs nothing and is revealed whole. A mark
    /// at visible index i fires when that many characters show; marks at 0 fire on the first <see cref="Advance"/>.
    /// </summary>
    public sealed class Typewriter
    {
        public Typewriter(string text, TypewriterSpec spec, IReadOnlyList<int> marks = null) => throw new NotImplementedException();

        public string Text => throw new NotImplementedException();

        /// <summary>Glyphs revealed, markup excluded.</summary>
        public int VisibleChars => throw new NotImplementedException();

        /// <summary>The prefix length of <see cref="Text"/> to show, markup included whole.</summary>
        public int VisibleRawLength => throw new NotImplementedException();

        public bool IsDone => throw new NotImplementedException();

        /// <summary>The marks this call fired, ascending; the list is reused by the next call.</summary>
        public IReadOnlyList<int> Advance(float deltaTime) => throw new NotImplementedException();

        /// <summary>Reveals everything and fires the remaining marks.</summary>
        public IReadOnlyList<int> Complete() => throw new NotImplementedException();
    }
}
