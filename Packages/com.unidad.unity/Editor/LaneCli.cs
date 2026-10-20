using System;
using System.Collections.Generic;

namespace Unidad.Adapter.Editor
{
    /// <summary>
    /// Batch entry points of <c>tools/unity-lane.ps1</c> (<c>-executeMethod</c>). Each logs exactly one line
    /// <c>UNIDAD LaneCli.&lt;Method&gt; ok|fail &lt;reason&gt;</c> and exits through <c>EditorApplication.Exit</c>.
    /// </summary>
    public static class LaneCli
    {
        /// <summary>Exit 1 when <c>EditorUtility.scriptCompilationFailed</c>, else 0. No arguments.</summary>
        public static void Compile() => throw new NotImplementedException();

        /// <summary><c>AssetDatabase.Refresh(ForceSynchronousImport)</c>, then <see cref="Compile"/>'s check; exit 0/1. No arguments.</summary>
        public static void Import() => throw new NotImplementedException();

        /// <summary>
        /// Opens a scene and enters Play mode (the interactive testbed; settings travel in <c>UNIDAD_LAUNCH</c>).
        /// Arguments: <c>-unidadScene &lt;Assets/...unity&gt;</c> (required).
        /// </summary>
        public static void PlayScene() => throw new NotImplementedException();
    }

    /// <summary>What <see cref="BuildCli.Build"/> builds.</summary>
    public sealed record BuildCliOptions(string OutputFolder, string ExeName, IReadOnlyList<string> Scenes, bool Development, bool Release = false);

    /// <summary>What a build produced; also written to <c>&lt;out&gt;/build-report.txt</c>.</summary>
    public sealed record BuildCliResult(bool Succeeded, long TotalBytes, int Errors, int Warnings, double Seconds, string ExePath);

    public static class BuildCli
    {
        /// <summary>A StandaloneWindows64 CoreCLR player; writes <c>&lt;out&gt;/build-report.txt</c>.</summary>
        public static BuildCliResult Build(BuildCliOptions options) => throw new NotImplementedException();

        /// <summary>
        /// Batch entry point. Arguments:
        /// <c>-buildOut &lt;dir&gt;</c> (required) the output folder;
        /// <c>-development</c> a development player;
        /// <c>-scene &lt;path&gt;</c> (repeatable) the scenes, default the enabled build-settings scenes;
        /// <c>-release</c> a release player.
        /// Writes <c>&lt;dir&gt;/build-report.txt</c>; exit 0 when the build succeeded, 1 when it failed.
        /// </summary>
        public static void Player() => throw new NotImplementedException();
    }

    /// <summary>Writes the framework's committed assets (the overlay PanelSettings).</summary>
    public static class FrameworkAssets
    {
        public static void Write() => throw new NotImplementedException();
    }

    /// <summary>Sandbox recipes for the Movement sample.</summary>
    public static class SampleRecipes
    {
        /// <summary>Imports the "Movement" sample into Assets/Samples.</summary>
        public static void ImportMovement() => throw new NotImplementedException();

        /// <summary>Writes Assets/Scenes/Movement.unity with one "[Boot]" object and adds it to the build settings.</summary>
        public static void BuildMovementScene() => throw new NotImplementedException();
    }
}
