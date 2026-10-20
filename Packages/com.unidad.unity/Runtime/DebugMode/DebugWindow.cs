using System;
using System.Collections.Generic;
using System.Linq;
using Unidad.Adapter.Ui;
using Unidad.Core.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Adapter
{
    /// <summary>
    /// The DEBUG MODE window: draggable by its title, one tab per section title (<see cref="DebugLayout"/>). It is the
    /// <see cref="IDebugUi"/> the sections declare their rows on; readouts are read again every 0.25 s while it is shown.
    /// </summary>
    internal sealed class DebugWindow : IDebugUi
    {
        private const long RefreshMilliseconds = 250;

        private readonly UiDocumentHost _host;
        private readonly VisualElement _window;
        private readonly List<(Label Label, string Name, Func<string> Value)> _readouts = new();
        private readonly List<(IntegerField Field, Func<int> Value)> _liveInts = new();
        private VisualElement _rows;
        private bool _dragging;

        public DebugWindow(UiDocumentHost host, IEnumerable<IDebugSection> sections)
        {
            _host = host;
            _window = host.Q<VisualElement>("window");
            var title = host.Q<Label>("title");
            title.RegisterCallback<PointerDownEvent>(e =>
            {
                _dragging = true;
                title.CapturePointer(e.pointerId);
            });
            title.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!_dragging) return;
                _window.style.left = _window.resolvedStyle.left + e.deltaPosition.x;
                _window.style.top = _window.resolvedStyle.top + e.deltaPosition.y;
            });
            title.RegisterCallback<PointerUpEvent>(e =>
            {
                _dragging = false;
                title.ReleasePointer(e.pointerId);
            });

            var tabs = host.Q<TabView>("tabs");
            foreach (var group in DebugLayout.Group(sections))
            {
                var tab = new Tab(group.Title);
                var scroll = new ScrollView(ScrollViewMode.Vertical);
                scroll.AddToClassList("unidad-debug__rows");
                tab.Add(scroll);
                tabs.Add(tab);
                _rows = scroll;
                foreach (var section in group.Sections)
                {
                    try
                    {
                        section.Build(this);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[Debug] section '{section.Title}' ({section.GetType().Name}) failed to build: {ex}");
                        Readout("Error", () => ex.Message);
                    }
                }
            }
            _rows = null;
            _window.schedule.Execute(Refresh).Every(RefreshMilliseconds);
        }

        public void SetVisible(bool visible) => _host.SetVisible(visible);

        private VisualElement Rows => _rows ?? throw new InvalidOperationException("Debug rows are declared only inside IDebugSection.Build.");

        public void Button(string text, Action action) => Rows.Add(new Button(() => Safe(action, text)) { text = text });

        public void Readout(string label, Func<string> value)
        {
            var line = new Label(Read(label, value));
            line.AddToClassList("unidad-debug__readout");
            Rows.Add(line);
            _readouts.Add((line, label, value));
        }

        public void IntField(string label, int value, Action<int> set) => IntRow(label, value, set);

        public void IntField(string label, Func<int> value, Action<int> set) => _liveInts.Add((IntRow(label, value(), set), value));

        public void TextField(string label, string value, Action<string> set)
        {
            var field = new TextField(label) { value = value ?? "" };
            field.RegisterValueChangedCallback(e => Safe(() => set(e.newValue), label));
            Rows.Add(field);
        }

        public void Choice(string label, string[] options, Action<int> pick)
        {
            var field = new DropdownField(label, options.ToList(), options.Length > 0 ? 0 : -1);
            field.RegisterValueChangedCallback(_ => Safe(() => pick(field.index), label));
            Rows.Add(field);
        }

        public void Separator()
        {
            var line = new VisualElement();
            line.AddToClassList("unidad-debug__separator");
            Rows.Add(line);
        }

        private IntegerField IntRow(string label, int value, Action<int> set)
        {
            var field = new IntegerField(label) { value = value };
            field.RegisterValueChangedCallback(e => Safe(() => set(e.newValue), label));
            Rows.Add(field);
            return field;
        }

        private void Refresh()
        {
            if (_host.Root.resolvedStyle.display == DisplayStyle.None) return;
            foreach (var (label, name, value) in _readouts) label.text = Read(name, value);
            foreach (var (field, value) in _liveInts)
                if (field.focusController?.focusedElement != field) Safe(() => field.SetValueWithoutNotify(value()), "IntField");
        }

        private static string Read(string name, Func<string> value)
        {
            string text;
            try
            {
                text = value() ?? "";
            }
            catch (Exception ex)
            {
                text = $"<{ex.GetType().Name}: {ex.Message}>";
            }
            return string.IsNullOrEmpty(name) ? text : $"{name}: {text}";
        }

        private static void Safe(Action action, string what)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error($"[Debug] '{what}' failed: {ex}");
            }
        }
    }
}
