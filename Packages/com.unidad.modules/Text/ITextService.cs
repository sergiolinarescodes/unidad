namespace Unidad.Text
{
    /// <summary>Every player-facing string, by key. A missing key shows "[key]" and is logged once.</summary>
    public interface ITextService
    {
        string Get(string key);

        string Format(string key, params object[] args);

        bool Has(string key);
    }
}
