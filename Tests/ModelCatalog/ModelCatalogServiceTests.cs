using System;
using NUnit.Framework;
using UnityEngine;
using Unidad.Core.Abstractions;
using Unidad.Core.EventBus;
using EventBus = Unidad.Core.EventBus.EventBus;
using Unidad.Core.ModelCatalog;

namespace Unidad.Core.Tests.ModelCatalog
{
    [TestFixture]
    public sealed class ModelCatalogServiceTests
    {
        EventBus _eventBus;
        LocalGameObjectFactory _factory;
        ModelCatalogService _service;

        static ModelCatalogDatabase TestDatabase() => new(
            new[]
            {
                new ModelKindDefinition
                {
                    id = "misc", displayName = "Misc", folder = "Misc",
                    unitScale = 1f, effectProfile = "bounce",
                    effects = new[] { "spawn", "despawn", "hop" },
                },
            },
            new[]
            {
                new ModelEntry { id = "pig", kindId = "misc", prefabPath = "Models/Misc/pig", clips = new[] { "Motion" } },
                new ModelEntry { id = "ghost", kindId = "misc", prefabPath = "Models/Misc/does_not_exist", clips = Array.Empty<string>() },
            });

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _factory = new LocalGameObjectFactory();
            _service = new ModelCatalogService(_eventBus, _factory, new InstantAnimationResolver(), TestDatabase());
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            _factory.Dispose();
            _eventBus.ClearAllSubscriptions();
        }

        [Test]
        public void Database_LoadsKindsAndModels()
        {
            CollectionAssert.Contains(_service.KindIds, "misc");
            CollectionAssert.Contains(_service.ModelIds, "pig");
            Assert.AreEqual("Misc", _service.GetKind("misc").folder);
            Assert.AreEqual("Models/Misc/pig", _service.GetModel("pig").prefabPath);
        }

        [Test]
        public void GetModel_Unknown_Throws()
        {
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => _service.GetModel("nope"));
        }

        [Test]
        public void Spawn_CreatesInstance_PublishesEvent_TracksRegistry()
        {
            ModelSpawnedEvent? received = null;
            _eventBus.Subscribe<ModelSpawnedEvent>(e => received = e);

            var instance = _service.Spawn("pig", new Vector3(1, 0, 2));

            Assert.IsNotNull(instance.View.Root, "prefab should load from Resources/Models/Misc/pig");
            Assert.AreEqual(1, _service.InstanceCount);
            Assert.IsTrue(_service.TryGetInstance(instance.InstanceId, out _));
            Assert.IsNotNull(received);
            Assert.AreEqual("pig", received.Value.ModelId);
            Assert.AreEqual("misc", received.Value.KindId);
            Assert.AreEqual(new Vector3(1, 0, 2), instance.View.Root.transform.position);
        }

        [Test]
        public void Spawn_MissingPrefab_ThrowsInformative()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => _service.Spawn("ghost", Vector3.zero));
            StringAssert.Contains("does_not_exist", ex.Message);
            Assert.AreEqual(0, _service.InstanceCount);
        }

        [Test]
        public void Despawn_RemovesInstance_PublishesEvent()
        {
            var instance = _service.Spawn("pig", Vector3.zero);
            ModelDespawnedEvent? received = null;
            _eventBus.Subscribe<ModelDespawnedEvent>(e => received = e);

            _service.Despawn(instance.InstanceId);

            Assert.AreEqual(0, _service.InstanceCount);
            Assert.IsFalse(_service.TryGetInstance(instance.InstanceId, out _));
            Assert.IsNotNull(received);
            Assert.AreEqual(instance.InstanceId, received.Value.InstanceId);
        }

        [Test]
        public void PlayEffect_InstantResolver_CompletesSynchronously()
        {
            var instance = _service.Spawn("pig", Vector3.zero);
            var completed = false;
            ModelEffectPlayedEvent? effectEvent = null;
            _eventBus.Subscribe<ModelEffectPlayedEvent>(e => effectEvent = e);

            _service.PlayEffect(instance.InstanceId, "hop", () => completed = true);

            Assert.IsTrue(completed, "InstantAnimationResolver must complete synchronously");
            Assert.IsNotNull(effectEvent);
            Assert.AreEqual("hop", effectEvent.Value.EffectId);
        }

        [Test]
        public void PlayEffect_UnknownInstance_Throws()
        {
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
                () => _service.PlayEffect("nope_1", "hop"));
        }

        [Test]
        public void Spawn_InstanceIdsAreUnique()
        {
            var a = _service.Spawn("pig", Vector3.zero);
            var b = _service.Spawn("pig", Vector3.one);
            Assert.AreNotEqual(a.InstanceId, b.InstanceId);
            Assert.AreEqual(2, _service.InstanceCount);
        }
    }
}
