// netstandard2.1 lacks the attribute types C# 9-11 features compile against; harmless on net10 (internal duplicates).
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
    internal sealed class RequiredMemberAttribute : Attribute { }
    internal sealed class CompilerFeatureRequiredAttribute : Attribute { public CompilerFeatureRequiredAttribute(string featureName) { } }
}
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Constructor)] internal sealed class SetsRequiredMembersAttribute : Attribute { }
}
#endif
