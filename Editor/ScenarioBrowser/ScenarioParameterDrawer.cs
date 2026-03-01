using System;
using Unidad.Core.Testing;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.Editor.Editor.ScenarioBrowser
{
    /// <summary>
    /// Creates editable UI fields for ScenarioParameter based on value type.
    /// Supports int (slider), float (slider), string, bool, Vector2Int, and enums.
    /// </summary>
    public static class ScenarioParameterDrawer
    {
        public static VisualElement CreateField(ScenarioParameter param, ScenarioParameterOverrides overrides)
        {
            if (param.ValueType == typeof(int))
                return CreateIntField(param, overrides);
            if (param.ValueType == typeof(float))
                return CreateFloatField(param, overrides);
            if (param.ValueType == typeof(bool))
                return CreateBoolField(param, overrides);
            if (param.ValueType == typeof(string))
                return CreateStringField(param, overrides);
            if (param.ValueType == typeof(Vector2Int))
                return CreateVector2IntField(param, overrides);
            if (param.ValueType.IsEnum)
                return CreateEnumField(param, overrides);

            // Fallback: label showing type
            return new Label($"{param.Label}: [{param.ValueType.Name}] (unsupported type)");
        }

        private static VisualElement CreateIntField(ScenarioParameter param, ScenarioParameterOverrides overrides)
        {
            var defaultVal = param.DefaultValue is int i ? i : 0;
            var hasRange = param.MinValue is int && param.MaxValue is int;

            if (hasRange)
            {
                var min = (int)param.MinValue;
                var max = (int)param.MaxValue;
                var slider = new SliderInt(param.Label, min, max);
                slider.value = defaultVal;
                slider.showInputField = true;
                slider.RegisterValueChangedCallback(evt => overrides.Set(param.Name, evt.newValue));
                return slider;
            }

            var field = new IntegerField(param.Label);
            field.value = defaultVal;
            field.RegisterValueChangedCallback(evt => overrides.Set(param.Name, evt.newValue));
            return field;
        }

        private static VisualElement CreateFloatField(ScenarioParameter param, ScenarioParameterOverrides overrides)
        {
            var defaultVal = param.DefaultValue is float f ? f : 0f;
            var hasRange = param.MinValue is float && param.MaxValue is float;

            if (hasRange)
            {
                var min = (float)param.MinValue;
                var max = (float)param.MaxValue;
                var slider = new Slider(param.Label, min, max);
                slider.value = defaultVal;
                slider.showInputField = true;
                slider.RegisterValueChangedCallback(evt => overrides.Set(param.Name, evt.newValue));
                return slider;
            }

            var field = new FloatField(param.Label);
            field.value = defaultVal;
            field.RegisterValueChangedCallback(evt => overrides.Set(param.Name, evt.newValue));
            return field;
        }

        private static VisualElement CreateBoolField(ScenarioParameter param, ScenarioParameterOverrides overrides)
        {
            var defaultVal = param.DefaultValue is bool b && b;
            var toggle = new Toggle(param.Label);
            toggle.value = defaultVal;
            toggle.RegisterValueChangedCallback(evt => overrides.Set(param.Name, evt.newValue));
            return toggle;
        }

        private static VisualElement CreateStringField(ScenarioParameter param, ScenarioParameterOverrides overrides)
        {
            var defaultVal = param.DefaultValue as string ?? "";
            var field = new TextField(param.Label);
            field.value = defaultVal;
            field.RegisterValueChangedCallback(evt => overrides.Set(param.Name, evt.newValue));
            return field;
        }

        private static VisualElement CreateVector2IntField(ScenarioParameter param, ScenarioParameterOverrides overrides)
        {
            var defaultVal = param.DefaultValue is Vector2Int v ? v : Vector2Int.zero;

            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;

            var label = new Label(param.Label);
            label.style.width = 120;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            container.Add(label);

            var xField = new IntegerField("X") { value = defaultVal.x };
            xField.style.width = 60;
            container.Add(xField);

            var yField = new IntegerField("Y") { value = defaultVal.y };
            yField.style.width = 60;
            container.Add(yField);

            xField.RegisterValueChangedCallback(evt =>
                overrides.Set(param.Name, new Vector2Int(evt.newValue, yField.value)));
            yField.RegisterValueChangedCallback(evt =>
                overrides.Set(param.Name, new Vector2Int(xField.value, evt.newValue)));

            return container;
        }

        private static VisualElement CreateEnumField(ScenarioParameter param, ScenarioParameterOverrides overrides)
        {
            var defaultVal = param.DefaultValue as Enum;
            if (defaultVal == null && param.ValueType.IsEnum)
                defaultVal = (Enum)Enum.GetValues(param.ValueType).GetValue(0);

            var field = new EnumField(param.Label, defaultVal);
            field.RegisterValueChangedCallback(evt => overrides.Set(param.Name, evt.newValue));
            return field;
        }
    }
}
