using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Unidad.Analyzers
{
    /// <summary>
    /// UD002 — a MonoBehaviour's <c>Update</c>/<c>LateUpdate</c>/<c>FixedUpdate</c>/<c>OnGUI</c> must be declared with
    /// <c>[ProcessOverride("reason")]</c> on the method or its type.
    /// <para>
    /// Per-frame work belongs on a TickBinding, which a test can step deterministically. Unity calls
    /// these magic methods by name, so no <c>override</c> is needed to opt into a frame hook; the
    /// attribute makes the set of self-animating components explicit and greppable.
    /// </para>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class FrameHookAnalyzer : DiagnosticAnalyzer
    {
        private static readonly ImmutableHashSet<string> FrameHooks =
            ImmutableHashSet.Create("Update", "LateUpdate", "FixedUpdate", "OnGUI");

        private const string MarkerAttribute = "ProcessOverride";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(UnidadDiagnostics.UndeclaredFrameHook);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                if (!SymbolFacts.IsAnalyzed(start.Compilation, start.Options)) return;
                start.RegisterSymbolAction(Analyze, SymbolKind.Method);
            });
        }

        private static void Analyze(SymbolAnalysisContext context)
        {
            var method = (IMethodSymbol)context.Symbol;
            if (method.MethodKind != MethodKind.Ordinary || method.IsStatic || method.Parameters.Length != 0) return;
            if (!FrameHooks.Contains(method.Name)) return;

            var type = method.ContainingType;
            if (!SymbolFacts.DerivesFrom(type, "UnityEngine", "MonoBehaviour")) return;
            if (SymbolFacts.HasAttribute(method, MarkerAttribute) || SymbolFacts.HasAttribute(type, MarkerAttribute)) return;

            foreach (var location in method.Locations)
                if (location.IsInSource)
                    context.ReportDiagnostic(Diagnostic.Create(
                        UnidadDiagnostics.UndeclaredFrameHook, location, type.Name, method.Name));
        }
    }
}
