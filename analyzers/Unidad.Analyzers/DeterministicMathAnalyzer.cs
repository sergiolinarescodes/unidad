using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Unidad.Analyzers
{
    /// <summary>
    /// UD009 — an assembly that declares <c>[assembly: DeterministicSimulation]</c> computes the same
    /// bits on every machine.
    /// <para>
    /// <c>MathF.Sin</c> and friends call the platform's C library, which rounds differently on
    /// Windows and Linux; Unity's <c>Mathf</c> and Burst's <c>math</c> transcendentals differ by Burst
    /// float mode; <c>Vector2.Lerp</c>/<c>Normalize</c> were rewritten in .NET 9. Any of them is enough
    /// for two machines stepping the same simulation to diverge. The opt-in attribute matches by simple
    /// name in any namespace, so a game assembly may declare its own copy (U-151). <c>Sqrt</c> and
    /// <c>math.lerp</c> stay allowed.
    /// </para>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DeterministicMathAnalyzer : DiagnosticAnalyzer
    {
        private const string OptInAttribute = "DeterministicSimulation";

        private static readonly ImmutableHashSet<string> Transcendental = ImmutableHashSet.Create(
            "Sin", "Cos", "Tan", "Asin", "Acos", "Atan", "Atan2", "Sinh", "Cosh", "Tanh", "SinCos",
            "Exp", "Log", "Log2", "Log10", "Pow", "Cbrt");

        private static readonly ImmutableHashSet<string> MathfTranscendental = ImmutableHashSet.Create(
            "Sin", "Cos", "Tan", "Asin", "Acos", "Atan", "Atan2", "Exp", "Log", "Log10", "Pow");

        private static readonly ImmutableHashSet<string> BurstMathTranscendental = ImmutableHashSet.Create(
            "sin", "cos", "tan", "asin", "acos", "atan", "atan2", "sinh", "cosh", "tanh", "sincos",
            "exp", "exp2", "exp10", "log", "log2", "log10", "pow");

        private static readonly ImmutableHashSet<string> RewrittenVectorHelpers =
            ImmutableHashSet.Create("Lerp", "Normalize");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(UnidadDiagnostics.DeterministicMath);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                if (!SymbolFacts.IsAnalyzed(start.Compilation, start.Options)) return;
                if (!SymbolFacts.HasAssemblyAttribute(start.Compilation, OptInAttribute)) return;
                start.RegisterOperationAction(Analyze, OperationKind.Invocation);
            });
        }

        private static void Analyze(OperationAnalysisContext context)
        {
            var method = ((IInvocationOperation)context.Operation).TargetMethod;
            var type = method.ContainingType;
            var name = method.Name;
            var banned =
                (SymbolFacts.IsType(type, "System", "Math") || SymbolFacts.IsType(type, "System", "MathF")) &&
                    Transcendental.Contains(name) ||
                SymbolFacts.IsType(type, "UnityEngine", "Mathf") && MathfTranscendental.Contains(name) ||
                SymbolFacts.IsType(type, "Unity.Mathematics", "math") && BurstMathTranscendental.Contains(name) ||
                (SymbolFacts.IsType(type, "System.Numerics", "Vector2") || SymbolFacts.IsType(type, "System.Numerics", "Vector3") ||
                 SymbolFacts.IsType(type, "System.Numerics", "Vector4")) && RewrittenVectorHelpers.Contains(name);
            if (!banned) return;
            context.ReportDiagnostic(Diagnostic.Create(UnidadDiagnostics.DeterministicMath,
                context.Operation.Syntax.GetLocation(), SymbolFacts.Describe(method)));
        }
    }
}
