using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Unidad.Analyzers
{
    /// <summary>
    /// UD008 — devices and the OS cursor are read only by an input bridge.
    /// <para>
    /// A view that polls <c>Mouse.current</c> or <c>Input.mousePosition</c> cannot be driven by a test or a
    /// replay, and a warp steals the physical cursor from the person at the desk. Only a type named
    /// <c>*InputBridge</c> (or <c>PickService</c>) reads devices; everything else receives commands or the
    /// action value it was handed. An <c>InputAction.ReadValue&lt;T&gt;()</c> is never flagged.
    /// </para>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DeviceReadAnalyzer : DiagnosticAnalyzer
    {
        private static readonly ImmutableArray<string> Devices =
            ImmutableArray.Create("Mouse", "Keyboard", "Gamepad", "Pointer", "Touchscreen", "Pen");

        private const string InputSystem = "UnityEngine.InputSystem";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(UnidadDiagnostics.DeviceReadOutsideBridge);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                if (!SymbolFacts.IsAnalyzed(start.Compilation, start.Options)) return;
                start.RegisterOperationAction(Analyze,
                    OperationKind.PropertyReference, OperationKind.FieldReference,
                    OperationKind.Invocation, OperationKind.EventReference);
            });
        }

        private static bool IsBridge(string name) => name.EndsWith("InputBridge") || name == "PickService";

        private static void Analyze(OperationAnalysisContext context)
        {
            var op = context.Operation;
            if (SymbolFacts.IsInsideTypeNamed(op.Syntax, IsBridge)) return;

            var member = op switch
            {
                IPropertyReferenceOperation p => (ISymbol)p.Property,
                IFieldReferenceOperation f    => f.Field,
                IInvocationOperation i        => i.TargetMethod,
                IEventReferenceOperation e    => e.Event,
                _                             => null,
            };
            var type = member?.ContainingType;
            if (type == null) return;

            var flagged =
                SymbolFacts.IsType(type, "UnityEngine", "Input") ||
                op is IPropertyReferenceOperation && member.Name == "current" && IsDevice(type) ||
                op is IInvocationOperation && member.Name == "WarpCursorPosition" && SymbolFacts.IsType(type, InputSystem, "Mouse") ||
                SymbolFacts.IsType(type, "UnityEngine", "Cursor") && (member.Name == "lockState" || member.Name == "visible") &&
                    op.Parent is ISimpleAssignmentOperation assign && assign.Target == op;
            if (!flagged) return;

            context.ReportDiagnostic(Diagnostic.Create(UnidadDiagnostics.DeviceReadOutsideBridge,
                op.Syntax.GetLocation(), SymbolFacts.EnclosingTypeName(op.Syntax), op.Syntax.ToString()));
        }

        private static bool IsDevice(INamedTypeSymbol type)
        {
            foreach (var d in Devices)
                if (SymbolFacts.IsType(type, InputSystem, d)) return true;
            return false;
        }
    }
}
