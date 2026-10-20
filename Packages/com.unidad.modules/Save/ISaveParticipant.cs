using System;
using System.Text.Json.Nodes;

namespace Unidad.Save
{
    /// <summary>One section of the save slot; added with <c>AddMany&lt;ISaveParticipant&gt;</c>.</summary>
    public interface ISaveParticipant
    {
        string Key { get; }

        JsonNode Capture();

        /// <summary>Parses <paramref name="section"/> and returns the step that applies it; throws SaveFormatException on bad data, changing nothing.</summary>
        Action Read(JsonNode section);
    }
}
