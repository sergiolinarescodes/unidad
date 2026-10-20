using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Unidad.Analyzers
{
    /// <summary>
    /// UD010 / UD011 — objects come from the factory, scenes from the scene loader, assets from AssetLibrary.
    /// <para>
    /// One instantiation seam (<c>GameObjectFactory</c>/<c>ComponentPool</c>), one scene seam
    /// (<c>SceneLoader</c>) and one asset shelf (<c>Unidad.Adapter.AssetLibrary</c>) keep pooling, preparation
    /// before play and first-use disk loads in one place each. <c>AssetDatabase</c> belongs to editor
    /// assemblies. Every <c>*.Editor</c> assembly is exempt from both rules (U-152).
    /// </para>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class FactoryBoundaryAnalyzer : DiagnosticAnalyzer
    {
        private const string AssetLibrary = "Unidad.Adapter.AssetLibrary";

        private static readonly ImmutableHashSet<string> ResourcesLoads =
            ImmutableHashSet.Create("Load", "LoadAll", "LoadAsync");

        private static readonly ImmutableHashSet<string> BundleLoads = ImmutableHashSet.Create(
            "LoadAsset", "LoadAssetAsync", "LoadAllAssets", "LoadAllAssetsAsync", "LoadFromFile",
            "LoadFromFileAsync", "LoadFromMemory", "LoadFromMemoryAsync");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                UnidadDiagnostics.InstantiateOutsideFactory,
                UnidadDiagnostics.AssetLoadOutsideLibrary,
                UnidadDiagnostics.AssetDatabaseOutsideEditor);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                if (!SymbolFacts.IsAnalyzed(start.Compilation) || SymbolFacts.IsEditorAssembly(start.Compilation)) return;
                start.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
                start.RegisterSyntaxNodeAction(AnalyzeAssetDatabase, SyntaxKind.SimpleMemberAccessExpression);
            });
        }

        private static void AnalyzeInvocation(OperationAnalysisContext context)
        {
            var method = ((IInvocationOperation)context.Operation).TargetMethod;
            var type = method.ContainingType;
            var name = method.Name;
            var syntax = context.Operation.Syntax;

            DiagnosticDescriptor rule = null;
            if (SymbolFacts.IsType(type, "UnityEngine", "Object") && name == "Instantiate")
            {
                if (!SymbolFacts.IsInsideTypeNamed(syntax, n => n == "GameObjectFactory" || n == "ComponentPool"))
                    rule = UnidadDiagnostics.InstantiateOutsideFactory;
            }
            else if (SymbolFacts.IsType(type, "UnityEngine.SceneManagement", "SceneManager") &&
                     (name == "LoadScene" || name == "LoadSceneAsync"))
            {
                if (!SymbolFacts.IsInsideTypeNamed(syntax, n => n == "SceneLoader"))
                    rule = UnidadDiagnostics.InstantiateOutsideFactory;
            }
            else if (SymbolFacts.IsType(type, "UnityEngine", "Resources") && ResourcesLoads.Contains(name) ||
                     SymbolFacts.IsType(type, "UnityEngine", "AssetBundle") && BundleLoads.Contains(name) ||
                     SymbolFacts.IsType(type, "UnityEngine.AddressableAssets", "Addressables") && name.StartsWith("Load"))
            {
                if (!IsInsideAssetLibrary(context.ContainingSymbol))
                    rule = UnidadDiagnostics.AssetLoadOutsideLibrary;
            }

            if (rule == null) return;
            context.ReportDiagnostic(Diagnostic.Create(rule, syntax.GetLocation(),
                SymbolFacts.EnclosingTypeName(syntax), SymbolFacts.Describe(method)));
        }

        private static void AnalyzeAssetDatabase(SyntaxNodeAnalysisContext context)
        {
            var access = (MemberAccessExpressionSyntax)context.Node;
            if (context.SemanticModel.GetSymbolInfo(access.Expression, context.CancellationToken).Symbol
                is not INamedTypeSymbol receiver || !SymbolFacts.IsType(receiver, "UnityEditor", "AssetDatabase")) return;
            context.ReportDiagnostic(Diagnostic.Create(UnidadDiagnostics.AssetDatabaseOutsideEditor, access.GetLocation(),
                SymbolFacts.EnclosingTypeName(access), "AssetDatabase." + access.Name.Identifier.Text));
        }

        private static bool IsInsideAssetLibrary(ISymbol symbol)
        {
            for (var t = symbol as INamedTypeSymbol ?? symbol?.ContainingType; t != null; t = t.ContainingType)
                if (t.ToDisplayString() == AssetLibrary) return true;
            return false;
        }
    }
}
