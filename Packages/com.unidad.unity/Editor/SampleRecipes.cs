using System;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unidad.Adapter.Editor
{
    /// <summary>
    /// Sandbox recipes for the Movement sample: two separate <c>-executeMethod</c> runs, because the imported code only
    /// compiles in the next editor run.
    /// </summary>
    public static class SampleRecipes
    {
        public const string PackageName = "com.unidad.unity";
        public const string SampleName = "Movement";
        public const string ScenePath = "Assets/Scenes/Movement.unity";
        public const string BootstrapType = "Unidad.Samples.Movement.View.MovementSampleBootstrap, Unidad.Samples.Movement.View";

        /// <summary>Imports the "Movement" sample into Assets/Samples, unless a copy already compiles (the Sandbox's prepare step puts one there).</summary>
        public static void ImportMovement() => BatchExit.Run("SampleRecipes.ImportMovement", () =>
        {
            // A second copy would declare the sample's assemblies twice.
            if (Type.GetType(BootstrapType) != null)
            {
                Debug.Log($"UNIDAD SampleRecipes.ImportMovement: {BootstrapType} already compiles; not imported again");
                return null;
            }
            var sample = Sample.FindByPackage(PackageName, null).FirstOrDefault(s => s.displayName == SampleName);
            if (string.IsNullOrEmpty(sample.displayName)) return $"no sample '{SampleName}' in {PackageName}";
            return sample.Import(Sample.ImportOptions.OverridePreviousImports) ? null : "Sample.Import returned false";
        });

        /// <summary>Writes Assets/Scenes/Movement.unity with one "[Boot]" object and adds it to the build settings.</summary>
        public static void BuildMovementScene() => BatchExit.Run("SampleRecipes.BuildMovementScene", () =>
        {
            var bootstrap = Type.GetType(BootstrapType);
            if (bootstrap == null) return "sample not compiled";

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("[Boot]").AddComponent(bootstrap);
            EditorFolders.Ensure(ScenePath.Substring(0, ScenePath.LastIndexOf('/')));
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) return $"could not save {ScenePath}";
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            return null;
        });
    }
}
