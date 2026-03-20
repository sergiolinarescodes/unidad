using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS.Tests
{
    [TestFixture]
    public class EntityPoolSystemTests : DOTSTestFixture
    {
        Entity CreatePrototype(int poolId, int prewarmCount = 0)
        {
            var e = CreateEntity(ComponentType.ReadWrite<PoolPrototype>());
            Manager.SetComponentData(e, new PoolPrototype { PoolId = poolId, PrewarmCount = prewarmCount });
            return e;
        }

        Entity CreatePooledEntity(int poolId, bool active = false)
        {
            var types = new ComponentType[]
            {
                ComponentType.ReadWrite<Pooled>(),
                ComponentType.ReadWrite<PoolActive>(),
                ComponentType.ReadWrite<PoolAcquired>(),
                ComponentType.ReadWrite<PoolReturned>(),
                ComponentType.ReadWrite<ReturnRequest>()
            };
            var e = CreateEntity(types);
            Manager.SetComponentData(e, new Pooled { PoolId = poolId });
            SetEnabled<PoolActive>(e, active);
            SetEnabled<PoolAcquired>(e, false);
            SetEnabled<PoolReturned>(e, false);
            SetEnabled<ReturnRequest>(e, false);
            if (!active)
                Manager.AddComponent<Disabled>(e);
            return e;
        }

        Entity CreateAcquireRequest(int poolId)
        {
            var e = CreateEntity(ComponentType.ReadWrite<AcquireRequest>());
            Manager.SetComponentData(e, new AcquireRequest { PoolId = poolId });
            return e;
        }

        int CountPooledEntities(int poolId)
        {
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Pooled>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Build(Manager);
            var pooledArray = query.ToComponentDataArray<Pooled>(Allocator.Temp);
            int count = 0;
            for (int i = 0; i < pooledArray.Length; i++)
            {
                if (pooledArray[i].PoolId == poolId)
                    count++;
            }
            pooledArray.Dispose();
            return count;
        }

        // --- Prewarm ---

        [Test]
        public void Prewarm_CreatesCorrectCount()
        {
            CreatePrototype(1, prewarmCount: 3);

            var handle = GetOrCreateSystem<EntityPoolPrewarmSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            Assert.AreEqual(3, CountPooledEntities(1));
        }

        [Test]
        public void Prewarm_ZerosPrewarmCount()
        {
            var proto = CreatePrototype(1, prewarmCount: 3);

            var handle = GetOrCreateSystem<EntityPoolPrewarmSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            Assert.AreEqual(0, Manager.GetComponentData<PoolPrototype>(proto).PrewarmCount);
        }

        [Test]
        public void Prewarm_SpawnedEntitiesAreDisabled()
        {
            CreatePrototype(1, prewarmCount: 2);

            var handle = GetOrCreateSystem<EntityPoolPrewarmSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Pooled, Disabled>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Build(Manager);
            Assert.AreEqual(2, query.CalculateEntityCount());
        }

        [Test]
        public void Prewarm_PoolActiveDisabledOnSpawned()
        {
            CreatePrototype(1, prewarmCount: 1);

            var handle = GetOrCreateSystem<EntityPoolPrewarmSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Pooled>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Build(Manager);
            var entities = query.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(1, entities.Length);
            Assert.IsFalse(IsEnabled<PoolActive>(entities[0]));
            entities.Dispose();
        }

        // --- Acquire ---

        [Test]
        public void Acquire_ReusesAvailableEntity()
        {
            var pooled = CreatePooledEntity(1, active: false);
            CreatePrototype(1); // needed for fallback lookup
            var request = CreateAcquireRequest(1);

            var handle = GetOrCreateSystem<EntityPoolSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            Assert.IsTrue(Manager.Exists(pooled));
            Assert.IsFalse(Manager.HasComponent<Disabled>(pooled));
        }

        [Test]
        public void Acquire_RemovesDisabled()
        {
            var pooled = CreatePooledEntity(1, active: false);
            CreatePrototype(1);
            CreateAcquireRequest(1);

            var handle = GetOrCreateSystem<EntityPoolSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            Assert.IsFalse(Manager.HasComponent<Disabled>(pooled));
        }

        [Test]
        public void Acquire_EnablesPoolActive()
        {
            var pooled = CreatePooledEntity(1, active: false);
            CreatePrototype(1);
            CreateAcquireRequest(1);

            var handle = GetOrCreateSystem<EntityPoolSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            Assert.IsTrue(IsEnabled<PoolActive>(pooled));
        }

        [Test]
        public void Acquire_SetsPoolAcquired()
        {
            var pooled = CreatePooledEntity(1, active: false);
            CreatePrototype(1);
            CreateAcquireRequest(1);

            var handle = GetOrCreateSystem<EntityPoolSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            Assert.IsTrue(IsEnabled<PoolAcquired>(pooled));
        }

        [Test]
        public void Acquire_DestroysRequestEntity()
        {
            CreatePooledEntity(1, active: false);
            CreatePrototype(1);
            var request = CreateAcquireRequest(1);

            var handle = GetOrCreateSystem<EntityPoolSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            Assert.IsFalse(Manager.Exists(request));
        }

        [Test]
        public void Acquire_InstantiatesFromPrototypeWhenPoolEmpty()
        {
            // No pooled entities — only prototype
            CreatePrototype(1);
            CreateAcquireRequest(1);

            var handle = GetOrCreateSystem<EntityPoolSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            // Should have instantiated one new pooled entity
            Assert.AreEqual(1, CountPooledEntities(1));
        }

        // --- Return ---

        [Test]
        public void Return_DisablesPoolActive()
        {
            var pooled = CreatePooledEntity(1, active: true);
            SetEnabled<ReturnRequest>(pooled, true);

            var handle = GetOrCreateSystem<EntityPoolSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            Assert.IsFalse(IsEnabled<PoolActive>(pooled));
        }

        [Test]
        public void Return_AddsDisabled()
        {
            var pooled = CreatePooledEntity(1, active: true);
            SetEnabled<ReturnRequest>(pooled, true);

            var handle = GetOrCreateSystem<EntityPoolSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            Assert.IsTrue(Manager.HasComponent<Disabled>(pooled));
        }

        [Test]
        public void Return_SetsPoolReturned()
        {
            var pooled = CreatePooledEntity(1, active: true);
            SetEnabled<ReturnRequest>(pooled, true);

            var handle = GetOrCreateSystem<EntityPoolSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            Assert.IsTrue(IsEnabled<PoolReturned>(pooled));
        }

        [Test]
        public void Return_ClearsReturnRequest()
        {
            var pooled = CreatePooledEntity(1, active: true);
            SetEnabled<ReturnRequest>(pooled, true);

            var handle = GetOrCreateSystem<EntityPoolSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            Assert.IsFalse(IsEnabled<ReturnRequest>(pooled));
        }

        // --- Events ---

        [Test]
        public void Events_ClearedNextFrame()
        {
            var pooled = CreatePooledEntity(1, active: true);
            SetEnabled<PoolAcquired>(pooled, true); // simulate previous acquisition

            var handle = GetOrCreateSystem<EntityPoolSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            Assert.IsFalse(IsEnabled<PoolAcquired>(pooled));
        }

        [Test]
        public void MultipleAcquires_ClaimDifferentEntities()
        {
            var p1 = CreatePooledEntity(1, active: false);
            var p2 = CreatePooledEntity(1, active: false);
            CreatePrototype(1);
            CreateAcquireRequest(1);
            CreateAcquireRequest(1);

            var handle = GetOrCreateSystem<EntityPoolSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            // Both should be acquired (not disabled)
            Assert.IsFalse(Manager.HasComponent<Disabled>(p1));
            Assert.IsFalse(Manager.HasComponent<Disabled>(p2));
        }
    }
}
