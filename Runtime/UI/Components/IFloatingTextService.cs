using UnityEngine;

namespace Unidad.Core.UI.Components
{
    public interface IFloatingTextService
    {
        void Spawn(Vector3 worldPosition, string text, FloatingTextStyle style = null);
        void Spawn(Transform follow, string text, FloatingTextStyle style = null);
    }
}
