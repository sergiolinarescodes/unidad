using NUnit.Framework;
using Unity.Core;
using Unity.Entities;

namespace Unidad.Core.DOTS.Tests
{
    public abstract class DOTSTestFixture
    {
        protected World World;
        protected EntityManager Manager => World.EntityManager;

        [SetUp]
        public virtual void SetUp()
        {
            World = new World("TestWorld");
        }

        [TearDown]
        public virtual void TearDown()
        {
            if (World != null && World.IsCreated)
            {
                Manager.CompleteAllTrackedJobs();
                World.Dispose();
            }
            World = null;
        }

        protected Entity CreateEntity(params ComponentType[] types)
        {
            return Manager.CreateEntity(types);
        }

        protected DynamicBuffer<T> AddBuffer<T>(Entity entity) where T : unmanaged, IBufferElementData
        {
            Manager.AddBuffer<T>(entity);
            return Manager.GetBuffer<T>(entity);
        }

        protected DynamicBuffer<T> AddBuffer<T>(Entity entity, params T[] elements)
            where T : unmanaged, IBufferElementData
        {
            var buffer = AddBuffer<T>(entity);
            foreach (var e in elements)
                buffer.Add(e);
            return buffer;
        }

        protected bool IsEnabled<T>(Entity entity) where T : unmanaged, IEnableableComponent
        {
            return Manager.IsComponentEnabled<T>(entity);
        }

        protected void SetEnabled<T>(Entity entity, bool value) where T : unmanaged, IEnableableComponent
        {
            Manager.SetComponentEnabled<T>(entity, value);
        }

        protected SystemHandle GetOrCreateSystem<T>() where T : unmanaged, ISystem
        {
            return World.GetOrCreateSystem<T>();
        }

        protected SimulationSystemGroup CreateSimGroup(params SystemHandle[] systems)
        {
            var group = World.GetOrCreateSystemManaged<SimulationSystemGroup>();
            foreach (var sys in systems)
                group.AddSystemToUpdateList(sys);
            group.SortSystems();
            return group;
        }

        protected void UpdateGroup(SimulationSystemGroup group)
        {
            group.Update();
            Manager.CompleteAllTrackedJobs();
        }

        protected void SetWorldTime(double elapsed, float deltaTime)
        {
            World.SetTime(new TimeData(elapsed, deltaTime));
        }
    }
}
