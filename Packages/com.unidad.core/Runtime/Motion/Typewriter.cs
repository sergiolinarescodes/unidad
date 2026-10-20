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
        // Float sums of 1/cps drift below the exact budget; this keeps "0.3 s at 10 chars/s" at 3 characters.
        private const float Epsilon = 1e-5f;

        private readonly float[] _cost;      // seconds glyph i costs
        private readonly int[] _rawLength;   // raw prefix to show when v glyphs are visible (markup after them included)
        private readonly int[] _marks;
        private readonly List<int> _fired = new();
        private int _nextMark;
        private float _budget;

        public Typewriter(string text, TypewriterSpec spec, IReadOnlyList<int> marks = null)
        {
            if (!(spec.CharsPerSecond > 0f)) throw new ArgumentOutOfRangeException(nameof(spec), spec.CharsPerSecond, "A typewriter types > 0 chars/s.");
            Text = text ?? throw new ArgumentNullException(nameof(text));

            var glyphChars = new List<char>();
            var glyphEnds = new List<int>();
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '<')
                {
                    int close = text.IndexOf('>', i + 1);
                    if (close >= 0) { i = close; continue; }
                }
                glyphChars.Add(text[i]);
                glyphEnds.Add(i + 1);
            }

            int count = glyphChars.Count;
            float step = 1f / spec.CharsPerSecond;
            _cost = new float[count];
            for (int g = 0; g < count; g++)
            {
                _cost[g] = step;
                if (g == 0 || !char.IsWhiteSpace(glyphChars[g])) continue;
                char before = glyphChars[g - 1];
                if (before == '.' || before == '!' || before == '?' || before == '…') _cost[g] += spec.SentencePause;
                else if (before == ',') _cost[g] += spec.CommaPause;
            }

            _rawLength = new int[count + 1];
            for (int v = 0; v <= count; v++)
            {
                int end = v == 0 ? 0 : glyphEnds[v - 1];
                while (end < text.Length && text[end] == '<')
                {
                    int close = text.IndexOf('>', end + 1);
                    if (close < 0) break;
                    end = close + 1;
                }
                _rawLength[v] = end;
            }

            _marks = marks == null ? Array.Empty<int>() : new List<int>(marks).ToArray();
            Array.Sort(_marks);
        }

        public string Text { get; }

        /// <summary>Glyphs revealed, markup excluded.</summary>
        public int VisibleChars { get; private set; }

        /// <summary>The prefix length of <see cref="Text"/> to show, markup included whole.</summary>
        public int VisibleRawLength => _rawLength[VisibleChars];

        public bool IsDone => VisibleChars == _cost.Length;

        /// <summary>The marks this call fired, ascending; the list is reused by the next call.</summary>
        public IReadOnlyList<int> Advance(float deltaTime)
        {
            if (!(deltaTime >= 0f)) throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "A step is >= 0.");
            _budget += deltaTime;
            while (VisibleChars < _cost.Length && _budget + Epsilon >= _cost[VisibleChars])
                _budget -= _cost[VisibleChars++];
            if (IsDone) _budget = 0f;
            return FireMarks(VisibleChars);
        }

        /// <summary>Reveals everything and fires the remaining marks.</summary>
        public IReadOnlyList<int> Complete()
        {
            VisibleChars = _cost.Length;
            _budget = 0f;
            return FireMarks(int.MaxValue);
        }

        private IReadOnlyList<int> FireMarks(int upTo)
        {
            _fired.Clear();
            while (_nextMark < _marks.Length && _marks[_nextMark] <= upTo) _fired.Add(_marks[_nextMark++]);
            return _fired;
        }
    }
}
