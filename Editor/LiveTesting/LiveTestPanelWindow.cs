using Unidad.Core.LiveTesting;
using Unidad.Core.Testing;
using UnityEditor;
using UnityEngine;

namespace Unidad.Core.Editor.LiveTesting
{
    /// <summary>
    /// Manual control panel for the active Live MCP Test — the editor-button half of
    /// the workflow (the picoCAD-style buttons). Every action button calls the SAME
    /// <see cref="LiveTestAction.Invoke"/> delegate the MCP tools call, so what the
    /// user clicks and what Claude triggers are identical.
    /// </summary>
    public sealed class LiveTestPanelWindow : EditorWindow
    {
        private ScenarioVerificationResult _lastResult;
        private Vector2 _scroll;

        [MenuItem("Window/Unidad/Live Test Panel")]
        public static void Open() => GetWindow<LiveTestPanelWindow>("Live Test");

        private void OnEnable() => LiveTestRegistry.ActiveChanged += Repaint;
        private void OnDisable() => LiveTestRegistry.ActiveChanged -= Repaint;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Live MCP Test Panel", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode in a live-test scene to drive actions.", MessageType.Info);
                return;
            }

            var scene = LiveTestRegistry.Active;
            if (scene == null)
            {
                EditorGUILayout.HelpBox("No active live test registered in the running scene.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField($"{scene.Name}  ({scene.Id})", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            foreach (var action in scene.Actions)
                if (GUILayout.Button($"▶ {action.Name}", GUILayout.Height(26)))
                    action.Invoke(LiveTestArgs.Empty);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Step 1")) LiveTestPlanDriver.Step(1);
                if (GUILayout.Button("Step 10")) LiveTestPlanDriver.Step(10);
                if (GUILayout.Button("Step 50")) LiveTestPlanDriver.Step(50);
            }

            if (GUILayout.Button("▶▶ Run Scripted Plan", GUILayout.Height(30)))
                _lastResult = LiveTestPlanDriver.RunPlan(scene);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Probes (live)", EditorStyles.boldLabel);
            foreach (var probe in scene.Probes)
                EditorGUILayout.LabelField(probe.Id, probe.Read().ToString());

            if (_lastResult != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(
                    $"Plan result: {_lastResult.PassedCount}/{_lastResult.TotalCount} passed",
                    EditorStyles.boldLabel);
                foreach (var check in _lastResult.Checks)
                {
                    var previous = GUI.color;
                    GUI.color = check.Passed ? Color.green : Color.red;
                    EditorGUILayout.LabelField((check.Passed ? "PASS  " : "FAIL  ") + check.Name, check.Message ?? string.Empty);
                    GUI.color = previous;
                }
            }

            EditorGUILayout.EndScrollView();

            Repaint(); // keep the live probe read-out refreshing
        }
    }
}
