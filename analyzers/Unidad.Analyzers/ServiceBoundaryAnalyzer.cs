using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Unidad.Analyzers
{
    /// <summary>
    /// UD001 / UD003 / UD004 / UD013 — the service boundary. A service holds rules and state and must be
    /// constructible in a unit test with no scene, no clock, no renderer, no input device and no ambient
    /// randomness. All four share the "am I inside a service" walk, so they share one analyzer. Unity
    /// targets are matched through the semantic model only: a domain type named Input, Time or Random is
    /// never flagged.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ServiceBoundaryAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>Engine singletons whose every member is off limits to a service: (namespace, type).</summary>
        private static readonly ImmutableArray<(string Ns, string Name)> EngineSingletons = ImmutableArray.Create(
            ("UnityEngine", "Resources"), ("UnityEngine.SceneManagement", "SceneManager"), ("UnityEngine", "Input"),
            ("UnityEngine", "Physics"), ("UnityEngine", "Physics2D"), ("UnityEngine", "Graphics"),
            ("UnityEngine", "Screen"), ("UnityEngine", "Display"), ("UnityEngine", "Application"),
            ("UnityEngine", "PlayerPrefs"), ("UnityEngine", "AudioSettings"), ("UnityEngine", "QualitySettings"),
            ("UnityEditor", "AssetDatabase"), ("UnityEngine.AddressableAssets", "Addressables"),
            ("UnityEngine.InputSystem", "Keyboard"), ("UnityEngine.InputSystem", "Mouse"),
            ("UnityEngine.InputSystem", "Gamepad"), ("UnityEngine.InputSystem", "Pointer"),
            ("UnityEngine.InputSystem", "Touchscreen"), ("UnityEngine.InputSystem", "Pen"),
            ("UnityEngine.InputSystem", "InputSystem"));

        private static readonly ImmutableHashSet<string> ObjectLifetimeMethods = ImmutableHashSet.Create(
            "Instantiate", "Destroy", "DestroyImmediate", "DontDestroyOnLoad");

        private static readonly ImmutableHashSet<string> GameObjectFinds = ImmutableHashSet.Create(
            "Find", "FindWithTag", "FindGameObjectWithTag", "FindGameObjectsWithTag");

        private static readonly ImmutableHashSet<string> ObjectFinds = ImmutableHashSet.Create(
            "FindObjectOfType", "FindObjectsOfType", "FindFirstObjectByType", "FindAnyObjectByType", "FindObjectsByType");

        private static readonly ImmutableHashSet<string> ComponentLookups = ImmutableHashSet.Create(
            "GetComponent", "GetComponents", "GetComponentInChildren", "GetComponentsInChildren",
            "GetComponentInParent", "GetComponentsInParent", "TryGetComponent");

        private static readonly ImmutableArray<(string Ns, string Name, string Member)> AmbientClocks = ImmutableArray.Create(
            ("System", "DateTime", "Now"), ("System", "DateTime", "UtcNow"), ("System", "DateTime", "Today"),
            ("System", "DateTimeOffset", "Now"), ("System", "DateTimeOffset", "UtcNow"),
            ("System", "Environment", "TickCount"), ("System", "Environment", "TickCount64"),
            ("System.Diagnostics", "Stopwatch", "StartNew"), ("System.Diagnostics", "Stopwatch", "GetTimestamp"));

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                UnidadDiagnostics.NoEngineObjectInService,
                UnidadDiagnostics.NoSceneLookupInService,
                UnidadDiagnostics.NoAmbientTime,
                UnidadDiagnostics.NoAmbientRandomness);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                if (!SymbolFacts.IsAnalyzed(start.Compilation)) return;
                start.RegisterOperationAction(AnalyzeCreation, OperationKind.ObjectCreation);
                start.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
                start.RegisterOperationAction(AnalyzePropertyRead, OperationKind.PropertyReference);
                start.RegisterSyntaxNodeAction(AnalyzeStaticAccess, SyntaxKind.SimpleMemberAccessExpression);
            });
        }

        private static void Report(OperationAnalysisContext context, DiagnosticDescriptor rule, string what) =>
            context.ReportDiagnostic(Diagnostic.Create(rule, context.Operation.Syntax.GetLocation(),
                SymbolFacts.EnclosingTypeName(context.Operation.Syntax), what));

        private static void AnalyzeCreation(OperationAnalysisContext context)
        {
            if (!SymbolFacts.IsInsideService(context.Operation.Syntax)) return;
            var created = context.Operation.Type;
            if (created == null) return;

            if (SymbolFacts.DerivesFrom(created, "UnityEngine", "Object"))
                Report(context, UnidadDiagnostics.NoEngineObjectInService, "new " + created.Name);
            else if (SymbolFacts.IsType(created, "System.Diagnostics", "Stopwatch"))
                Report(context, UnidadDiagnostics.NoAmbientTime, "new Stopwatch");
            else if (SymbolFacts.IsType(created, "System", "Random"))
                Report(context, UnidadDiagnostics.NoAmbientRandomness, "new Random");
        }

        private static void AnalyzeInvocation(OperationAnalysisContext context)
        {
            if (!SymbolFacts.IsInsideService(context.Operation.Syntax)) return;
            var method = ((IInvocationOperation)context.Operation).TargetMethod;
            var type = method.ContainingType;
            var name = method.Name;

            if (SymbolFacts.IsType(type, "UnityEngine", "Object") && ObjectLifetimeMethods.Contains(name) ||
                SymbolFacts.IsType(type, "UnityEngine", "ScriptableObject") && name == "CreateInstance" ||
                SymbolFacts.IsType(type, "UnityEngine", "GameObject") && name == "AddComponent")
            {
                Report(context, UnidadDiagnostics.NoEngineObjectInService, SymbolFacts.Describe(method));
                return;
            }

            if (SymbolFacts.IsType(type, "UnityEngine", "GameObject") && GameObjectFinds.Contains(name) ||
                SymbolFacts.IsType(type, "UnityEngine", "Object") && ObjectFinds.Contains(name) ||
                (SymbolFacts.IsType(type, "UnityEngine", "Component") || SymbolFacts.IsType(type, "UnityEngine", "GameObject")) &&
                    ComponentLookups.Contains(name) ||
                SymbolFacts.IsType(type, "UnityEngine", "Transform") && (name == "Find" || name == "GetChild") ||
                SymbolFacts.IsType(type, "UnityEngine.SceneManagement", "Scene") && name == "GetRootGameObjects" ||
                SymbolFacts.IsType(type, "UnityEngine.UIElements", "UQueryExtensions"))
            {
                Report(context, UnidadDiagnostics.NoSceneLookupInService, SymbolFacts.Describe(method));
                return;
            }

            if (SymbolFacts.IsType(type, "System", "Guid") && name == "NewGuid")
                Report(context, UnidadDiagnostics.NoAmbientRandomness, "Guid.NewGuid");
        }

        private static void AnalyzePropertyRead(OperationAnalysisContext context)
        {
            if (!SymbolFacts.IsInsideService(context.Operation.Syntax)) return;
            var op = (IPropertyReferenceOperation)context.Operation;
            if (op.Parent is ISimpleAssignmentOperation assign && assign.Target == op) return;

            var type = op.Property.ContainingType;
            var name = op.Property.Name;
            if (SymbolFacts.IsType(type, "UnityEngine", "Transform") && (name == "parent" || name == "root") ||
                SymbolFacts.IsType(type, "UnityEngine", "Camera") && (name == "main" || name == "current"))
                Report(context, UnidadDiagnostics.NoSceneLookupInService, SymbolFacts.Describe(op.Property));
        }

        /// <summary>
        /// Static reads through a TYPE receiver: engine singletons, clocks, ambient random. An instance
        /// <c>dt</c> handed to Tick(dt) is never flagged because its receiver is not a type.
        /// </summary>
        private static void AnalyzeStaticAccess(SyntaxNodeAnalysisContext context)
        {
            var access = (MemberAccessExpressionSyntax)context.Node;
            if (!SymbolFacts.IsInsideService(access)) return;
            if (context.SemanticModel.GetSymbolInfo(access.Expression, context.CancellationToken).Symbol
                is not INamedTypeSymbol receiver) return;

            var member = access.Name.Identifier.Text;
            var what = receiver.Name + "." + member;
            DiagnosticDescriptor rule = null;

            foreach (var (ns, name) in EngineSingletons)
                if (SymbolFacts.IsType(receiver, ns, name)) rule = UnidadDiagnostics.NoEngineObjectInService;

            if (SymbolFacts.IsType(receiver, "UnityEngine", "Time")) rule = UnidadDiagnostics.NoAmbientTime;
            foreach (var (ns, name, clock) in AmbientClocks)
                if (member == clock && SymbolFacts.IsType(receiver, ns, name)) rule = UnidadDiagnostics.NoAmbientTime;

            if (SymbolFacts.IsType(receiver, "UnityEngine", "Random") || SymbolFacts.IsType(receiver, "System", "Random"))
                rule = UnidadDiagnostics.NoAmbientRandomness;

            if (rule == null) return;
            context.ReportDiagnostic(Diagnostic.Create(rule, access.GetLocation(), SymbolFacts.EnclosingTypeName(access), what));
        }
    }
}
