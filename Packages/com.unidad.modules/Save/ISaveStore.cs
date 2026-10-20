namespace Unidad.Save
{
    /// <summary>Where the slot's text lives.</summary>
    public interface ISaveStore
    {
        string Location { get; }

        /// <summary>The slot's text; null when there is no slot; IOException when it cannot be read.</summary>
        string Read();

        /// <summary>IOException when it cannot be written.</summary>
        void Write(string text);
    }
}
