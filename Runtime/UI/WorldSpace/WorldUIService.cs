using System.Collections.Generic;
using Unidad.Core.EventBus;
using Unidad.Core.Factory;
using Unidad.Core.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.WorldSpace
{
    internal sealed class WorldUIService : SystemServiceBase, IWorldUIService
    {
        private readonly IGameObjectFactory _factory;
        private readonly Dictionary<int, WorldUIHandle> _handles = new();
        private int _nextId;

        public WorldUIService(IEventBus eventBus, IGameObjectFactory factory) : base(eventBus)
        {
            _factory = factory;
        }

        public WorldUIHandle Attach(Transform target, PanelSettings panelSettings, WorldUISettings settings = null)
        {
            settings ??= WorldUISettings.Default;

            var go = _factory.CreateEmpty($"WorldUI_{_nextId}", null);
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.sortingOrder = settings.SortOrder;

            var anchor = go.AddComponent<WorldUIAnchor>();
            anchor.Initialize(target, settings);

            var handle = new WorldUIHandle(_nextId, go, document);
            _handles[_nextId] = handle;
            _nextId++;

            return handle;
        }

        public void Detach(WorldUIHandle handle)
        {
            if (handle == null) return;
            if (!_handles.Remove(handle.Id)) return;

            if (handle.GameObject != null)
                _factory.Destroy(handle.GameObject);
        }

        public void DetachAll()
        {
            foreach (var handle in _handles.Values)
            {
                if (handle.GameObject != null)
                    _factory.Destroy(handle.GameObject);
            }
            _handles.Clear();
        }

        public override void Dispose()
        {
            DetachAll();
            base.Dispose();
        }
    }
}
