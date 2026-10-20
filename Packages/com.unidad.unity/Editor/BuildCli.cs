using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Unidad.Adapter.Editor
{
    /// <summary>What <see cref="BuildCli.Build"/> builds.</summary>
    public sealed record BuildCliOptions(string OutputFolder, string ExeName, IReadOnlyList<string> Scenes, bool Development, bool Release = false);

    /// <summary>What a build produced; also written to <c>&lt;out&gt;/build-report.txt</c>.</summary>
    public sealed record BuildCliResult(bool Succeeded, long TotalBytes, int Errors, int Warnings, double Seconds, string ExePath);

    public static class BuildCli
    {
        public const string ReportFile = "build-report.txt";

        /// <summary>A StandaloneWindows64 CoreCLR player; writes <c>&lt;out&gt;/build-report.txt</c>.</summary>
        public static BuildCliResult Build(BuildCliOptions options)
        {
            Directory.CreateDirectory(options.OutputFolder);
            var exePath = Path.Combine(options.OutputFolder, options.ExeName).Replace('\\', '/');
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = options.Scenes.ToArray(),
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = options.Development && !options.Release ? BuildOptions.Development : BuildOptions.None,
            });
            var summary = report.summary;
            var result = new BuildCliResult(summary.result == BuildResult.Succeeded, (long)summary.totalSize, summary.totalErrors,
                summary.totalWarnings, summary.totalTime.TotalSeconds, exePath);
            File.WriteAllText(Path.Combine(options.OutputFolder, ReportFile), string.Join("\n", new[]
            {
                $"result={summary.result}",
                $"totalBytes={result.TotalBytes}",
                $"errors={result.Errors}",
                $"warnings={result.Warnings}",
                $"seconds={result.Seconds.ToString("0.0", CultureInfo.InvariantCulture)}",
                $"exe={exePath}",
            }) + "\n");
            return result;
        }

        /// <summary>
        /// Batch entry point. Arguments:
        /// <c>-buildOut &lt;dir&gt;</c> (required) the output folder;
        /// <c>-development</c> a development player;
        /// <c>-scene &lt;path&gt;</c> (repeatable) the scenes, default the enabled build-settings scenes;
        /// <c>-release</c> a release player.
        /// Writes <c>&lt;dir&gt;/build-report.txt</c>; exit 0 when the build succeeded, 1 when it failed.
        /// </summary>
        public static void Player() => BatchExit.Run("BuildCli.Player", () =>
        {
            var args = new CliArgs(Environment.GetCommandLineArgs());
            var output = args.Required("-buildOut");
            var scenes = args.Values("-scene");
            if (scenes.Count == 0)
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Count == 0) return "no scenes to build";
            var result = Build(new BuildCliOptions(output, PlayerSettings.productName + ".exe", scenes, args.Flag("-development"), args.Flag("-release")));
            return result.Succeeded ? null : $"build failed with {result.Errors} error(s); see {ReportFile}";
        });
    }
}
