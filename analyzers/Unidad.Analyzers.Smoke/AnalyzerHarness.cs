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
        public static ImmutableArray<Diagnostic> Diagnostics(string assemblyName, params string[] sources)
        {
            var compilation = CSharpCompilation.Create(assemblyName,
                sources.Select(s => CSharpSyntaxTree.ParseText(s, Parse)),
                Platform.Add(Unity.Value),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));

            return compilation.WithAnalyzers(Analyzers).GetAnalyzerDiagnosticsAsync().Result
                .Where(d => d.Id.StartsWith("UD"))
                .OrderBy(d => d.Location.GetLineSpan().StartLinePosition.Line).ToImmutableArray();
        }

        /// <summary>The UD ids, sorted.</summary>
        public static string[] Run(string assemblyName, params string[] sources) =>
            Diagnostics(assemblyName, sources).Select(d => d.Id).OrderBy(i => i, StringComparer.Ordinal).ToArray();
    }
}
