using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Unidad.Adapter.Editor
{
    /// <summary>
    /// Batch entry points of <c>tools/unity-lane.ps1</c> (<c>-executeMethod</c>). Each logs exactly one line
    /// <c>UNIDAD LaneCli.&lt;Method&gt; ok|fail &lt;reason&gt;</c> and exits through <c>EditorApplication.Exit</c>.
    /// </summary>
    public static class LaneCli
    {
        /// <summary>Exit 1 when <c>EditorUtility.scriptCompilationFailed</c>, else 0. No arguments.</summary>
        public static void Compile() => BatchExit.Run("LaneCli.Compile", CompileFailure);

        /// <summary><c>AssetDatabase.Refresh(ForceSynchronousImport)</c>, then <see cref="Compile"/>'s check; exit 0/1. No arguments.</summary>
        public static void Import() => BatchExit.Run("LaneCli.Import", () =>
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return CompileFailure();
        });

        /// <summary>
        /// Opens a scene and enters Play mode (the interactive testbed; settings travel in <c>UNIDAD_LAUNCH</c>).
        /// Arguments: <c>-unidadScene &lt;Assets/...unity&gt;</c> (required).
        /// </summary>
        public static void PlayScene()
        {
            try
            {
                var scene = new CliArgs(Environment.GetCommandLineArgs()).Required("-unidadScene");
                EditorSceneManager.OpenScene(scene);
                EditorApplication.EnterPlaymode();
                Debug.Log($"UNIDAD LaneCli.PlayScene ok {scene}");
            }
            catch (Exception e)
            {
                BatchExit.Finish("LaneCli.PlayScene", e.Message);
            }
        }

        private static string CompileFailure() => EditorUtility.scriptCompilationFailed ? "scripts have compile errors" : null;
    }

    /// <summary>The one-line result and exit code every batch entry ends with.</summary>
    internal static class BatchExit
    {
        /// <param name="work">Returns null on success, else the failure reason; a throw is a failure too.</param>
        public static void Run(string what, Func<string> work)
        {
            string failure;
            try
            {
                failure = work();
            }
            catch (Exception e)
            {
                failure = $"{e.GetType().Name}: {e.Message}";
            }

            Finish(what, failure);
        }

        /// <summary>Logs <c>UNIDAD &lt;what&gt; ok|fail &lt;reason&gt;</c>; in batch mode exits 0 or 1.</summary>
        public static void Finish(string what, string failure)
        {
            Debug.Log(failure == null ? $"UNIDAD {what} ok" : $"UNIDAD {what} fail {failure}");
            if (UnityEngine.Application.isBatchMode) EditorApplication.Exit(failure == null ? 0 : 1);
        }
    }
}
