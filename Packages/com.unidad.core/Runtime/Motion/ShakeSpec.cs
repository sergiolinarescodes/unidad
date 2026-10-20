using Unidad.Core.Random;

namespace Unidad.Core.Motion
{
    /// <summary>A deterministic shake along one axis: Vibrato alternating points over Duration, from <see cref="Seed"/>.</summary>
    public readonly record struct ShakeSpec(float Duration, float Strength, int Vibrato, bool FadeOut, int Seed)
    {
        /// <summary>The signed offset at <paramref name="t"/>; 0 at t &lt;= 0 and t &gt;= Duration.</summary>
        public float Sample(float t)
        {
            if (!(t > 0f) || t >= Duration || Vibrato <= 0) return 0f;
            float segment = t / Duration * (Vibrato + 1);
            int k = (int)segment;
            // shortcut: replays the seed's sequence up to point k+1 on every sample (one small allocation); cache per shake if a profile shows it.
            var random = new DeterministicRandom(Seed);
            float left = 0f, right = 0f;
            for (int i = 1; i <= k + 1 && i <= Vibrato; i++)
            {
                float r = random.NextFloat();
                if (i == k) left = Point(i, r);
                if (i == k + 1) right = Point(i, r);
            }
            return Easing.Lerp(left, right, segment - k);
        }

        private float Point(int k, float r)
        {
            float sign = (k & 1) == 1 ? 1f : -1f;
            float fade = FadeOut ? 1f - (float)(k - 1) / Vibrato : 1f;
            return sign * Strength * fade * (0.5f + 0.5f * r);
        }
    }
}
