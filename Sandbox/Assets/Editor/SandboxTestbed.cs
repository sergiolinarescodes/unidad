using System.Linq;
using Unidad.Adapter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unidad.Sandbox.Editor
{
    /// <summary>The testbed door (<c>tools/testbed.ps1</c>): the Movement scene built in memory, then Play. Settings arrive in UNIDAD_LAUNCH.</summary>
    public static class Testbed
    {
        public static void Play()
        {
            // Scene first: NewScene unloads unreferenced assets.
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var boot = TypeCache.GetTypesDerivedFrom<UnidadBootstrap>()
                .Where(t => !t.IsAbstract && (t.Namespace ?? "").StartsWith("Unidad.Samples"))
                .OrderBy(t => t.FullName)
                .FirstOrDefault();
            if (boot == null)
            {
                Debug.LogWarning("[testbed] no sample bootstrap compiled (is the Movement sample in Samples~?)");
                return;
            }
            new GameObject("[Boot]").AddComponent(boot);
            EditorApplication.EnterPlaymode();
        }
    }
}
