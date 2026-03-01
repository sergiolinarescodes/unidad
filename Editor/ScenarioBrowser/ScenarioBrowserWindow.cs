using System;
using System.Collections.Generic;
using System.Linq;
using Unidad.Core.Bootstrap;
using Unidad.Core.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.Editor.Editor.ScenarioBrowser
{
    /// <summary>
    /// Main Editor Window: lists all discovered scenarios grouped by system.
    /// Allows searching, selecting, and launching scenario execution.
    /// </summary>
    public class ScenarioBrowserWindow : EditorWindow
    {
        private const string UIToolkitFolder = "Assets/UI Toolkit";
        private const string PanelSettingsPath = "Assets/UI Toolkit/ScenarioPanelSettings.asset";

        private TextField _searchField;
        private ScrollView _scenarioList;
        private VisualElement _inspectorContainer;
        private VisualElement _resultsContainer;

        private List<ScenarioGroup> _allGroups = new();
        private ITestScenario _selectedScenario;
        private ITestScenario _lastExecutedScenario;
        private ScenarioParameterOverrides _currentOverrides;
        private ScenarioVerificationResult _lastResult;
        private bool _pendingRunAfterPlayMode;

        [MenuItem("Window/Unidad/Scenario Browser")]
        public static void ShowWindow()
        {
            var window = GetWindow<ScenarioBrowserWindow>();
            window.titleContent = new GUIContent("Scenario Browser");
            window.minSize = new Vector2(400, 600);
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && _pendingRunAfterPlayMode)
            {
                _pendingRunAfterPlayMode = false;
                // Delay one frame so Play Mode is fully initialized
                EditorApplication.delayCall += ExecuteSelectedScenario;
            }
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;

            // Search bar
            _searchField = new TextField("Search");
            _searchField.RegisterValueChangedCallback(evt => FilterScenarios(evt.newValue));
            _searchField.style.marginBottom = 4;
            _searchField.style.marginTop = 4;
            _searchField.style.marginLeft = 4;
            _searchField.style.marginRight = 4;
            root.Add(_searchField);

            // Toolbar
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.marginBottom = 4;
            toolbar.style.marginLeft = 4;

            var refreshButton = new Button(RefreshScenarios) { text = "Refresh" };
            toolbar.Add(refreshButton);

            root.Add(toolbar);

            // Main split: list on left, inspector+results on right
            var splitView = new TwoPaneSplitView(0, 200, TwoPaneSplitViewOrientation.Horizontal);

            // Left panel: scenario list
            _scenarioList = new ScrollView(ScrollViewMode.Vertical);
            _scenarioList.style.minWidth = 180;
            splitView.Add(_scenarioList);

            // Right panel: inspector + results
            var rightPanel = new VisualElement();
            rightPanel.style.flexGrow = 1;

            _inspectorContainer = new VisualElement();
            _inspectorContainer.style.flexGrow = 1;
            _inspectorContainer.style.borderBottomWidth = 1;
            _inspectorContainer.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
            rightPanel.Add(_inspectorContainer);

            _resultsContainer = new VisualElement();
            _resultsContainer.style.minHeight = 100;
            _resultsContainer.style.paddingTop = 4;
            _resultsContainer.style.paddingLeft = 4;
            rightPanel.Add(_resultsContainer);

            splitView.Add(rightPanel);
            root.Add(splitView);

            RefreshScenarios();
        }

        private void RefreshScenarios()
        {
            _allGroups.Clear();
            _scenarioList.Clear();

            var groups = new Dictionary<string, ScenarioGroup>();

            foreach (var installerType in InstallerDiscovery.FindInstallerTypes())
            {
                var installer = InstallerDiscovery.CreateInstaller(installerType);
                if (installer == null) continue;

                try
                {
                    var factory = installer.CreateTestFactory();
                    if (factory == null) continue;

                    var groupName = installerType.Name.Replace("Installer", "");
                    var scenarios = factory.GetScenarios().ToList();

                    if (scenarios.Count > 0)
                    {
                        groups[groupName] = new ScenarioGroup
                        {
                            Name = groupName,
                            InstallerType = installerType,
                            Scenarios = scenarios
                        };
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ScenarioBrowser] Failed to load {installerType.Name}: {ex.Message}");
                }
            }

            _allGroups = groups.Values.OrderBy(g => g.Name).ToList();
            RebuildList(_allGroups);
        }

        private void RebuildList(IEnumerable<ScenarioGroup> groups)
        {
            _scenarioList.Clear();

            foreach (var group in groups)
            {
                var foldout = new Foldout { text = $"{group.Name} ({group.Scenarios.Count})", value = true };
                foldout.style.marginLeft = 4;

                foreach (var scenario in group.Scenarios)
                {
                    var button = new Button(() => SelectScenario(scenario))
                    {
                        text = scenario.Definition.Name
                    };
                    button.style.unityTextAlign = TextAnchor.MiddleLeft;
                    button.style.marginLeft = 8;
                    foldout.Add(button);
                }

                _scenarioList.Add(foldout);
            }
        }

        private void FilterScenarios(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                RebuildList(_allGroups);
                return;
            }

            var lower = filter.ToLowerInvariant();
            var filtered = _allGroups
                .Select(g => new ScenarioGroup
                {
                    Name = g.Name,
                    InstallerType = g.InstallerType,
                    Scenarios = g.Scenarios
                        .Where(s =>
                            s.Definition.Name.ToLowerInvariant().Contains(lower) ||
                            s.Definition.Id.ToLowerInvariant().Contains(lower) ||
                            g.Name.ToLowerInvariant().Contains(lower))
                        .ToList()
                })
                .Where(g => g.Scenarios.Count > 0);

            RebuildList(filtered);
        }

        private void SelectScenario(ITestScenario scenario)
        {
            _selectedScenario = scenario;
            _currentOverrides = new ScenarioParameterOverrides();
            _lastResult = null;

            BuildInspector();
            ClearResults();
        }

        private void BuildInspector()
        {
            _inspectorContainer.Clear();

            if (_selectedScenario == null) return;

            var def = _selectedScenario.Definition;

            // Header
            var title = new Label(def.Name);
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4;
            title.style.marginLeft = 8;
            title.style.marginTop = 8;
            _inspectorContainer.Add(title);

            var desc = new Label(def.Description);
            desc.style.marginBottom = 8;
            desc.style.marginLeft = 8;
            desc.style.whiteSpace = WhiteSpace.Normal;
            _inspectorContainer.Add(desc);

            // Parameters
            if (def.Parameters != null && def.Parameters.Length > 0)
            {
                var paramsLabel = new Label("Parameters:");
                paramsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                paramsLabel.style.marginLeft = 8;
                paramsLabel.style.marginBottom = 4;
                _inspectorContainer.Add(paramsLabel);

                foreach (var param in def.Parameters)
                {
                    var field = ScenarioParameterDrawer.CreateField(param, _currentOverrides);
                    field.style.marginLeft = 12;
                    field.style.marginBottom = 2;
                    _inspectorContainer.Add(field);
                }
            }

            // Action buttons
            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginTop = 8;
            buttonRow.style.marginLeft = 8;

            var runButton = new Button(RunScenario) { text = "Run" };
            runButton.style.width = 80;
            buttonRow.Add(runButton);

            var rerunButton = new Button(RunScenario) { text = "Re-run" };
            rerunButton.style.width = 80;
            buttonRow.Add(rerunButton);

            var resetButton = new Button(ResetDefaults) { text = "Reset Defaults" };
            resetButton.style.width = 100;
            buttonRow.Add(resetButton);

            _inspectorContainer.Add(buttonRow);
        }

        private void RunScenario()
        {
            if (_selectedScenario == null) return;

            // Enter Play Mode if not already — text animations require Time.time to advance
            if (!EditorApplication.isPlaying)
            {
                _pendingRunAfterPlayMode = true;
                EditorApplication.isPlaying = true;
                return;
            }

            ExecuteSelectedScenario();
        }

        private void ExecuteSelectedScenario()
        {
            EnsurePanelSettings();
            EnsureStyleSheets();

            // Clean up previous scenario before running a new one
            if (_lastExecutedScenario != null && _lastExecutedScenario != _selectedScenario)
                _lastExecutedScenario.Reset();

            try
            {
                _selectedScenario.Execute(_currentOverrides);
                _lastExecutedScenario = _selectedScenario;
                _lastResult = _selectedScenario.Verify();
                ShowResults();
            }
            catch (Exception ex)
            {
                _lastResult = ScenarioVerificationResult.Fail("Execution", ex.Message);
                ShowResults();
            }
        }

        private void ResetDefaults()
        {
            _currentOverrides = new ScenarioParameterOverrides();
            BuildInspector();
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

            var summary = new Label($"Total: {_lastResult.PassedCount}/{_lastResult.TotalCount} passed");
            summary.style.marginTop = 4;
            summary.style.unityFontStyleAndWeight = FontStyle.Bold;
            _resultsContainer.Add(summary);
        }

        private void ClearResults()
        {
            _resultsContainer.Clear();
        }

        /// <summary>
        /// Finds or creates a real PanelSettings asset on disk.
        /// In-memory ScriptableObject.CreateInstance does not work — Unity requires real assets.
        /// Tries to find Unity's default runtime ThemeStyleSheet so text has a font to render with.
        /// Falls back to no theme (DataDrivenScenario sets a font on root as safety net).
        /// </summary>
        private static void EnsurePanelSettings()
        {
            if (DataDrivenScenario.SharedPanelSettings != null)
                return;

            var defaultTheme = FindDefaultRuntimeTheme();

            // Try loading our known asset first
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null)
            {
                // Assign the real default theme (or clear a broken custom one)
                if (existing.themeStyleSheet != defaultTheme)
                {
                    existing.themeStyleSheet = defaultTheme;
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssetIfDirty(existing);
                }

                DataDrivenScenario.SharedPanelSettings = existing;
                return;
            }

            // Create folder if needed
            if (!AssetDatabase.IsValidFolder(UIToolkitFolder))
                AssetDatabase.CreateFolder("Assets", "UI Toolkit");

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.themeStyleSheet = defaultTheme;
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            AssetDatabase.SaveAssets();

            DataDrivenScenario.SharedPanelSettings =
                AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);

            Debug.Log($"[ScenarioBrowser] Created PanelSettings at {PanelSettingsPath}" +
                      (defaultTheme != null ? $" with theme: {AssetDatabase.GetAssetPath(defaultTheme)}" : " (no theme found — using fallback font)"));
        }

        private static ThemeStyleSheet FindDefaultRuntimeTheme()
        {
            // Search for Unity's built-in runtime ThemeStyleSheet assets
            var guids = AssetDatabase.FindAssets("t:ThemeStyleSheet");
            ThemeStyleSheet fallback = null;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                // Skip our own previously created empty theme
                if (path.Contains("ScenarioTheme")) continue;

                // Prefer Unity's default runtime theme
                if (path.Contains("UnityDefaultRuntimeTheme") ||
                    path.Contains("DefaultRuntimeTheme"))
                {
                    var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);
                    if (theme != null)
                    {
                        Debug.Log($"[ScenarioBrowser] Found Unity runtime theme at: {path}");
                        return theme;
                    }
                }

                // Keep track of any theme as fallback
                if (fallback == null)
                    fallback = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);
            }

            if (fallback != null)
                Debug.Log($"[ScenarioBrowser] Using fallback theme: {AssetDatabase.GetAssetPath(fallback)}");
            else
                Debug.LogWarning("[ScenarioBrowser] No ThemeStyleSheet found — text will use fallback font from DataDrivenScenario");

            return fallback;
        }

        private static void EnsureStyleSheets()
        {
            if (DataDrivenScenario.SharedStyleSheets != null)
                return;

            var themeSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.unidad.core/Runtime/UI/DesignSystem/UnidadTheme.uss");
            var componentSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.unidad.core/Runtime/UI/DesignSystem/UnidadComponents.uss");

            DataDrivenScenario.SharedStyleSheets = new[] { themeSheet, componentSheet };
        }

        private class ScenarioGroup
        {
            public string Name;
            public Type InstallerType;
            public List<ITestScenario> Scenarios;
        }
    }
}
