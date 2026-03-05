using System;
using TextAnimationsForUIToolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.Components
{
    [UxmlElement]
    public partial class AnimatedScriptField : VisualElement
    {
        private const string RootClass = "animated-script-field";
        private const string InputClass = "animated-script-field__input";
        private const string AnimatingModifier = "animated-script-field--animating";

        private readonly AnimatedLabel _display;
        private readonly TextField _input;
        private TextAnimationSettings _ownedSettings;
        private bool _isAnimating;

        public string value
        {
            get => _input.value;
            set => _input.value = value;
        }

        public bool isReadOnly
        {
            get => _input.isReadOnly;
            set => _input.isReadOnly = value;
        }

        public bool multiline
        {
            get => _input.multiline;
            set => _input.multiline = value;
        }

        public int cursorIndex => _input.cursorIndex;
        public int selectIndex => _input.selectIndex;

        public AnimatedScriptField()
        {
            AddToClassList(RootClass);

            _display = new AnimatedLabel
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = 0,
                    top = 0,
                    right = 0,
                    bottom = 0,
                    whiteSpace = WhiteSpace.PreWrap,
                    display = DisplayStyle.None
                }
            };

            _input = new TextField
            {
                multiline = true,
                style =
                {
                    flexGrow = 1
                }
            };
            _input.AddToClassList(InputClass);

            Add(_display);
            Add(_input);

            _input.RegisterCallback<FocusInEvent>(_ => CancelAnimation());
        }

        public void RegisterValueChangedCallback(EventCallback<ChangeEvent<string>> callback)
        {
            _input.RegisterValueChangedCallback(callback);
        }

        public void UnregisterValueChangedCallback(EventCallback<ChangeEvent<string>> callback)
        {
            _input.UnregisterValueChangedCallback(callback);
        }

        public void InsertAtCaret(string text)
        {
            var current = _input.value ?? "";
            var cursor = _input.cursorIndex;
            if (cursor < 0 || cursor > current.Length)
                cursor = current.Length;

            var newValue = current.Insert(cursor, text);
            _input.value = newValue;
            _input.SelectRange(cursor + text.Length, cursor + text.Length);
        }

        public void SelectRange(int cursorPos, int selectPos)
        {
            _input.SelectRange(cursorPos, selectPos);
        }

        public new void Focus()
        {
            _input.Focus();
        }

        public void SetFont(Font font)
        {
            if (font == null) return;
            _display.style.unityFont = font;
            _input.style.unityFont = font;

            var inputElement = _input.Q<VisualElement>("unity-text-input");
            if (inputElement != null)
                inputElement.style.unityFont = font;

            var textElement = _input.Q<TextElement>();
            if (textElement != null)
                textElement.style.unityFont = font;
        }

        public void PlayAnimation(string animatedText, TextAnimationSettings settings)
        {
            CancelAnimation();

            if (string.IsNullOrWhiteSpace(animatedText))
                return;

            _isAnimating = true;
            AddToClassList(AnimatingModifier);

            // Create owned settings copy so we can destroy it on cleanup
            _ownedSettings = ScriptableObject.CreateInstance<TextAnimationSettings>();
            _ownedSettings.name = "AnimatedScriptFieldPreset";
            _ownedSettings.enableTextAppearance = settings != null && settings.enableTextAppearance;
            _ownedSettings.enableTextVanishing = settings != null && settings.enableTextVanishing;

            _display.settings = _ownedSettings;
            _display.style.display = DisplayStyle.Flex;
            _display.text = animatedText;
            _display.Play();
        }

        public void CancelAnimation()
        {
            if (!_isAnimating) return;

            _isAnimating = false;
            RemoveFromClassList(AnimatingModifier);

            _display.style.display = DisplayStyle.None;
            _display.text = "";

            if (_ownedSettings != null)
            {
                UnityEngine.Object.Destroy(_ownedSettings);
                _ownedSettings = null;
            }
        }
    }
}
