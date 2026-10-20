using Microsoft.CodeAnalysis;

namespace Unidad.Analyzers
{
    /// <summary>
    /// Every Unidad rule ID in one place.
    /// <para>
    /// These exist because architectural rules written as prose in a CLAUDE.md do not hold. The
    /// project this framework was extracted from carried ~30k words of doctrine and still shipped
    /// the violations the doctrine forbade — the rules were correct, but nothing enforced them at
    /// the moment of typing. A build error does.
    /// </para>
    /// <para>
    /// Escaping a rule is a normal <c>#pragma warning disable UDxxx</c> with a comment saying why.
    /// There is deliberately no bespoke attribute for it: a pragma is greppable, scoped, and
    /// already understood by every tool.
    /// </para>
    /// </summary>
    internal static class UnidadDiagnostics
    {
        public const string Category = "Unidad";

        private static DiagnosticDescriptor Rule(string id, string title, string message, string description = null) =>
            new(id, title, message, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: description);

        // ---- service boundary: a service is pure logic and must be testable with no scene ----

        public static readonly DiagnosticDescriptor NoEngineObjectInService = Rule(
            "UD001", "A service must not create engine objects or call engine singletons",
            "'{0}' creates or destroys an engine object, or calls an engine singleton ({1}). Services hold rules and " +
            "state; objects come from IGameObjectFactory or ComponentPool, requested by a stage host or view, and input " +
            "arrives as commands from an input bridge.",
            "A service that constructs engine objects, loads files or reads devices cannot be stood up in a unit test " +
            "without the engine.");

        public static readonly DiagnosticDescriptor UndeclaredFrameHook = Rule(
            "UD002", "Update/LateUpdate/FixedUpdate/OnGUI is not declared",
            "'{0}' declares {1} without [ProcessOverride]. Per-frame work belongs on a TickBinding; if this component " +
            "genuinely animates itself, declare it with a reason.",
            "Every extra frame hook is an untracked, unsteppable update path. Marking them makes the set of " +
            "self-animating types explicit and reviewable.");

        public static readonly DiagnosticDescriptor NoSceneLookupInService = Rule(
            "UD003", "A service must not reach into the scene",
            "'{0}' reaches into the scene ({1}). Take what you need as a constructor dependency; a lookup by name or " +
            "hierarchy is not injection.");

        public static readonly DiagnosticDescriptor NoAmbientTime = Rule(
            "UD004", "A service must not read ambient time",
            "'{0}' reads ambient time ({1}). Inject ITimeProvider, or take the step from ITickable.Tick(dt) / " +
            "IFixedTickable.FixedTick(dt).",
            "A service that reads the clock directly cannot be stepped deterministically, so its tests either sleep " +
            "or flake.");

        // UD005 (hand-typed pin) and UD007 (a measurement must not assert) were retired on
        // 2026-09-07 with the snapshot and measurement lanes they policed.

        public static readonly DiagnosticDescriptor UnnamedTuningValue = Rule(
            "UD006", "A tuning value must be a named constant",
            "'{0}' contains the bare numeric literal {1}. A value a test or policy consumes is " +
            "written once, as a named const or static readonly member.",
            "Two copies of a tuning number drift apart silently. Naming it in the one type " +
            "that owns it gives every consumer something to reference instead of retyping.");

        public static readonly DiagnosticDescriptor DeviceReadOutsideBridge = Rule(
            "UD008", "Devices and the cursor are read only by an input bridge",
            "'{0}' reads a device or the OS cursor ({1}). Only an *InputBridge (or PickService) reads devices; " +
            "everything else receives commands or the action value it was handed.");

        public static readonly DiagnosticDescriptor DeterministicMath = Rule(
            "UD009", "Simulation math must be platform-independent",
            "'{0}' can round differently on another OS, runtime or Burst mode. Use integer math or " +
            "Unidad.Core.Primitives.DetMath.",
            "Applies only to an assembly marked [assembly: DeterministicSimulation]. Platform libm transcendentals " +
            "round differently across machines, so two machines stepping the same simulation diverge.");

        public static readonly DiagnosticDescriptor InstantiateOutsideFactory = Rule(
            "UD010", "Objects and scenes come from the factory and the scene loader",
            "'{0}' calls {1} directly. Instantiate through IGameObjectFactory / ComponentPool, and load scenes " +
            "through SceneLoader.");

        public static readonly DiagnosticDescriptor AssetLoadOutsideLibrary = Rule(
            "UD011", "Assets are loaded through AssetLibrary",
            "'{0}' loads an asset directly ({1}). Use AssetLibrary, and declare cold assets with ScenePreparation " +
            "before play.");

        public static readonly DiagnosticDescriptor AssetDatabaseOutsideEditor = Rule(
            "UD011", "Assets are loaded through AssetLibrary",
            "'{0}' uses {1}. AssetDatabase is editor-only; use it in an *.Editor assembly.");

        public static readonly DiagnosticDescriptor GpuReadback = Rule(
            "UD012", "Nothing reads GPU data back",
            "'{0}' reads data back from the GPU and stalls the frame. Keep the value you wrote instead.");

        public static readonly DiagnosticDescriptor NoAmbientRandomness = Rule(
            "UD013", "A service must not create its own randomness",
            "'{0}' creates its own randomness ({1}). Take a DeterministicRandom built from ISeedSource / " +
            "SeedMix.Derive.");

        public static readonly DiagnosticDescriptor MutableStatic = Rule(
            "UD014", "No mutable static state",
            "'{0}' is mutable static state. Unity 7 has no domain reload, so it survives Play sessions. Make it an " +
            "instance member of a container singleton, a const, or a static readonly immutable value.");

        public static readonly DiagnosticDescriptor BurstSubset = Rule(
            "UD015", "Burst subset violation",
            "'{0}' is outside the Burst subset: {1}");

        public static readonly DiagnosticDescriptor UnidadNamespaceInGame = Rule(
            "UD016", "The Unidad namespace belongs to the framework",
            "'{0}' is declared in namespace '{1}' inside assembly '{2}'. Unidad.* belongs to the framework; use " +
            "Cds.<Module>[.<Feature>].");
    }
}
