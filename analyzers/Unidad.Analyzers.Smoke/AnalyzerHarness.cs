using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Unidad.Analyzers.Smoke
{
    /// <summary>Compiles inline sources against the host's runtime and the Unity stubs, runs every Unidad analyzer.</summary>
    internal static class AnalyzerHarness
    {
        private static readonly CSharpParseOptions Parse = new(LanguageVersion.CSharp14);

        private static readonly ImmutableArray<MetadataReference> Platform =
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToImmutableArray();

        private static readonly Lazy<MetadataReference> Unity = new(() =>
        {
            var stubs = CSharpCompilation.Create("UnityEngine",
                new[] { CSharpSyntaxTree.ParseText(UnityStubs.Source, Parse) }, Platform,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var ms = new MemoryStream();
            var emit = stubs.Emit(ms);
            if (!emit.Success) throw new InvalidOperationException(string.Join("\n", emit.Diagnostics));
            return MetadataReference.CreateFromImage(ms.ToArray());
        });

        private static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers =
            typeof(ServiceBoundaryAnalyzer).Assembly.GetTypes()
                .Where(t => typeof(DiagnosticAnalyzer).IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => (DiagnosticAnalyzer)Activator.CreateInstance(t)).ToImmutableArray();

        /// <summary>Every UD diagnostic, ordered by position. Throws if the inputs do not compile.</summary>
        public static ImmutableArray<Diagnostic> Diagnostics(string assemblyName, params string[] sources) =>
            DiagnosticsWithPrefix(assemblyName, null, sources);

        /// <summary>As above, with <paramref name="gamePrefix"/> as <c>build_property.UnidadGamePrefix</c> (null: unset).</summary>
        public static ImmutableArray<Diagnostic> DiagnosticsWithPrefix(string assemblyName, string gamePrefix, params string[] sources)
        {
            var compilation = CSharpCompilation.Create(assemblyName,
                sources.Select(s => CSharpSyntaxTree.ParseText(s, Parse)),
                Platform.Add(Unity.Value),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));

            var globals = gamePrefix == null
                ? ImmutableDictionary<string, string>.Empty
                : ImmutableDictionary<string, string>.Empty.Add("build_property.UnidadGamePrefix", gamePrefix);
            var options = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, new GlobalOptionsProvider(globals));
            return compilation.WithAnalyzers(Analyzers, options).GetAnalyzerDiagnosticsAsync().Result
                .Where(d => d.Id.StartsWith("UD"))
                .OrderBy(d => d.Location.GetLineSpan().StartLinePosition.Line).ToImmutableArray();
        }

        /// <summary>The UD ids, sorted.</summary>
        public static string[] Run(string assemblyName, params string[] sources) => RunWithPrefix(assemblyName, null, sources);

        /// <summary>The UD ids, sorted, with the game prefix set as the consumer's global analyzer config would.</summary>
        public static string[] RunWithPrefix(string assemblyName, string gamePrefix, params string[] sources) =>
            DiagnosticsWithPrefix(assemblyName, gamePrefix, sources).Select(d => d.Id).OrderBy(i => i, StringComparer.Ordinal).ToArray();

        private sealed class GlobalOptionsProvider : AnalyzerConfigOptionsProvider
        {
            private readonly Options _global;
            public GlobalOptionsProvider(ImmutableDictionary<string, string> values) => _global = new Options(values);
            public override AnalyzerConfigOptions GlobalOptions => _global;
            public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Options.Empty;
            public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Options.Empty;
        }

        private sealed class Options : AnalyzerConfigOptions
        {
            public static readonly Options Empty = new(ImmutableDictionary<string, string>.Empty);
            private readonly ImmutableDictionary<string, string> _values;
            public Options(ImmutableDictionary<string, string> values) => _values = values;
            public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value);
        }
    }
}
