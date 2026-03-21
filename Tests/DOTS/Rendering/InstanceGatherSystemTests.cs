using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS.Tests
{
    [TestFixture]
    public class InstanceGatherSystemTests : DOTSTestFixture
    {
        InstanceGatherSystem _system;

        public override void SetUp()
        {
            base.SetUp();
            _system = World.GetOrCreateSystemManaged<InstanceGatherSystem>();
        }

        public override void TearDown()
        {
            // Clear static state before world dispose
            InstanceGatherSystem.Batches = null;
            InstanceGatherSystem.BatchCount = 0;
            base.TearDown();
        }

        void UpdateGather()
        {
            _system.Update();
            Manager.CompleteAllTrackedJobs();
        }

        Entity CreateRenderable(int meshId, int materialId, float3 position, float4 color)
        {
            var entity = Manager.CreateEntity(
                ComponentType.ReadWrite<LocalToWorld>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<InstanceColor>());

            Manager.AddSharedComponentManaged(entity,
                new InstanceRenderable { MeshId = meshId, MaterialId = materialId });

            Manager.SetComponentData(entity, new LocalToWorld
            {
                Value = float4x4.TRS(position, quaternion.identity, new float3(1))
            });
            Manager.SetComponentData(entity, LocalTransform.FromPosition(position));
            Manager.SetComponentData(entity, new InstanceColor { Value = color });

            return entity;
        }

        [Test]
        public void SingleEntity_ProducesOneBatch()
        {
            CreateRenderable(1, 1, new float3(0, 5, 0), new float4(1, 0, 0, 1));

            UpdateGather();

            Assert.AreEqual(1, InstanceGatherSystem.BatchCount);
            Assert.AreEqual(1, InstanceGatherSystem.Batches[0].Count);
            Assert.AreEqual(1, InstanceGatherSystem.Batches[0].Renderable.MeshId);
            Assert.AreEqual(1, InstanceGatherSystem.Batches[0].Renderable.MaterialId);
        }

        [Test]
        public void MultipleEntities_SameMeshMaterial_SingleBatch()
        {
            CreateRenderable(1, 1, new float3(0, 0, 0), new float4(1, 0, 0, 1));
            CreateRenderable(1, 1, new float3(1, 0, 0), new float4(0, 1, 0, 1));
            CreateRenderable(1, 1, new float3(2, 0, 0), new float4(0, 0, 1, 1));

            UpdateGather();

            Assert.AreEqual(1, InstanceGatherSystem.BatchCount);
            Assert.AreEqual(3, InstanceGatherSystem.Batches[0].Count);
        }

        [Test]
        public void DifferentMeshIds_SeparateBatches()
        {
            CreateRenderable(1, 1, new float3(0, 0, 0), new float4(1, 0, 0, 1));
            CreateRenderable(2, 1, new float3(1, 0, 0), new float4(0, 1, 0, 1));

            UpdateGather();

            Assert.AreEqual(2, InstanceGatherSystem.BatchCount);

            // Both batches should have 1 entity each
            Assert.AreEqual(1, InstanceGatherSystem.Batches[0].Count);
            Assert.AreEqual(1, InstanceGatherSystem.Batches[1].Count);
        }

        [Test]
        public void DifferentMaterialIds_SeparateBatches()
        {
            CreateRenderable(1, 1, new float3(0, 0, 0), new float4(1, 0, 0, 1));
            CreateRenderable(1, 2, new float3(1, 0, 0), new float4(0, 1, 0, 1));

            UpdateGather();

            Assert.AreEqual(2, InstanceGatherSystem.BatchCount);
        }

        [Test]
        public void ColorValues_CorrectlyGathered()
        {
            var color = new float4(0.5f, 0.25f, 0.75f, 1.0f);
            CreateRenderable(1, 1, float3.zero, color);

            UpdateGather();

            var batch = InstanceGatherSystem.Batches[0];
            Assert.AreEqual(1, batch.Count);
            Assert.IsTrue(batch.Colors.IsCreated);

            var gathered = batch.Colors[0];
            Assert.AreEqual(color.x, gathered.x, 0.001f);
            Assert.AreEqual(color.y, gathered.y, 0.001f);
            Assert.AreEqual(color.z, gathered.z, 0.001f);
            Assert.AreEqual(color.w, gathered.w, 0.001f);
        }

        [Test]
        public void TransformValues_CorrectlyGathered()
        {
            var pos = new float3(3, 7, 11);
            CreateRenderable(1, 1, pos, new float4(1, 1, 1, 1));

            UpdateGather();

            var batch = InstanceGatherSystem.Batches[0];
            var matrix = batch.Matrices[0];
            // Translation is in column 3 (indices [0,3], [1,3], [2,3])
            Assert.AreEqual(pos.x, matrix.c3.x, 0.001f);
            Assert.AreEqual(pos.y, matrix.c3.y, 0.001f);
            Assert.AreEqual(pos.z, matrix.c3.z, 0.001f);
        }

        [Test]
        public void NoRenderableEntities_ZeroBatches()
        {
            // Create entity without InstanceRenderable
            Manager.CreateEntity(ComponentType.ReadWrite<LocalToWorld>());

            UpdateGather();

            Assert.AreEqual(0, InstanceGatherSystem.BatchCount);
        }

        [Test]
        public void AnimationComponent_SetsHasAnimationFlag()
        {
            var entity = CreateRenderable(1, 1, float3.zero, new float4(1, 1, 1, 1));
            Manager.AddComponentData(entity, new InstanceAnimation
            {
                ClipId = 1,
                Time = 0.5f,
                Speed = 1.0f,
                PhaseOffset = 0.1f
            });

            UpdateGather();

            var batch = InstanceGatherSystem.Batches[0];
            Assert.IsTrue(batch.HasAnimation);
            Assert.IsTrue(batch.AnimParams.IsCreated);

            var animParam = batch.AnimParams[0];
            Assert.AreEqual(0.6f, animParam.x, 0.001f); // time + phase
            Assert.AreEqual(1.0f, animParam.y, 0.001f);  // speed
            Assert.AreEqual(1.0f, animParam.z, 0.001f);  // clipId
        }
    }
}
