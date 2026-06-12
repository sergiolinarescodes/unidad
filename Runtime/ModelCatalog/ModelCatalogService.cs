using System;
using System.Collections.Generic;
using UnityEngine;
using Unidad.Core.Abstractions;
using Unidad.Core.EventBus;
using Unidad.Core.Factory;
using Unidad.Core.Registry;
using Unidad.Core.Systems;

namespace Unidad.Core.ModelCatalog
{
    internal sealed class ModelCatalogService : SystemServiceBase, IModelCatalogService, ITickable
    {
        readonly IGameObjectFactory _factory;
        readonly IAnimationResolver _animationResolver;
        readonly RegistryBase<string, ModelKindDefinition> _kinds = new();
        readonly RegistryBase<string, ModelEntry> _models = new();
        readonly RegistryBase<string, ModelInstance> _instances = new();
        readonly Dictionary<string, Func<GameObject, Views.ModelViewBase>> _viewFactories;
        int _nextInstanceNumber = 1;

        public ModelCatalogService(
            IEventBus eventBus,
            IGameObjectFactory factory,
            IAnimationResolver animationResolver,
            ModelCatalogDatabase database,
            Dictionary<string, Func<GameObject, Views.ModelViewBase>> viewFactories = null)
            : base(eventBus)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _animationResolver = animationResolver ?? throw new ArgumentNullException(nameof(animationResolver));
            _viewFactories = viewFactories ?? new Dictionary<string, Func<GameObject, Views.ModelViewBase>>();

            if (database != null)
            {
                foreach (var kind in database.Kinds)
                    _kinds.Register(kind.id, kind);
                foreach (var model in database.Models)
                    _models.Register(model.id, model);
            }
        }

        public IEnumerable<string> ModelIds => _models.Keys;
        public IEnumerable<string> KindIds => _kinds.Keys;
        public int InstanceCount => _instances.Count;

        public ModelKindDefinition GetKind(string kindId) => _kinds.Get(kindId);

        public ModelEntry GetModel(string modelId) => _models.Get(modelId);

        public ModelInstance Spawn(string modelId, Vector3 position)
        {
            var entry = _models.Get(modelId);
            var instanceId = $"{modelId}_{_nextInstanceNumber++}";

            var gameObject = _factory.InstantiatePrefab(entry.prefabPath, instanceId, position);
            if (gameObject == null)
                throw new InvalidOperationException(
                    $"Model '{modelId}': prefab not found at Resources/{entry.prefabPath}");

            var view = CreateView(entry, gameObject);
            var instance = new ModelInstance(instanceId, modelId, entry.kindId, view);
            _instances.Register(instanceId, instance);

            Publish(new ModelSpawnedEvent(instanceId, modelId, entry.kindId));
            return instance;
        }

        public bool TryGetInstance(string instanceId, out ModelInstance instance) =>
            _instances.TryGet(instanceId, out instance);

        public void Despawn(string instanceId)
        {
            var instance = _instances.Get(instanceId);
            _instances.Remove(instanceId);
            if (instance.View.Root != null)
                _factory.Destroy(instance.View.Root);
            Publish(new ModelDespawnedEvent(instanceId, instance.ModelId));
        }

        public void PlayEffect(string instanceId, string effectId, Action onComplete = null)
        {
            var instance = _instances.Get(instanceId); // throws on unknown — caller bug
            _animationResolver.Play($"{instanceId}/{effectId}", onComplete);
            Publish(new ModelEffectPlayedEvent(instanceId, effectId));
        }

        /// <summary>Resolve a live instance to its transform + effect profile (used by the production resolver).</summary>
        public (Transform target, string profileId)? ResolveInstanceForEffects(string instanceId)
        {
            if (!_instances.TryGet(instanceId, out var instance) || instance.View.Root == null)
                return null;
            var profileId = _kinds.TryGet(instance.KindId, out var kind) ? kind.effectProfile : null;
            return (instance.View.Root.transform, profileId);
        }

        public void Tick(float deltaTime)
        {
            foreach (var instance in _instances.Values)
                instance.View.Tick(deltaTime);
        }

        Views.ModelViewBase CreateView(ModelEntry entry, GameObject gameObject)
        {
            var viewClass = _kinds.TryGet(entry.kindId, out var kind) ? kind.viewClass : null;
            if (!string.IsNullOrEmpty(viewClass) && _viewFactories.TryGetValue(viewClass, out var create))
                return create(gameObject);
            return new Views.DefaultModelView(gameObject);
        }

        public override void Dispose()
        {
            foreach (var instance in _instances.Values)
            {
                if (instance.View.Root != null)
                    _factory.Destroy(instance.View.Root);
            }
            _instances.Clear();
            base.Dispose();
        }
    }
}
