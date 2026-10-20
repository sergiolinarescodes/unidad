using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Unidad.Analyzers
{
    /// <summary>
    /// UD016 — the <c>Unidad</c> namespace belongs to the framework. A type declared under <c>Unidad.*</c> in a
    /// <c>Cds.*</c> assembly would read as framework code in every using and every sync, and collide with the
    /// next framework carry. Nested types are covered by their container.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NamespaceOwnershipAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(UnidadDiagnostics.UnidadNamespaceInGame);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                var assembly = start.Compilation.AssemblyName;
                if (assembly == null || !assembly.StartsWith("Cds.", System.StringComparison.Ordinal)) return;
                start.RegisterSymbolAction(c => Analyze(c, assembly), SymbolKind.NamedType);
            });
        }

        private static void Analyze(SymbolAnalysisContext context, string assembly)
        {
            var type = (INamedTypeSymbol)context.Symbol;
            if (type.ContainingType != null || type.IsImplicitlyDeclared) return;

            var root = type.ContainingNamespace;
            if (root == null || root.IsGlobalNamespace) return;
            while (!root.ContainingNamespace.IsGlobalNamespace) root = root.ContainingNamespace;
            if (root.Name != "Unidad") return;

            var ns = type.ContainingNamespace.ToDisplayString();
            foreach (var location in type.Locations)
                if (location.IsInSource)
                    context.ReportDiagnostic(Diagnostic.Create(
                        UnidadDiagnostics.UnidadNamespaceInGame, location, type.Name, ns, assembly));
        }
    }
}
