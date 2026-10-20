using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Unidad.Save
{
    /// <summary>Strict readers for participants; each throws <see cref="SaveFormatException"/> naming the field.</summary>
    public static class SaveRead
    {
        public static JsonObject Object(JsonNode node, string what) => throw new NotImplementedException();
        public static JsonObject Object(JsonObject section, string field) => throw new NotImplementedException();
        public static JsonArray Array(JsonObject section, string field) => throw new NotImplementedException();
        public static float Number(JsonObject section, string field) => throw new NotImplementedException();
        public static double Double(JsonObject section, string field) => throw new NotImplementedException();
        public static double[] Doubles(JsonObject section, string field, int count) => throw new NotImplementedException();
        public static int Int(JsonObject section, string field) => throw new NotImplementedException();
        public static long Long(JsonObject section, string field) => throw new NotImplementedException();
        public static int? OptionalInt(JsonObject section, string field) => throw new NotImplementedException();
        public static ulong Seed(JsonObject section, string field) => throw new NotImplementedException();
        public static bool Bool(JsonObject section, string field) => throw new NotImplementedException();
        public static string String(JsonObject section, string field) => throw new NotImplementedException();
        public static IReadOnlyList<string> Strings(JsonObject section, string field) => throw new NotImplementedException();

        /// <summary>An enum by NAME; an ordinal is refused.</summary>
        public static TEnum Enum<TEnum>(JsonObject section, string field) where TEnum : struct, System.Enum => throw new NotImplementedException();
    }
}
