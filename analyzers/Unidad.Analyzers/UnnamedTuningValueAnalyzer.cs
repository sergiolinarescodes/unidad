using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Unidad.Analyzers
{
    /// <summary>
    /// UD006 — a tuning type must expose its numbers as NAMED members.
    /// <para>
    /// "Numbers live in one place" only works if there is a name to reference. A type called
    /// <c>*Seed</c>, <c>*Config</c>, <c>*Style</c>, <c>*Policy</c> or <c>*Tuning</c> is by
    /// definition the owner of its values, so a bare literal buried in one of its method bodies is
    /// a number with no name — which is how the same value ends up retyped in a test, a policy and
    /// a design doc, then drifts apart silently.
    /// </para>
    /// <para>
    /// Literals inside <c>const</c> / <c>static readonly</c> initialisers, property bodies and enum
    /// members are all fine: those ARE the names.
    /// </para>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnnamedTuningValueAnalyzer : DiagnosticAnalyzer
    {
        private static readonly ImmutableArray<string> TuningTypeSuffixes =
            ImmutableArray.Create("Seed", "Config", "Style", "Policy", "Tuning");

        /// <summary>Neutral values that carry no tuning intent and would only add noise.</summary>
        private static readonly ImmutableHashSet<string> Neutral =
            ImmutableHashSet.Create("0", "1", "0f", "1f", "0d", "1d", "0.0", "1.0");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(UnidadDiagnostics.UnnamedTuningValue);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                if (!SymbolFacts.IsAnalyzed(start.Compilation)) return;
                start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.NumericLiteralExpression);
            });
        }

        private static void Analyze(SyntaxNodeAnalysisContext context)
        {
            var literal = (LiteralExpressionSyntax)context.Node;

            var text = literal.Token.Text;
            if (Neutral.Contains(text)) return;

            var type = literal.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
            if (type == null) return;
            if (!TuningTypeSuffixes.Any(s => type.Identifier.Text.EndsWith(s))) return;

            if (IsAlreadyNamed(literal)) return;

            context.ReportDiagnostic(Diagnostic.Create(
                UnidadDiagnostics.UnnamedTuningValue,
                literal.GetLocation(),
                type.Identifier.Text,
                text));
        }

        /// <summary>
        /// True when the literal already sits behind a name.
        /// <para>
        /// A static PRESET FACTORY counts as a name: <c>static CardPanelStyle Standard()</c>
        /// is "the numbers this preset means", and consumers reference the method rather than
        /// retyping its values — which is the drift the law actually prevents. What the rule is
        /// really hunting is a magic literal inside a COMPUTATION
        /// (<c>Curve(t) => t * 1.5f + 220f</c>), where the number has no name at all.
        /// </para>
        /// <para>
        /// ponytail: a property is treated as a name wholesale, so magic numbers inside a
        /// multi-statement property accessor slip through. Tighten to "initialiser or arrow body
        /// only" if a project ever puts real logic in a tuning property.
        /// </para>
        /// </summary>
        private static bool IsAlreadyNamed(SyntaxNode literal)
        {
            foreach (var ancestor in literal.Ancestors())
            {
                switch (ancestor)
                {
                    case EnumMemberDeclarationSyntax:
                    case PropertyDeclarationSyntax:
                        return true;

                    case FieldDeclarationSyntax field:
                        var modifiers = field.Modifiers;
                        return modifiers.Any(SyntaxKind.ConstKeyword) ||
                               (modifiers.Any(SyntaxKind.StaticKeyword) &&
                                modifiers.Any(SyntaxKind.ReadOnlyKeyword));

                    case MethodDeclarationSyntax method:
                        return IsPresetFactory(method);

                    case TypeDeclarationSyntax:
                        return false;   // reached the type without passing a name
                }
            }

            return false;
        }

        /// <summary>
        /// A static method on a tuning type that returns that same type — i.e. a named preset.
        /// </summary>
        private static bool IsPresetFactory(MethodDeclarationSyntax method)
        {
            if (!method.Modifiers.Any(SyntaxKind.StaticKeyword)) return false;

            var owner = method.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
            if (owner == null) return false;

            var returned = method.ReturnType switch
            {
                IdentifierNameSyntax i  => i.Identifier.Text,
                QualifiedNameSyntax q   => q.Right.Identifier.Text,
                GenericNameSyntax g     => g.Identifier.Text,
                _                       => null,
            };

            return returned == owner.Identifier.Text;
        }
    }
}
