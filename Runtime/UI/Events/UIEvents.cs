using System;
using Unidad.Core.UI.Core;
using UnityEngine;

namespace Unidad.Core.UI.Events
{
    public readonly record struct PanelShownEvent(Type PanelType, UILayer Layer);
    public readonly record struct PanelHiddenEvent(Type PanelType, UILayer Layer);
    public readonly record struct DialogShownEvent(string DialogId);
    public readonly record struct DialogDismissedEvent(string DialogId, Dialog.DialogResult Result);
    public readonly record struct ThemeChangedEvent(string ThemeName);
    public readonly record struct FloatingTextSpawnedEvent(Vector3 Position, string Text);
}
