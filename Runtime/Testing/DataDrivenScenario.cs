using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.Testing
{
    /// <summary>
    /// Base class for data-driven test scenarios.
    /// Provides automatic scene setup with a UIDocument + PanelSettings,
    /// so subclasses just add VisualElements to RootVisualElement.
    /// Handles full cleanup on re-run automatically.
    /// </summary>
    public abstract class DataDrivenScenario : ITestScenario
    {
        public TestScenarioDefinition Definition { get; }

        private ScenarioParameterOverrides _currentOverrides;
        private GameObject _sceneRoot;
        private UIDocument _document;

        /// <summary>
        /// Set this from Editor code before running scenarios.
        /// Must be a real asset (saved to disk) — in-memory PanelSettings do not render.
        /// </summary>
        public static PanelSettings SharedPanelSettings { get; set; }

        /// <summary>
        /// Stylesheets to attach to every scenario's root VisualElement.
        /// Set from Editor code or test setup to load design tokens and component styles.
        /// </summary>
        public static StyleSheet[] SharedStyleSheets { get; set; }

        protected DataDrivenScenario(TestScenarioDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        /// <summary>
        /// The root GameObject for this scenario. Created automatically, destroyed on cleanup.
        /// </summary>
        protected GameObject SceneRoot
        {
            get
            {
                if (_sceneRoot == null)
                    _sceneRoot = new GameObject($"[Scenario] {Definition.Name}");
                return _sceneRoot;
            }
        }

        /// <summary>
        /// A ready-to-use UIDocument attached to SceneRoot with a valid PanelSettings.
        /// Created on first access. PanelSettings is assigned BEFORE OnEnable runs
        /// so the panel and rootVisualElement are created in a single pass.
        /// </summary>
        protected UIDocument Document
        {
            get
            {
                if (_document == null)
                {
                    // Disable the GameObject so UIDocument.OnEnable does NOT fire yet
                    SceneRoot.SetActive(false);

                    _document = SceneRoot.AddComponent<UIDocument>();
                    if (SharedPanelSettings != null)
                        _document.panelSettings = SharedPanelSettings;

                    // Now enable — OnEnable fires with panelSettings already assigned,
                    // so the panel + rootVisualElement are created properly in one go
                    SceneRoot.SetActive(true);

                    // Ensure root stretches to fill the full panel area
                    var root = _document.rootVisualElement;
                    if (root != null)
                    {
                        root.style.flexGrow = 1;

                        // Attach shared stylesheets (design tokens + component styles)
                        if (SharedStyleSheets != null)
                        {
                            foreach (var sheet in SharedStyleSheets)
                            {
                                if (sheet != null && !root.styleSheets.Contains(sheet))
                                    root.styleSheets.Add(sheet);
                            }
                        }

                        // Runtime panels without a ThemeStyleSheet have no default font,
                        // which makes ALL text invisible. Set a fallback font on the root
                        // so every child element can inherit it and render text.
                        var builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                        if (builtinFont == null)
                            builtinFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
                        if (builtinFont != null)
                            root.style.unityFontDefinition = FontDefinition.FromFont(builtinFont);
                    }
                }
                return _document;
            }
        }

        /// <summary>
        /// Shortcut to Document.rootVisualElement — the main entry point for building UI.
        /// </summary>
        protected VisualElement RootVisualElement => Document.rootVisualElement;

        public void Execute()
        {
            _currentOverrides = new ScenarioParameterOverrides();
            Cleanup();
            ExecuteInternal(_currentOverrides);
        }

        public void Execute(ScenarioParameterOverrides overrides)
        {
            _currentOverrides = overrides ?? new ScenarioParameterOverrides();
            Cleanup();
            ExecuteInternal(_currentOverrides);
        }

        public ScenarioVerificationResult Verify()
        {
            return VerifyInternal(_currentOverrides ?? new ScenarioParameterOverrides());
        }

        public void Reset() => Cleanup();

        protected abstract void ExecuteInternal(ScenarioParameterOverrides overrides);
        protected abstract ScenarioVerificationResult VerifyInternal(ScenarioParameterOverrides overrides);

        /// <summary>
        /// Resolve a parameter value from overrides or definition defaults.
        /// </summary>
        protected T ResolveParam<T>(ScenarioParameterOverrides overrides, string paramName)
        {
            foreach (var param in Definition.Parameters)
            {
                if (param.Name == paramName)
                    return overrides.Resolve<T>(param);
            }
            return default;
        }

        /// <summary>
        /// Override to dispose custom resources. Called before each re-run.
        /// </summary>
        protected virtual void OnCleanup() { }

        private void Cleanup()
        {
            OnCleanup();
            _document = null;
            if (_sceneRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(_sceneRoot);
                _sceneRoot = null;
            }
        }
    }
}
