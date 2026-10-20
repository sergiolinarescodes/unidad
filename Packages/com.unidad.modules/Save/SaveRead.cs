using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Unidad.Save
{
    /// <summary>Strict readers for participants; each throws <see cref="SaveFormatException"/> naming the field.</summary>
    public static class SaveRead
    {
        public static JsonObject Object(JsonNode node, string what) =>
            node as JsonObject ?? throw new SaveFormatException($"{what} is not an object.");

        public static JsonObject Object(JsonObject section, string field) =>
            section[field] as JsonObject ?? throw new SaveFormatException($"{field} is not an object.");

        public static JsonArray Array(JsonObject section, string field) =>
            section[field] as JsonArray ?? throw new SaveFormatException($"{field} is not an array.");

        /// <summary>A finite number.</summary>
        public static float Number(JsonObject section, string field)
        {
            if (IsNumber(section[field], out var value) && value.TryGetValue(out float number) && float.IsFinite(number)) return number;
            throw new SaveFormatException($"{field} is not a finite number.");
        }

        /// <summary>A finite double, read back exactly as written.</summary>
        public static double Double(JsonObject section, string field)
        {
            if (IsNumber(section[field], out var value) && value.TryGetValue(out double number) && double.IsFinite(number)) return number;
            throw new SaveFormatException($"{field} is not a finite number.");
        }

        /// <summary>An array of exactly <paramref name="count"/> finite doubles.</summary>
        public static double[] Doubles(JsonObject section, string field, int count)
        {
            if (section[field] is not JsonArray array || array.Count != count) throw new SaveFormatException($"{field} is not an array of {count} numbers.");
            var numbers = new double[count];
            for (int i = 0; i < count; i++)
            {
                if (!IsNumber(array[i], out var value) || !value.TryGetValue(out double number) || !double.IsFinite(number))
                    throw new SaveFormatException($"{field}[{i}] is not a finite number.");
                numbers[i] = number;
            }
            return numbers;
        }

        public static int Int(JsonObject section, string field)
        {
            if (IsNumber(section[field], out var value) && value.TryGetValue(out int number)) return number;
            throw new SaveFormatException($"{field} is not a whole number.");
        }

        public static long Long(JsonObject section, string field)
        {
            if (IsNumber(section[field], out var value) && value.TryGetValue(out long number)) return number;
            throw new SaveFormatException($"{field} is not a whole number.");
        }

        /// <summary>A whole number, or null when the field holds null.</summary>
        public static int? OptionalInt(JsonObject section, string field)
        {
            if (!section.TryGetPropertyValue(field, out var node)) throw new SaveFormatException($"{field} is missing.");
            if (node == null) return null;
            if (IsNumber(node, out var value) && value.TryGetValue(out int number)) return number;
            throw new SaveFormatException($"{field} is not a whole number or null.");
        }

        /// <summary>A whole number that fits an unsigned 64-bit seed.</summary>
        public static ulong Seed(JsonObject section, string field)
        {
            if (IsNumber(section[field], out var value) && value.TryGetValue(out ulong number)) return number;
            throw new SaveFormatException($"{field} is not a seed (a non-negative whole number).");
        }

        public static bool Bool(JsonObject section, string field)
        {
            if (section[field] is JsonValue value && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False) return value.GetValue<bool>();
            throw new SaveFormatException($"{field} is not true or false.");
        }

        public static string String(JsonObject section, string field)
        {
            if (section[field] is JsonValue value && value.GetValueKind() == JsonValueKind.String && value.TryGetValue(out string text)) return text;
            throw new SaveFormatException($"{field} is not a string.");
        }

        public static IReadOnlyList<string> Strings(JsonObject section, string field)
        {
            if (section[field] is not JsonArray array) throw new SaveFormatException($"{field} is not an array.");
            var strings = new List<string>(array.Count);
            for (int i = 0; i < array.Count; i++)
            {
                if (array[i] is not JsonValue value || value.GetValueKind() != JsonValueKind.String || !value.TryGetValue(out string text))
                    throw new SaveFormatException($"{field}[{i}] is not a string.");
                strings.Add(text);
            }
            return strings;
        }

        /// <summary>An enum by NAME; an ordinal is refused.</summary>
        public static TEnum Enum<TEnum>(JsonObject section, string field) where TEnum : struct, System.Enum
        {
            var text = section[field] is JsonValue value && value.GetValueKind() == JsonValueKind.String && value.TryGetValue(out string s) ? s : null;
            if (text != null && !IsNumeric(text) && System.Enum.TryParse(text, false, out TEnum parsed)
                && System.Enum.IsDefined(typeof(TEnum), parsed))
                return parsed;
            throw new SaveFormatException($"{field} is not a {typeof(TEnum).Name} name.");
        }

        private static bool IsNumber(JsonNode node, out JsonValue value)
        {
            value = node as JsonValue;
            return value != null && value.GetValueKind() == JsonValueKind.Number;
        }

        private static bool IsNumeric(string text)
        {
            var trimmed = text.TrimStart();
            return trimmed.Length == 0 || trimmed[0] is '-' or '+' || char.IsDigit(trimmed[0]);
        }
    }
}
