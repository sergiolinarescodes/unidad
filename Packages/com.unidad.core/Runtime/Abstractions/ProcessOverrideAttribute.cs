using System;

namespace Unidad.Core.Abstractions
{
    /// <summary>
    /// Declares that a type deliberately overrides <c>Update</c>/<c>FixedUpdate</c> and
    /// drives itself per-frame, rather than being stepped by the tick pump.
    /// <para>
    /// Required by analyzer rule UD002 (matched by simple name). Per-frame work normally belongs on the tick pump, which
    /// services reach through <c>ITickable</c>/<c>IFixedTickable</c> and which a test can step
    /// deterministically; an undeclared override is an update path nothing can step. A few types
    /// genuinely do animate themselves — the pump itself, a self-animating
    /// control — and this attribute makes that set explicit, greppable and reviewable instead of
    /// something a reader has to notice.
    /// </para>
    /// <para>
    /// Lives in Unidad.Core (not the Unity adapter) so any assembly in a project can declare one
    /// without taking a dependency on the adapter layer. It is an attribute; it has no engine
    /// dependency of its own.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ProcessOverrideAttribute : Attribute
    {
        /// <summary>Why this type must drive itself. Required — an undocumented pump is the thing being prevented.</summary>
        public string Reason { get; }

        public ProcessOverrideAttribute(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException(
                    "A [ProcessOverride] needs a reason — that is the entire point of declaring it.",
                    nameof(reason));

            Reason = reason;
        }
    }
}
