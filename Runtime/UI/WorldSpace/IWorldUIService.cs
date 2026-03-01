using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.WorldSpace
{
    public interface IWorldUIService
    {
        WorldUIHandle Attach(Transform target, PanelSettings panelSettings, WorldUISettings settings = null);
        void Detach(WorldUIHandle handle);
        void DetachAll();
    }
}
