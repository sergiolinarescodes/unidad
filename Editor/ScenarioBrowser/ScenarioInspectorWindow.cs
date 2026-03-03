using Unidad.Core.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.Editor.ScenarioBrowser
{
    /// <summary>
    /// Standalone inspector window for a selected scenario's parameters.
    /// Can be used independently or alongside the browser.
    /// </summary>
    public class ScenarioInspectorWindow : EditorWindow
    {
        private ITestScenario _scenario;
        private ScenarioParameterOverrides _overrides;
        private ScrollView _content;

        [MenuItem("Window/Unidad/Scenario Inspector")]
        public static void ShowWindow()
        {
            var window = GetWindow<ScenarioInspectorWindow>();
            window.titleContent = new GUIContent("Scenario Inspector");
            window.minSize = new Vector2(300, 400);
        }

        /// <summary>Set the scenario to inspect (called from browser or code).</summary>
        public void Inspect(ITestScenario scenario, ScenarioParameterOverrides overrides)
        {
            _scenario = scenario;
            _overrides = overrides;
            Rebuild();
        }

        public ScenarioParameterOverrides CurrentOverrides => _overrides;

        private void CreateGUI()
        {
            _content = new ScrollView(ScrollViewMode.Vertical);
            _content.style.paddingTop = 8;
            _content.style.paddingLeft = 8;
            _content.style.paddingRight = 8;
            rootVisualElement.Add(_content);

            if (_scenario != null)
                Rebuild();
            else
                _content.Add(new Label("No scenario selected. Use the Scenario Browser to select one."));
        }

        private void Rebuild()
        {
            if (_content == null) return;
            _content.Clear();

            if (_scenario == null)
            {
                _content.Add(new Label("No scenario selected."));
                return;
            }

            var def = _scenario.Definition;

            var title = new Label(def.Name);
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4;
            _content.Add(title);

            var idLabel = new Label($"ID: {def.Id}");
            idLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            idLabel.style.marginBottom = 4;
            _content.Add(idLabel);

            var desc = new Label(def.Description);
            desc.style.whiteSpace = WhiteSpace.Normal;
            desc.style.marginBottom = 8;
            _content.Add(desc);

            if (def.Parameters == null || def.Parameters.Length == 0)
            {
                _content.Add(new Label("No editable parameters."));
                return;
            }

            var header = new Label("Parameters:");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 4;
            _content.Add(header);

            foreach (var param in def.Parameters)
            {
                var field = ScenarioParameterDrawer.CreateField(param, _overrides);
                field.style.marginBottom = 4;
                _content.Add(field);
            }
        }
    }
}
