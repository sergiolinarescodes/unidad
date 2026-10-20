using System.Text;

namespace Unidad.Text
{
    /// <summary>Turns an id into display words: "chain_lightning" gives "Chain Lightning", "fireBolt" gives "Fire Bolt".</summary>
    public static class IdNames
    {
        public static string Format(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            var result = new StringBuilder(id.Length + 4);
            bool afterDelimiter = true;
            foreach (char c in id)
            {
                if (c is '_' or '.')
                {
                    result.Append(' ');
                    afterDelimiter = true;
                }
                else if (char.IsUpper(c) && result.Length > 0 && !afterDelimiter)
                {
                    result.Append(' ').Append(c);
                }
                else
                {
                    result.Append(afterDelimiter ? char.ToUpperInvariant(c) : c);
                    afterDelimiter = false;
                }
            }
            return result.ToString();
        }
    }
}
