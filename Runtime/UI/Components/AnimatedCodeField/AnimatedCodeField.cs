using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.Components
{
    [UxmlElement]
    public partial class AnimatedCodeField : VisualElement
    {
        private const string RootClass = "animated-code-field";
        private const string LinesClass = "animated-code-field__lines";
        private const string LineClass = "animated-code-field__line";
        private const string CharClass = "animated-code-field__char";
        private const string CaretClass = "animated-code-field__caret";
        private const string SelectionClass = "animated-code-field__selection";
        private const string SelectionRectClass = "animated-code-field__selection-rect";
        private const string HiddenInputClass = "animated-code-field__hidden-input";

        private const long CaretBlinkMs = 530;

        private readonly VisualElement _selectionLayer;
        private readonly VisualElement _linesContainer;
        private readonly VisualElement _caret;
        private readonly TextField _hiddenInput;
        private readonly Label _refLabel;

        private readonly List<Label> _charLabels = new();
        private readonly List<int> _lineStartIndices = new();
        private readonly List<EventCallback<ChangeEvent<string>>> _valueChangedCallbacks = new();

        private float _charWidth;
        private float _lineHeight;
        private bool _metricsReady;
        private bool _caretVisible = true;
        private IVisualElementScheduledItem _caretBlink;
        private IVisualElementScheduledItem _animSchedule;
        private bool _pointerDown;
        private Font _font;

        // Animation state
        private ICharAnimation _currentAnim;
        private float _animStartTime;

        // Typing animation state (runs in parallel with opening animation)
        private VibrateAnimation _typingAnim;
        private float _typingAnimStartTime;
        private IVisualElementScheduledItem _typingAnimSchedule;
        private string _previousText = "";

        public float TypingAnimationAmplitude { get; set; }

        public bool IsAnimating => _currentAnim != null;

        public string value
        {
            get => _hiddenInput.value;
            set
            {
                _hiddenInput.SetValueWithoutNotify(value);
                _previousText = value ?? "";
                RebuildCharGrid();
                UpdateCaretPosition();
            }
        }

        public bool isReadOnly
        {
            get => _hiddenInput.isReadOnly;
            set => _hiddenInput.isReadOnly = value;
        }

        public bool multiline
        {
            get => _hiddenInput.multiline;
            set => _hiddenInput.multiline = value;
        }

        public int cursorIndex => _hiddenInput.cursorIndex;
        public int selectIndex => _hiddenInput.selectIndex;

        public IReadOnlyList<Label> CharLabels => _charLabels;
        public float CharWidth => _charWidth;
        public float LineHeight => _lineHeight;

        public AnimatedCodeField()
        {
            AddToClassList(RootClass);
            focusable = true;

            // Hidden TextField for keyboard input (behind everything)
            _hiddenInput = new TextField { multiline = true };
            _hiddenInput.AddToClassList(HiddenInputClass);
            Add(_hiddenInput);

            // Selection layer (behind text)
            _selectionLayer = new VisualElement();
            _selectionLayer.AddToClassList(SelectionClass);
            _selectionLayer.pickingMode = PickingMode.Ignore;
            _selectionLayer.style.position = Position.Absolute;
            _selectionLayer.style.left = 0;
            _selectionLayer.style.top = 0;
            _selectionLayer.style.right = 0;
            _selectionLayer.style.bottom = 0;
            Add(_selectionLayer);

            // Lines container (per-character Labels)
            _linesContainer = new VisualElement();
            _linesContainer.AddToClassList(LinesClass);
            _linesContainer.pickingMode = PickingMode.Ignore;
            Add(_linesContainer);

            // Custom caret (on top of everything)
            _caret = new VisualElement();
            _caret.AddToClassList(CaretClass);
            _caret.pickingMode = PickingMode.Ignore;
            _caret.style.display = DisplayStyle.None;
            _caret.style.position = Position.Absolute;
            _caret.style.width = 2;
            _caret.style.backgroundColor = new Color(0.9f, 0.92f, 0.88f);
            Add(_caret);

            // Permanent reference label for metrics (never destroyed by RebuildCharGrid)
            _refLabel = new Label("X")
            {
                pickingMode = PickingMode.Ignore
            };
            _refLabel.AddToClassList(CharClass);
            _refLabel.style.position = Position.Absolute;
            _refLabel.style.left = -9999;
            _refLabel.style.visibility = Visibility.Hidden;
            Add(_refLabel);

            _refLabel.RegisterCallback<GeometryChangedEvent>(e =>
            {
                Debug.Log($"[ACF] _refLabel GeometryChanged: oldRect={e.oldRect}, newRect={e.newRect}");
                TryMeasureMetrics();
            });

            // Wire up events
            _hiddenInput.RegisterValueChangedCallback(OnHiddenInputChanged);
            _hiddenInput.RegisterCallback<KeyDownEvent>(OnKeyDown);
            _hiddenInput.RegisterCallback<FocusInEvent>(OnInputFocusIn);

            RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);

            // Click-outside detection for ending edit mode
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

            // Initial empty grid
            RebuildCharGrid();
        }

        public void RegisterValueChangedCallback(EventCallback<ChangeEvent<string>> callback)
        {
            _valueChangedCallbacks.Add(callback);
            _hiddenInput.RegisterValueChangedCallback(callback);
        }

        public void UnregisterValueChangedCallback(EventCallback<ChangeEvent<string>> callback)
        {
            _valueChangedCallbacks.Remove(callback);
            _hiddenInput.UnregisterValueChangedCallback(callback);
        }

        public void InsertAtCaret(string text)
        {
            var current = _hiddenInput.value ?? "";
            var cursor = _hiddenInput.cursorIndex;
            if (cursor < 0 || cursor > current.Length)
                cursor = current.Length;

            var newValue = current.Insert(cursor, text);
            _hiddenInput.value = newValue;
            _hiddenInput.SelectRange(cursor + text.Length, cursor + text.Length);
        }

        public void SelectRange(int cursorPos, int selectPos)
        {
            _hiddenInput.SelectRange(cursorPos, selectPos);
        }

        public new void Focus()
        {
            _hiddenInput.Focus();
        }

        public void SetFont(Font font)
        {
            if (font == null) return;
            _font = font;
            Debug.Log($"[ACF] SetFont: {font.name}");

            _hiddenInput.style.unityFont = font;
            _hiddenInput.style.unityFontDefinition = FontDefinition.FromFont(font);
            var inputElement = _hiddenInput.Q<VisualElement>("unity-text-input");
            if (inputElement != null)
            {
                inputElement.style.unityFont = font;
                inputElement.style.unityFontDefinition = FontDefinition.FromFont(font);
            }

            _refLabel.style.unityFont = font;
            _refLabel.style.unityFontDefinition = FontDefinition.FromFont(font);

            // Font changed — force re-measure
            _metricsReady = false;
            RebuildCharGrid();
        }

        public void PlayAnimation(ICharAnimation animation)
        {
            CancelAnimation();

            if (animation == null || _charLabels.Count == 0) return;

            _currentAnim = animation;
            _animStartTime = Time.realtimeSinceStartup;
            _currentAnim.Initialize(_charLabels.Count);

            // Hide caret during animation
            _caret.style.display = DisplayStyle.None;

            _animSchedule = schedule.Execute(AnimationTick).Every(16);
        }

        public void CancelAnimation()
        {
            CancelTypingAnimation();

            if (_currentAnim == null) return;

            for (int i = 0; i < _charLabels.Count; i++)
                _currentAnim.Reset(_charLabels[i]);

            _currentAnim = null;

            if (_animSchedule != null)
            {
                _animSchedule.Pause();
                _animSchedule = null;
            }
        }

        // --- Private: Character Grid ---

        private void RebuildCharGrid()
        {
            _linesContainer.Clear();
            _charLabels.Clear();
            _lineStartIndices.Clear();

            var text = _hiddenInput.value ?? "";
            var lines = text.Split('\n');

            int index = 0;
            foreach (var line in lines)
            {
                _lineStartIndices.Add(index);
                var lineEl = new VisualElement();
                lineEl.AddToClassList(LineClass);
                lineEl.pickingMode = PickingMode.Ignore;

                if (line.Length == 0)
                {
                    // Empty line — force line height since space labels collapse
                    if (_metricsReady)
                        lineEl.style.minHeight = _lineHeight;
                }
                else
                {
                    foreach (var ch in line)
                    {
                        var label = CreateCharLabel(ch.ToString());
                        _charLabels.Add(label);
                        lineEl.Add(label);
                        index++;
                    }
                }

                _linesContainer.Add(lineEl);
                index++; // account for \n
            }

            TryMeasureMetrics();
        }

        private Label CreateCharLabel(string ch)
        {
            var label = new Label(ch)
            {
                pickingMode = PickingMode.Ignore
            };
            label.AddToClassList(CharClass);
            if (_font != null)
            {
                label.style.unityFont = _font;
                label.style.unityFontDefinition = FontDefinition.FromFont(_font);
            }
            // Spaces collapse to zero width in Labels — force width when metrics are known
            if (ch == " " && _metricsReady)
                label.style.width = _charWidth;
            return label;
        }

        // --- Private: Metrics ---

        private void TryMeasureMetrics()
        {
            var w = _refLabel.resolvedStyle.width;
            var h = _refLabel.resolvedStyle.height;

            Debug.Log($"[ACF] TryMeasureMetrics: refLabel w={w}, h={h}, metricsReady={_metricsReady}, refLabel.parent={_refLabel.parent?.name ?? "null"}, refLabel.panel={(_refLabel.panel != null ? "yes" : "null")}");

            if (float.IsNaN(w) || w <= 0 || float.IsNaN(h) || h <= 0)
            {
                Debug.Log("[ACF] TryMeasureMetrics: FAILED — invalid refLabel dimensions");
                return;
            }

            _charWidth = w;
            _lineHeight = h;
            _metricsReady = true;
            Debug.Log($"[ACF] TryMeasureMetrics: SUCCESS — charWidth={_charWidth}, lineHeight={_lineHeight}");

            // Force width on space labels that collapse to zero
            foreach (var label in _charLabels)
            {
                if (label.text == " ")
                    label.style.width = _charWidth;
            }

            // Force height on empty lines
            for (int i = 0; i < _linesContainer.childCount; i++)
            {
                var lineEl = _linesContainer[i];
                if (lineEl.childCount == 0)
                    lineEl.style.minHeight = _lineHeight;
            }

            UpdateCaretPosition();
        }

        // --- Private: Caret Position (Pure Math) ---

        private (float x, float y, float height) GetCaretPosition(int lineIdx, int col)
        {
            return (col * _charWidth, lineIdx * _lineHeight, _lineHeight);
        }

        // --- Private: Caret ---

        private void UpdateCaretPosition()
        {
            if (!_metricsReady)
            {
                Debug.Log("[ACF] UpdateCaretPosition: SKIPPED — metrics not ready");
                return;
            }

            // Caret visibility is controlled by ShowCaret/HideCaret — this method only updates position.
            var cursor = _hiddenInput.cursorIndex;
            var (line, col) = IndexToLineCol(cursor);
            var (x, y, h) = GetCaretPosition(line, col);

            Debug.Log($"[ACF] UpdateCaretPosition: cursorIndex={cursor}, line={line}, col={col}, x={x}, y={y}, h={h}, caretDisplay={_caret.style.display}");

            _caret.style.left = x;
            _caret.style.top = y;
            _caret.style.height = h;

            // Reset blink
            _caretVisible = true;
            _caret.style.opacity = 1f;

            // Update selection highlight
            UpdateSelection();
        }

        private void StartCaretBlink()
        {
            if (_caretBlink != null) return;
            _caretBlink = schedule.Execute(() =>
            {
                _caretVisible = !_caretVisible;
                _caret.style.opacity = _caretVisible ? 1f : 0f;
            }).Every(CaretBlinkMs);
        }

        private void StopCaretBlink()
        {
            if (_caretBlink != null)
            {
                _caretBlink.Pause();
                _caretBlink = null;
            }
        }

        // --- Private: Selection ---

        private void UpdateSelection()
        {
            _selectionLayer.Clear();

            var cursor = _hiddenInput.cursorIndex;
            var sel = _hiddenInput.selectIndex;
            if (cursor == sel || !_metricsReady) return;

            var start = Mathf.Min(cursor, sel);
            var end = Mathf.Max(cursor, sel);

            var (startLine, startCol) = IndexToLineCol(start);
            var (endLine, endCol) = IndexToLineCol(end);

            var text = _hiddenInput.value ?? "";
            var lines = text.Split('\n');

            for (int line = startLine; line <= endLine; line++)
            {
                if (line < 0 || line >= lines.Length) continue;

                int colStart = line == startLine ? startCol : 0;
                int colEnd = line == endLine ? endCol : lines[line].Length;

                if (colEnd <= colStart) continue;

                var (xStart, y, h) = GetCaretPosition(line, colStart);
                var (xEnd, _, _) = GetCaretPosition(line, colEnd);

                var rect = new VisualElement();
                rect.AddToClassList(SelectionRectClass);
                rect.pickingMode = PickingMode.Ignore;
                rect.style.left = xStart;
                rect.style.top = y;
                rect.style.width = xEnd - xStart;
                rect.style.height = h;
                _selectionLayer.Add(rect);
            }
        }

        // --- Private: Click Handling ---

        private void OnPointerDown(PointerDownEvent evt)
        {
            Debug.Log($"[ACF] OnPointerDown: position={evt.position}, metricsReady={_metricsReady}, isAnimating={IsAnimating}");

            if (IsAnimating)
            {
                CancelAnimation();
            }

            _pointerDown = true;
            _hiddenInput.Focus();

            if (_metricsReady)
            {
                var localPos = _linesContainer.WorldToLocal(evt.position);
                var idx = LocalPosToIndex(localPos);
                Debug.Log($"[ACF] OnPointerDown: localPos={localPos}, idx={idx}, linesContainer.layout={_linesContainer.layout}");
                _hiddenInput.SelectRange(idx, idx);
                ShowCaret();
                // Deferred: TextField resets cursor during internal focus handling
                schedule.Execute(() =>
                {
                    Debug.Log($"[ACF] OnPointerDown deferred: re-applying SelectRange({idx}, {idx}), cursorIndex before={_hiddenInput.cursorIndex}");
                    _hiddenInput.SelectRange(idx, idx);
                    UpdateCaretPosition();
                });
            }
            else
            {
                Debug.Log("[ACF] OnPointerDown: metrics NOT ready, skipping position calc");
            }

            evt.StopImmediatePropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_pointerDown || !_metricsReady) return;

            var localPos = _linesContainer.WorldToLocal(evt.position);
            var idx = LocalPosToIndex(localPos);
            var anchor = _hiddenInput.cursorIndex;
            _hiddenInput.SelectRange(anchor, idx);
            UpdateCaretPosition();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            _pointerDown = false;
            evt.StopImmediatePropagation();
        }

        private int LocalPosToIndex(Vector3 localPos)
        {
            var text = _hiddenInput.value ?? "";
            var lines = text.Split('\n');
            int lineIdx = Mathf.Clamp(Mathf.FloorToInt(localPos.y / _lineHeight), 0, lines.Length - 1);
            int col = Mathf.Clamp(Mathf.RoundToInt(localPos.x / _charWidth), 0, lines[lineIdx].Length);
            int idx = LineColToIndex(lineIdx, col);
            Debug.Log($"[ACF] LocalPosToIndex: localPos={localPos}, lineHeight={_lineHeight}, charWidth={_charWidth}, lineIdx={lineIdx}, col={col}, idx={idx}, totalLines={lines.Length}");
            return idx;
        }

        // --- Private: Hidden Input Events ---

        private void OnHiddenInputChanged(ChangeEvent<string> evt)
        {
            if (IsAnimating)
                CancelAnimation();

            // Detect inserted characters for typing animation
            var oldText = _previousText;
            var newText = evt.newValue ?? "";
            var insertedCount = newText.Length - oldText.Length;
            var cursorBeforeRebuild = _hiddenInput.cursorIndex;

            // Save cursor before rebuild — DOM changes can reset it
            var savedCursor = _hiddenInput.cursorIndex;
            var savedSelect = _hiddenInput.selectIndex;
            RebuildCharGrid();

            // Restore cursor and keep caret visible
            _hiddenInput.SelectRange(savedCursor, savedSelect);
            _caret.style.display = DisplayStyle.Flex;
            UpdateCaretPosition();

            // Trigger typing vibration for inserted chars
            if (insertedCount > 0 && TypingAnimationAmplitude > 0f)
            {
                var indices = new List<int>();
                // Map text indices to charLabel indices (skip newlines which aren't in _charLabels)
                int startTextIdx = cursorBeforeRebuild - insertedCount;
                if (startTextIdx < 0) startTextIdx = 0;
                for (int ti = startTextIdx; ti < cursorBeforeRebuild && ti < newText.Length; ti++)
                {
                    if (newText[ti] == '\n') continue;
                    // Count non-newline chars before this index to get charLabel index
                    int labelIdx = 0;
                    for (int j = 0; j < ti; j++)
                    {
                        if (newText[j] != '\n') labelIdx++;
                    }
                    if (labelIdx < _charLabels.Count)
                        indices.Add(labelIdx);
                }

                if (indices.Count > 0)
                    PlayTypingAnimation(indices);
            }

            _previousText = newText;
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            // Arrow keys, home/end don't change text but move the caret
            UpdateCaretPosition();
            // Belt-and-suspenders: cursorIndex may update after this callback returns
            schedule.Execute(() =>
            {
                UpdateCaretPosition();
                UpdateSelection();
            });
        }

        private void OnInputFocusIn(FocusInEvent evt)
        {
            Debug.Log($"[ACF] OnInputFocusIn: isReadOnly={isReadOnly}, isAnimating={IsAnimating}");
            if (IsAnimating)
                CancelAnimation();

            if (!isReadOnly)
                ShowCaret();
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            evt.destinationPanel.visualTree.RegisterCallback<PointerDownEvent>(
                OnPanelPointerDown, TrickleDown.TrickleDown);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            evt.originPanel.visualTree.UnregisterCallback<PointerDownEvent>(
                OnPanelPointerDown, TrickleDown.TrickleDown);
        }

        private void OnPanelPointerDown(PointerDownEvent evt)
        {
            if (evt.target is VisualElement target && !Contains(target) && target != this)
            {
                Debug.Log($"[ACF] OnPanelPointerDown: click outside — hiding caret. target={target.GetType().Name}");
                HideCaret();
            }
        }

        private void ShowCaret()
        {
            Debug.Log("[ACF] ShowCaret called");
            _caret.style.display = DisplayStyle.Flex;
            StartCaretBlink();
            UpdateCaretPosition();
        }

        private void HideCaret()
        {
            Debug.Log("[ACF] HideCaret called");
            _caret.style.display = DisplayStyle.None;
            StopCaretBlink();
            _selectionLayer.Clear();
        }

        // --- Private: Animation Loop ---

        private void AnimationTick()
        {
            if (_currentAnim == null) return;

            float elapsed = Time.realtimeSinceStartup - _animStartTime;
            bool stillRunning = false;
            for (int i = 0; i < _charLabels.Count; i++)
            {
                if (_currentAnim.Update(elapsed, i, _charLabels.Count, _charLabels[i]))
                    stillRunning = true;
            }

            if (!stillRunning)
                CancelAnimation();
        }

        // --- Private: Typing Animation ---

        private void PlayTypingAnimation(List<int> charIndices)
        {
            CancelTypingAnimation();

            _typingAnim = new VibrateAnimation
            {
                Amplitude = TypingAnimationAmplitude,
                Frequency = 18f,
                Duration = 0.3f
            };
            _typingAnim.SetTargetIndices(charIndices);
            _typingAnimStartTime = Time.realtimeSinceStartup;
            _typingAnim.Initialize(_charLabels.Count);
            _typingAnimSchedule = schedule.Execute(TypingAnimationTick).Every(16);
        }

        private void CancelTypingAnimation()
        {
            if (_typingAnim == null) return;

            for (int i = 0; i < _charLabels.Count; i++)
                _typingAnim.Reset(_charLabels[i]);

            _typingAnim = null;

            if (_typingAnimSchedule != null)
            {
                _typingAnimSchedule.Pause();
                _typingAnimSchedule = null;
            }
        }

        private void TypingAnimationTick()
        {
            if (_typingAnim == null) return;

            float elapsed = Time.realtimeSinceStartup - _typingAnimStartTime;
            bool stillRunning = false;
            for (int i = 0; i < _charLabels.Count; i++)
            {
                if (_typingAnim.Update(elapsed, i, _charLabels.Count, _charLabels[i]))
                    stillRunning = true;
            }

            if (!stillRunning)
                CancelTypingAnimation();
        }

        // --- Private: Index ↔ LineCol Helpers ---

        private (int line, int col) IndexToLineCol(int index)
        {
            var text = _hiddenInput.value ?? "";
            int line = 0;
            int col = 0;
            for (int i = 0; i < text.Length && i < index; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                    col = 0;
                }
                else
                {
                    col++;
                }
            }
            return (line, col);
        }

        private int LineColToIndex(int line, int col)
        {
            var text = _hiddenInput.value ?? "";
            int currentLine = 0;
            int currentCol = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (currentLine == line && currentCol == col) return i;
                if (text[i] == '\n')
                {
                    if (currentLine == line) return i; // col was past end of line
                    currentLine++;
                    currentCol = 0;
                }
                else
                {
                    currentCol++;
                }
            }
            return text.Length;
        }
    }
}
