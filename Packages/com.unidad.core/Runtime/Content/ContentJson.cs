using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Unidad.Core.Registry;

namespace Unidad.Core.Content
{
    /// <summary>
    /// Strict content JSON: enums by NAME (an ordinal is refused), an unknown member is refused, <c>required</c> members must be
    /// present, comments and trailing commas are allowed, member names match case-insensitively. Every failure is a
    /// <see cref="ContentException"/> naming the file.
    /// </summary>
    public static class ContentJson
    {
        // Internal so System.Text.Json never reaches the public surface outside Unidad.Save (if C1 fails, only Save moves).
        internal static readonly JsonSerializerOptions Options = new()
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) },
        };

        /// <summary>The file at <paramref name="path"/> as one <typeparamref name="T"/>.</summary>
        public static T Load<T>(IContentSource source, string path)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            string text;
            try
            {
                text = source.Read(path);
            }
            catch (ContentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ContentException(path, $"cannot be read ({ex.Message}).", ex);
            }
            return Parse<T>(text, path);
        }

        /// <summary><paramref name="json"/> as one <typeparamref name="T"/>; <paramref name="path"/> names it in errors.</summary>
        public static T Parse<T>(string json, string path)
        {
            T value;
            try
            {
                value = JsonSerializer.Deserialize<T>(json, Options);
            }
            catch (JsonException ex)
            {
                throw new ContentException(path, ex.Message, ex);
            }
            catch (NotSupportedException ex)
            {
                throw new ContentException(path, ex.Message, ex);
            }
            if (value == null) throw new ContentException(path, "is empty (null).");
            return value;
        }

        /// <summary>The file at <paramref name="path"/> (a JSON array) as a registry keyed by <paramref name="id"/>, in file order.</summary>
        public static OrderedRegistry<T> Registry<T>(IContentSource source, string path, Func<T, string> id) => Merge(source, id, path);

        /// <summary>
        /// Every file in <paramref name="paths"/> (each a JSON array) in one registry, in file then entry order: a catalogue and the
        /// files other modules contribute to it. An empty id, or an id two entries share (in one file or across two), is refused.
        /// </summary>
        public static OrderedRegistry<T> Merge<T>(IContentSource source, Func<T, string> id, params string[] paths)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            var registry = new OrderedRegistry<T>();
            var origin = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                var entries = Load<List<T>>(source, path);
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry == null) throw new ContentException(path, $"entry {i} is null.");
                    string key = id(entry);
                    if (string.IsNullOrWhiteSpace(key)) throw new ContentException(path, $"entry {i} has an empty id.");
                    if (origin.TryGetValue(key, out var first))
                        throw new ContentException(path, first == path ? $"the id '{key}' appears twice." : $"the id '{key}' is already defined in {first}.");
                    origin.Add(key, path);
                    registry.Add(key, entry);
                }
            }
            return registry;
        }

        /// <summary>Every "&lt;name&gt;.json" and "&lt;name&gt;.*.json" in every first-level folder, distinct, sorted ordinal.</summary>
        public static IReadOnlyList<string> CataloguePaths(IContentSource source, string name)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A catalogue has a name.", nameof(name));
            return source.List("*", name + ".json").Concat(source.List("*", name + ".*.json"))
                         .Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// The catalogue <paramref name="name"/>: <see cref="Merge{T}"/> over <see cref="CataloguePaths"/>. With
        /// <paramref name="required"/> and no file, throws a <see cref="ContentException"/> naming the patterns.
        /// </summary>
        public static OrderedRegistry<T> Catalogue<T>(IContentSource source, string name, Func<T, string> id, bool required = true)
        {
            var paths = CataloguePaths(source, name);
            if (paths.Count == 0 && required)
                throw new ContentException(name, $"no content file matches '{name}.json' or '{name}.*.json'.");
            return Merge(source, id, paths.ToArray());
        }
    }
}
