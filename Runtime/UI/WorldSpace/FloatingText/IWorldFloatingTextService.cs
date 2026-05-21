using System;
using Unidad.Core.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.WorldSpace
{
    public interface IWorldFloatingTextService : IDisposable
    {
        void Initialize(PanelSettings panelSettings, VisualTreeAsset template = null, int prewarmCount = 10);
        void Spawn(Vector3 worldPosition, string text, FloatingTextStyle style = null, FloatingTextAnimator animator = null);
    }
}
