using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Unidad.Analyzers
{
    /// <summary>
    /// UD014 — no mutable static state.
    /// <para>
    /// Unity 7 runs with no domain reload, so a static survives from one Play session into the next and a
    /// test run leaks into the game. Flagged: a static non-readonly field (<c>[ThreadStatic]</c> included), a
    /// static readonly field of a mutable collection or array type, a static auto-property with a setter or
    /// init, and a static field-like event. The only escape is the pragma with a reason (D-19).
    /// </para>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MutableStaticAnalyzer : DiagnosticAnalyzer
    {
        private static readonly ImmutableHashSet<string> MutableNamespaces = ImmutableHashSet.Create(
            "System.Collections", "System.Collections.Generic", "System.Collections.Concurrent");

        /// <summary>Types in those namespaces that hold no mutable state of their own (U-154).</summary>
        private static readonly ImmutableHashSet<string> ReadOnlyShapes = ImmutableHashSet.Create(
            "IEnumerable", "IEnumerator", "IComparer", "IEqualityComparer", "Comparer", "EqualityComparer",
            "KeyValuePair", "IStructuralComparable", "IStructuralEquatable");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(UnidadDiagnostics.MutableStatic);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                if (!SymbolFacts.IsAnalyzed(start.Compilation, start.Options)) return;
                start.RegisterSymbolAction(Analyze, SymbolKind.Field, SymbolKind.Property, SymbolKind.Event);
            });
        }

        private static void Analyze(SymbolAnalysisContext context)
        {
            var symbol = context.Symbol;
            if (!symbol.IsStatic || symbol.IsImplicitlyDeclared) return;

            var mutable = symbol switch
            {
                IFieldSymbol f    => !f.IsConst && (!f.IsReadOnly || IsMutableType(f.Type)),
                IPropertySymbol p => p.SetMethod != null && HasBackingField(p),
                IEventSymbol e    => IsFieldLike(e),
                _                 => false,
            };
            if (!mutable) return;

            foreach (var location in symbol.Locations)
                if (location.IsInSource)
                    context.ReportDiagnostic(Diagnostic.Create(UnidadDiagnostics.MutableStatic, location, symbol.Name));
        }

        /// <summary>An auto-property (or one using <c>field</c>): the compiler gave it a hidden field.</summary>
        private static bool HasBackingField(ISymbol member) =>
            member.ContainingType.GetMembers().OfType<IFieldSymbol>()
                  .Any(f => SymbolEqualityComparer.Default.Equals(f.AssociatedSymbol, member));

        /// <summary>A field-like event is declared by a variable declarator, not an add/remove block.</summary>
        private static bool IsFieldLike(IEventSymbol e) =>
            e.DeclaringSyntaxReferences.Any(r => r.GetSyntax() is VariableDeclaratorSyntax);

        private static bool IsMutableType(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol) return true;
            var ns = type.ContainingNamespace?.ToDisplayString();
            if (ns == "System.Text" && type.Name == "StringBuilder") return true;
            if (ns == null || !MutableNamespaces.Contains(ns)) return false;
            return !type.Name.StartsWith("IReadOnly") && !ReadOnlyShapes.Contains(type.Name);
        }
    }
}
