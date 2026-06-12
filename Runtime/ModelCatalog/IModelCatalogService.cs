using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unidad.Core.ModelCatalog
{
    /// <summary>
    /// Catalog of pipeline-imported models (picoCAD → glTF → prefab) and their
    /// live instances. Spawns prefabs via IGameObjectFactory, wraps them in plain
    /// C# views, and routes effects through IAnimationResolver for test parity.
    /// </summary>
    public interface IModelCatalogService : IDisposable
    {
        /// <summary>All registered model ids.</summary>
        IEnumerable<string> ModelIds { get; }

        /// <summary>All registered kind ids.</summary>
        IEnumerable<string> KindIds { get; }

        /// <summary>Number of live instances.</summary>
        int InstanceCount { get; }

        ModelKindDefinition GetKind(string kindId);

        ModelEntry GetModel(string modelId);

        /// <summary>Spawn a model instance at a position. Returns the live instance.</summary>
        ModelInstance Spawn(string modelId, Vector3 position);

        bool TryGetInstance(string instanceId, out ModelInstance instance);

        void Despawn(string instanceId);

        /// <summary>
        /// Play a kind-defined effect on a live instance. onComplete fires when the
        /// effect ends (immediately under InstantAnimationResolver).
        /// </summary>
        void PlayEffect(string instanceId, string effectId, Action onComplete = null);
    }

    /// <summary>A live spawned model.</summary>
    public sealed class ModelInstance
    {
        public string InstanceId { get; }
        public string ModelId { get; }
        public string KindId { get; }
        public Views.ModelViewBase View { get; }

        public ModelInstance(string instanceId, string modelId, string kindId, Views.ModelViewBase view)
        {
            InstanceId = instanceId;
            ModelId = modelId;
            KindId = kindId;
            View = view;
        }
    }
}
