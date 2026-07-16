// C# 9+ init-only setters (used by the record events) require this marker type,
// which netstandard2.1 does not ship. It must be internal and per-assembly — every
// asmdef that declares records needs its own copy.
namespace System.Runtime.CompilerServices
{
    internal class IsExternalInit
    {
    }
}
