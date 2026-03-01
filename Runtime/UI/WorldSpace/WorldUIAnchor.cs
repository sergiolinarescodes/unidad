using UnityEngine;

namespace Unidad.Core.UI.WorldSpace
{
    public sealed class WorldUIAnchor : MonoBehaviour
    {
        private Transform _target;
        private Vector3 _offset;
        private bool _billboard;
        private Camera _mainCamera;

        public void Initialize(Transform target, WorldUISettings settings)
        {
            _target = target;
            _offset = settings.Offset;
            _billboard = settings.Billboard;
            transform.localScale = Vector3.one * settings.Scale;
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            transform.position = _target.position + _offset;

            if (_billboard)
            {
                _mainCamera ??= Camera.main;
                if (_mainCamera != null)
                    transform.rotation = _mainCamera.transform.rotation;
            }
        }
    }
}
