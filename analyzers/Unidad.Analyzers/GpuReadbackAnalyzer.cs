using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Unidad.Analyzers
{
    /// <summary>
    /// UD012 — the main thread never waits on the GPU.
    /// <para>
    /// Texture pixel reads, buffer <c>GetData</c>, a blocking <c>AsyncGPUReadback</c> wait and a screenshot
    /// into a texture all stall the frame until the render thread catches up. Keep the value you wrote.
    /// A deliberate capture carries the pragma with its reason.
    /// </para>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class GpuReadbackAnalyzer : DiagnosticAnalyzer
    {
        private static readonly ImmutableHashSet<string> Texture2DReads = ImmutableHashSet.Create(
            "ReadPixels", "GetPixel", "GetPixels", "GetPixels32", "GetPixelData", "GetRawTextureData");

        private static readonly ImmutableHashSet<string> TextureReads =
            ImmutableHashSet.Create("GetPixels", "GetPixels32", "GetPixelData");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(UnidadDiagnostics.GpuReadback);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                if (!SymbolFacts.IsAnalyzed(start.Compilation)) return;
                start.RegisterOperationAction(Analyze, OperationKind.Invocation);
            });
        }

        private static void Analyze(OperationAnalysisContext context)
        {
            var method = ((IInvocationOperation)context.Operation).TargetMethod;
            var type = method.ContainingType;
            var name = method.Name;
            var banned =
                SymbolFacts.IsType(type, "UnityEngine", "Texture2D") && Texture2DReads.Contains(name) ||
                (SymbolFacts.IsType(type, "UnityEngine", "Texture3D") || SymbolFacts.IsType(type, "UnityEngine", "Texture2DArray") ||
                 SymbolFacts.IsType(type, "UnityEngine", "Cubemap")) && TextureReads.Contains(name) ||
                (SymbolFacts.IsType(type, "UnityEngine", "ComputeBuffer") || SymbolFacts.IsType(type, "UnityEngine", "GraphicsBuffer")) &&
                    name == "GetData" ||
                SymbolFacts.IsType(type, "UnityEngine.Rendering", "AsyncGPUReadback") && name == "WaitAllRequests" ||
                SymbolFacts.IsType(type, "UnityEngine.Rendering", "AsyncGPUReadbackRequest") && name == "WaitForCompletion" ||
                SymbolFacts.IsType(type, "UnityEngine", "ScreenCapture") && name == "CaptureScreenshotAsTexture";
            if (!banned) return;
            context.ReportDiagnostic(Diagnostic.Create(UnidadDiagnostics.GpuReadback,
                context.Operation.Syntax.GetLocation(), SymbolFacts.EnclosingTypeName(context.Operation.Syntax)));
        }
    }
}
