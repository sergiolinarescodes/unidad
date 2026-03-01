using System;
using Unidad.Core.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.Editor.Editor.ScenarioBrowser
{
    /// <summary>
    /// Standalone runner window for executing scenarios and viewing results.
    /// Can be used independently or alongside the browser.
    /// </summary>
    public class ScenarioRunnerWindow : EditorWindow
    {
        private ITestScenario _scenario;
        private ScenarioParameterOverrides _overrides;
        private ScenarioVerificationResult _lastResult;
        private VisualElement _controlsContainer;
        private ScrollView _resultsContainer;

        [MenuItem("Window/Unidad/Scenario Runner")]
        public static void ShowWindow()
        {
            var window = GetWindow<ScenarioRunnerWindow>();
            window.titleContent = new GUIContent("Scenario Runner");
            window.minSize = new Vector2(350, 300);
        }

        /// <summary>Set the scenario to run.</summary>
        public void SetScenario(ITestScenario scenario, ScenarioParameterOverrides overrides)
        {
            _scenario = scenario;
            _overrides = overrides;
            _lastResult = null;
            Rebuild();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingTop = 8;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;

            _controlsContainer = new VisualElement();
            root.Add(_controlsContainer);

            _resultsContainer = new ScrollView(ScrollViewMode.Vertical);
            _resultsContainer.style.flexGrow = 1;
            _resultsContainer.style.marginTop = 8;
            root.Add(_resultsContainer);

            Rebuild();
        }

        private void Rebuild()
        {
            if (_controlsContainer == null) return;
            _controlsContainer.Clear();

            if (_scenario == null)
            {
                _controlsContainer.Add(new Label("No scenario selected."));
                return;
            }

            var nameLabel = new Label(_scenario.Definition.Name);
            nameLabel.style.fontSize = 14;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.marginBottom = 8;
            _controlsContainer.Add(nameLabel);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;

            var runButton = new Button(Run) { text = "Run" };
            runButton.style.width = 80;
            runButton.style.height = 28;
            buttonRow.Add(runButton);

            var rerunButton = new Button(Run) { text = "Re-run" };
            rerunButton.style.width = 80;
            rerunButton.style.height = 28;
            buttonRow.Add(rerunButton);

            _controlsContainer.Add(buttonRow);

            if (_lastResult != null)
                ShowResults();
        }

        private void Run()
        {
            if (_scenario == null) return;

            _resultsContainer.Clear();

            try
            {
                if (_overrides != null)
                    _scenario.Execute(_overrides);
                else
                    _scenario.Execute();

                _lastResult = _scenario.Verify();
            }
            catch (Exception ex)
            {
                _lastResult = ScenarioVerificationResult.Fail("Execution", ex.Message);
            }

            ShowResults();
        }

        private void ShowResults()
        {
            _resultsContainer.Clear();

            if (_lastResult == null) return;

            var header = new Label("Results:");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 4;
            _resultsContainer.Add(header);

            foreach (var check in _lastResult.Checks)
            {
                var icon = check.Passed ? "PASS" : "FAIL";
                var label = new Label($"  {icon}: {check.Name}" +
                    (check.Message != null ? $" - {check.Message}" : ""));
                label.style.color = check.Passed
                    ? new Color(0.3f, 0.8f, 0.3f)
                    : new Color(0.9f, 0.3f, 0.3f);
                _resultsContainer.Add(label);
            }

            var summary = new Label(
                $"\nTotal: {_lastResult.PassedCount}/{_lastResult.TotalCount} passed");
            summary.style.unityFontStyleAndWeight = FontStyle.Bold;
            summary.style.color = _lastResult.Success
                ? new Color(0.3f, 0.8f, 0.3f)
                : new Color(0.9f, 0.3f, 0.3f);
            _resultsContainer.Add(summary);
        }
    }
}
