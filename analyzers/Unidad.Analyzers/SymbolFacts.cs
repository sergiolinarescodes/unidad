using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Unidad.Analyzers
{
    /// <summary>
    /// The shared questions the rules ask. The analyzer references no Unity or Unidad assembly, so engine symbols
    /// match by namespace and name, and opt-in attributes by simple name in any namespace (U-151).
    /// </summary>
    internal static class SymbolFacts
    {
        /// <summary>
        /// True if <paramref name="node"/> sits inside a service implementation — a class, struct or
        /// record whose name ends in "Service" — at any depth: a type nested in a service, a lambda or
        /// a local function in one, is still the service.
        /// </summary>
        public static bool IsInsideService(SyntaxNode node)
        {
            for (var current = node; current != null; current = current.Parent)
            {
                if (current is TypeDeclarationSyntax type && type is not InterfaceDeclarationSyntax &&
                    type.Identifier.Text.EndsWith("Service") &&
                    !type.Identifier.Text.EndsWith("TestService"))
                    return true;
            }

            return false;
        }

        /// <summary>The enclosing type's name, for diagnostic messages.</summary>
        public static string EnclosingTypeName(SyntaxNode node)
        {
            for (var current = node; current != null; current = current.Parent)
                if (current is TypeDeclarationSyntax type) return type.Identifier.Text;
            return "<unknown>";
        }

        /// <summary>The simple name being invoked, for an invocation of any shape.</summary>
        public static string InvokedName(InvocationExpressionSyntax invocation) =>
            invocation?.Expression switch
            {
                MemberAccessExpressionSyntax m => m.Name.Identifier.Text,
                IdentifierNameSyntax i         => i.Identifier.Text,
                GenericNameSyntax g            => g.Identifier.Text,
                MemberBindingExpressionSyntax b => b.Name.Identifier.Text,
                _                              => null,
            };

        /// <summary>True if any attribute on the member has the given simple name.</summary>
        public static bool HasAttribute(MemberDeclarationSyntax member, string simpleName) =>
            member.AttributeLists
                  .SelectMany(l => l.Attributes)
                  .Select(a => a.Name switch
                  {
                      QualifiedNameSyntax q => q.Right.Identifier.Text,
                      SimpleNameSyntax s    => s.Identifier.Text,
                      _                     => null,
                  })
                  .Any(n => n == simpleName || n == simpleName + "Attribute");

        /// <summary>Rules run only on our own assemblies (U-150).</summary>
        public static bool IsAnalyzed(Compilation c) =>
            c.AssemblyName != null && (c.AssemblyName.StartsWith("Unidad.", StringComparison.Ordinal) ||
                                       c.AssemblyName.StartsWith("Cds.", StringComparison.Ordinal));

        /// <summary>An editor assembly is one whose name ends in ".Editor" (U-152).</summary>
        public static bool IsEditorAssembly(Compilation c) =>
            c.AssemblyName != null && c.AssemblyName.EndsWith(".Editor", StringComparison.Ordinal);

        /// <summary>True if the symbol's namespace chain is rooted at UnityEngine, UnityEditor or Unity.</summary>
        public static bool IsUnitySymbol(ISymbol s)
        {
            var ns = s is INamespaceSymbol n ? n : s?.ContainingNamespace;
            if (ns == null || ns.IsGlobalNamespace) return false;
            while (!ns.ContainingNamespace.IsGlobalNamespace) ns = ns.ContainingNamespace;
            return ns.Name == "UnityEngine" || ns.Name == "UnityEditor" || ns.Name == "Unity";
        }

        /// <summary>Exact namespace and name, e.g. ("UnityEngine", "Time"). Generic types compare their definition.</summary>
        public static bool IsUnityType(ITypeSymbol t, string ns, string name) => IsType(t, ns, name);

        public static bool IsType(ITypeSymbol t, string ns, string name) =>
            t != null && t.Name == name && t.ContainingType == null &&
            t.ContainingNamespace?.ToDisplayString() == ns;

        /// <summary>True if <paramref name="t"/> is, or derives from, the type ns.name.</summary>
        public static bool DerivesFrom(ITypeSymbol t, string ns, string name)
        {
            for (var b = t; b != null; b = b.BaseType)
                if (IsType(b, ns, name)) return true;
            return false;
        }

        /// <summary>An assembly attribute named X or XAttribute, in any namespace.</summary>
        public static bool HasAssemblyAttribute(Compilation c, string simpleName) =>
            c.Assembly.GetAttributes().Any(a => NameMatches(a.AttributeClass?.Name, simpleName));

        /// <summary>An attribute named X or XAttribute on the symbol, in any namespace.</summary>
        public static bool HasAttribute(ISymbol s, string simpleName) =>
            s.GetAttributes().Any(a => NameMatches(a.AttributeClass?.Name, simpleName));

        /// <summary>True if any enclosing type, at any depth, has a name that <paramref name="match"/> accepts.</summary>
        public static bool IsInsideTypeNamed(SyntaxNode n, Func<string, bool> match)
        {
            for (var current = n; current != null; current = current.Parent)
                if (current is BaseTypeDeclarationSyntax type && match(type.Identifier.Text)) return true;
            return false;
        }

        /// <summary>"Type.Member" for messages.</summary>
        public static string Describe(ISymbol member) => member.ContainingType?.Name + "." + member.Name;

        private static bool NameMatches(string name, string simpleName) =>
            name == simpleName || name == simpleName + "Attribute";
    }
}
