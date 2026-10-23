using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Unidad.Analyzers
{
    /// <summary>
    /// UD015 — an assembly marked <c>[assembly: BurstSubset]</c> stays inside what Burst compiles bit-for-bit:
    /// unmanaged structs, static classes, pointers, fixed buffers, spans, integer math and <c>switch</c>
    /// dispatch. No managed types, arrays, collection expressions, delegates, strings, floats, boxing, LINQ,
    /// virtual or interface calls (except through a constrained type parameter), params, exceptions, foreach
    /// over a non-span, locks, iterators or async. Attribute classes, attribute arguments and <c>nameof</c>
    /// are exempt (the assembly's own marker attributes live in it).
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class BurstSubsetAnalyzer : DiagnosticAnalyzer
    {
        private const string OptInAttribute = "BurstSubset";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(UnidadDiagnostics.BurstSubset);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                if (!SymbolFacts.IsAnalyzed(start.Compilation, start.Options)) return;
                if (!SymbolFacts.HasAssemblyAttribute(start.Compilation, OptInAttribute)) return;

                start.RegisterSyntaxNodeAction(AnalyzeTypeDeclaration,
                    SyntaxKind.ClassDeclaration, SyntaxKind.RecordDeclaration, SyntaxKind.DelegateDeclaration);
                start.RegisterSyntaxNodeAction(AnalyzeDeclaredType,
                    SyntaxKind.VariableDeclaration, SyntaxKind.PropertyDeclaration, SyntaxKind.Parameter,
                    SyntaxKind.MethodDeclaration, SyntaxKind.LocalFunctionStatement);
                start.RegisterSyntaxNodeAction(AnalyzeAlways,
                    SyntaxKind.ArrayCreationExpression, SyntaxKind.ImplicitArrayCreationExpression,
                    SyntaxKind.CollectionExpression,
                    SyntaxKind.SimpleLambdaExpression, SyntaxKind.ParenthesizedLambdaExpression,
                    SyntaxKind.AnonymousMethodExpression,
                    SyntaxKind.StringLiteralExpression, SyntaxKind.InterpolatedStringExpression,
                    SyntaxKind.NumericLiteralExpression, SyntaxKind.QueryExpression,
                    SyntaxKind.ThrowStatement, SyntaxKind.ThrowExpression, SyntaxKind.CatchClause,
                    SyntaxKind.ForEachStatement, SyntaxKind.ForEachVariableStatement,
                    SyntaxKind.LockStatement, SyntaxKind.YieldReturnStatement, SyntaxKind.YieldBreakStatement,
                    SyntaxKind.AwaitExpression);
                start.RegisterOperationAction(AnalyzeOperation,
                    OperationKind.ObjectCreation, OperationKind.DelegateCreation, OperationKind.Conversion,
                    OperationKind.Invocation, OperationKind.PropertyReference);
            });
        }

        private static void Report(SyntaxNodeAnalysisContext c, Location at, string reason) =>
            c.ReportDiagnostic(Diagnostic.Create(UnidadDiagnostics.BurstSubset, at, SymbolFacts.EnclosingTypeName(c.Node), reason));

        private static void Report(OperationAnalysisContext c, string reason) =>
            c.ReportDiagnostic(Diagnostic.Create(UnidadDiagnostics.BurstSubset, c.Operation.Syntax.GetLocation(),
                SymbolFacts.EnclosingTypeName(c.Operation.Syntax), reason));

        /// <summary>Attribute classes, attribute arguments and nameof(...) are not kernel code.</summary>
        private static bool IsExempt(SyntaxNode node, SemanticModel model)
        {
            for (var n = node; n != null; n = n.Parent)
            {
                switch (n)
                {
                    case AttributeListSyntax:
                        return true;
                    case InvocationExpressionSyntax call when call.Expression is IdentifierNameSyntax { Identifier.Text: "nameof" }:
                        return true;
                    case ClassDeclarationSyntax cls:
                        if (model.GetDeclaredSymbol(cls) is INamedTypeSymbol t && SymbolFacts.DerivesFrom(t, "System", "Attribute"))
                            return true;
                        break;
                }
            }
            return false;
        }

        // 1. managed type declarations
        private static void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext c)
        {
            if (IsExempt(c.Node, c.SemanticModel)) return;
            switch (c.Node)
            {
                case ClassDeclarationSyntax cls when !cls.Modifiers.Any(SyntaxKind.StaticKeyword):
                    Report(c, cls.Identifier.GetLocation(), "a managed type declaration");
                    break;
                case RecordDeclarationSyntax rec when !rec.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword):
                    Report(c, rec.Identifier.GetLocation(), "a managed type declaration");
                    break;
                case DelegateDeclarationSyntax del:
                    Report(c, del.Identifier.GetLocation(), "a managed type declaration");
                    break;
            }
        }

        // 2, 3 (declared types), 14 (params parameters), 17 (async methods)
        private static void AnalyzeDeclaredType(SyntaxNodeAnalysisContext c)
        {
            if (IsExempt(c.Node, c.SemanticModel)) return;
            var typeSyntax = c.Node switch
            {
                VariableDeclarationSyntax v      => v.Type.IsVar ? null : v.Type,
                PropertyDeclarationSyntax p      => p.Type,
                ParameterSyntax p                => p.Type,
                MethodDeclarationSyntax m        => m.ReturnType,
                LocalFunctionStatementSyntax f   => f.ReturnType,
                _                                => null,
            };

            if (c.Node is ParameterSyntax param && param.Modifiers.Any(SyntaxKind.ParamsKeyword))
                Report(c, param.GetLocation(), "a params parameter");
            if (c.Node is MethodDeclarationSyntax { Modifiers: var mm } method && mm.Any(SyntaxKind.AsyncKeyword))
                Report(c, method.Identifier.GetLocation(), "a managed runtime feature");
            if (c.Node is LocalFunctionStatementSyntax { Modifiers: var fm } local && fm.Any(SyntaxKind.AsyncKeyword))
                Report(c, local.Identifier.GetLocation(), "a managed runtime feature");
            if (typeSyntax == null) return;

            var type = c.SemanticModel.GetTypeInfo(typeSyntax, c.CancellationToken).Type;
            if (type == null || type.SpecialType == SpecialType.System_Void) return;
            if (type.IsReferenceType)
                Report(c, typeSyntax.GetLocation(), "a managed type (" + type.ToDisplayString() + ")");
            else if (IsFloating(type))
                Report(c, typeSyntax.GetLocation(), "floating point (" + type.ToDisplayString() + ")");
        }

        // 5, 6, 7 (lambdas), 8, 9 (real literals), 11 (query), 15, 16, 17
        private static void AnalyzeAlways(SyntaxNodeAnalysisContext c)
        {
            var node = c.Node;
            if (IsExempt(node, c.SemanticModel)) return;
            var at = node.GetLocation();
            switch (node)
            {
                case ArrayCreationExpressionSyntax:
                case ImplicitArrayCreationExpressionSyntax:
                    Report(c, at, "a managed array"); break;
                case CollectionExpressionSyntax:
                    Report(c, at, "a collection expression"); break;
                case LambdaExpressionSyntax:
                case AnonymousMethodExpressionSyntax:
                    Report(c, at, "a delegate or lambda"); break;
                case InterpolatedStringExpressionSyntax:
                    Report(c, at, "a string"); break;
                case LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression):
                    Report(c, at, "a string"); break;
                case LiteralExpressionSyntax lit when lit.Token.Value is float || lit.Token.Value is double || lit.Token.Value is decimal:
                    Report(c, at, "floating point"); break;
                case QueryExpressionSyntax:
                    Report(c, at, "LINQ"); break;
                case ThrowStatementSyntax:
                case ThrowExpressionSyntax:
                    Report(c, at, "exception handling"); break;
                case CatchClauseSyntax katch:
                    Report(c, katch.CatchKeyword.GetLocation(), "exception handling"); break;
                case CommonForEachStatementSyntax loop:
                    var collection = c.SemanticModel.GetTypeInfo(loop.Expression, c.CancellationToken).Type;
                    if (!SymbolFacts.IsType(collection, "System", "Span") && !SymbolFacts.IsType(collection, "System", "ReadOnlySpan"))
                        Report(c, loop.ForEachKeyword.GetLocation(), "foreach over a non-span");
                    break;
                case LockStatementSyntax:
                case YieldStatementSyntax:
                case AwaitExpressionSyntax:
                    Report(c, at, "a managed runtime feature"); break;
            }
        }

        // 4, 7 (method groups), 9 (conversions), 10, 11, 12, 13
        private static void AnalyzeOperation(OperationAnalysisContext c)
        {
            var op = c.Operation;
            if (IsExempt(op.Syntax, op.SemanticModel)) return;
            switch (op)
            {
                case IObjectCreationOperation create when create.Type?.IsReferenceType == true:
                    Report(c, "new of a managed type"); break;
                case IDelegateCreationOperation del when del.Target is IMethodReferenceOperation:
                    Report(c, "a delegate or lambda"); break;
                case IConversionOperation conv:
                    if (conv.GetConversion().IsBoxing) Report(c, "boxing");
                    else if (conv.Type != null && IsFloating(conv.Type) &&
                             !SymbolEqualityComparer.Default.Equals(conv.Type, conv.Operand.Type))
                        Report(c, "floating point");
                    break;
                case IInvocationOperation call:
                    AnalyzeCall(c, call); break;
                case IPropertyReferenceOperation prop:
                    if (IsDispatched(prop.Property, prop.Instance)) Report(c, "a virtual or interface call");
                    break;
            }
        }

        private static void AnalyzeCall(OperationAnalysisContext c, IInvocationOperation call)
        {
            var method = call.TargetMethod;
            var owner = method.ContainingType;
            if (SymbolFacts.IsType(owner, "System.Linq", "Enumerable") || SymbolFacts.IsType(owner, "System.Linq", "Queryable"))
                Report(c, "LINQ");
            if (IsDispatched(method, call.Instance))
                Report(c, "a virtual or interface call");
            if (call.Arguments.Any(a => a.Parameter != null && a.Parameter.IsParams && a.ArgumentKind != ArgumentKind.Explicit))
                Report(c, "a params call");
        }

        /// <summary>
        /// Virtual, abstract, override or interface member, called on a receiver that is not a type
        /// parameter (a constrained call), and not a struct calling its own member.
        /// </summary>
        private static bool IsDispatched(ISymbol member, IOperation instance)
        {
            var dispatched = member.IsVirtual || member.IsAbstract || member.IsOverride ||
                             member.ContainingType?.TypeKind == TypeKind.Interface;
            if (!dispatched) return false;
            if (instance == null) return !member.IsStatic;   // a static abstract member is reached only through T
            var receiver = instance.Type;
            if (receiver == null || receiver.TypeKind == TypeKind.TypeParameter) return false;
            if (receiver.IsValueType && SymbolEqualityComparer.Default.Equals(member.ContainingType, receiver)) return false;
            return true;
        }

        private static bool IsFloating(ITypeSymbol t) =>
            t.SpecialType == SpecialType.System_Single || t.SpecialType == SpecialType.System_Double ||
            t.SpecialType == SpecialType.System_Decimal;
    }
}
