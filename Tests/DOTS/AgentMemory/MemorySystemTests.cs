using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS.Tests
{
    [TestFixture]
    public class MemorySystemTests : DOTSTestFixture
    {
        SimulationSystemGroup _group;

        public override void SetUp()
        {
            base.SetUp();
            var clearSys = GetOrCreateSystem<MemoryEventClearSystem>();
            var decaySys = GetOrCreateSystem<MemoryDecaySystem>();
            _group = CreateSimGroup(clearSys, decaySys);
        }

        Entity CreateMemoryAgent(int maxMemories = 10, float decayRate = 0.1f, float threshold = 0.05f)
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<MemoryConfig>(),
                ComponentType.ReadWrite<MemoryAdded>(),
                ComponentType.ReadWrite<MemoryForgotten>());

            Manager.SetComponentData(e, new MemoryConfig
            {
                MaxMemories = maxMemories,
                DecayRate = decayRate,
                ImportanceThreshold = threshold
            });

            SetEnabled<MemoryAdded>(e, false);
            SetEnabled<MemoryForgotten>(e, false);

            AddBuffer<MemoryElement>(e);
            return e;
        }

        void AddTestMemory(Entity e, int type, float importance, float3 location = default, double timestamp = 0.0)
        {
            var memories = Manager.GetBuffer<MemoryElement>(e);
            memories.Add(new MemoryElement
            {
                MemoryType = type,
                Location = location,
                Timestamp = timestamp,
                Importance = importance,
                IntParam = 0,
                FloatParam = 0f
            });
        }

        [Test]
        public void Importance_DecaysOverTime()
        {
            var agent = CreateMemoryAgent(decayRate: 0.5f);
            AddTestMemory(agent, type: 1, importance: 1.0f);

            SetWorldTime(1.0, 1.0f); // 1 second, decay 0.5/s → importance 0.5
            UpdateGroup(_group);

            var memories = Manager.GetBuffer<MemoryElement>(agent);
            Assert.AreEqual(1, memories.Length);
            Assert.AreEqual(0.5f, memories[0].Importance, 0.01f);
        }

        [Test]
        public void Memory_ForgottenBelowThreshold()
        {
            var agent = CreateMemoryAgent(decayRate: 1f, threshold: 0.5f);
            AddTestMemory(agent, type: 1, importance: 0.6f);

            SetWorldTime(0.2, 0.2f); // 0.2s * 1/s decay = 0.2 lost → 0.4 < 0.5 threshold
            UpdateGroup(_group);

            var memories = Manager.GetBuffer<MemoryElement>(agent);
            Assert.AreEqual(0, memories.Length, "Memory should be forgotten");
            Assert.IsTrue(IsEnabled<MemoryForgotten>(agent));
        }

        [Test]
        public void Memory_SurvivesAboveThreshold()
        {
            var agent = CreateMemoryAgent(decayRate: 0.1f, threshold: 0.05f);
            AddTestMemory(agent, type: 1, importance: 1.0f);

            SetWorldTime(1.0, 1.0f); // 1s * 0.1/s = 0.1 lost → 0.9 > 0.05
            UpdateGroup(_group);

            var memories = Manager.GetBuffer<MemoryElement>(agent);
            Assert.AreEqual(1, memories.Length);
        }

        [Test]
        public void ForgottenEvent_ClearedNextFrame()
        {
            var agent = CreateMemoryAgent();
            SetEnabled<MemoryForgotten>(agent, true);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsFalse(IsEnabled<MemoryForgotten>(agent));
        }

        [Test]
        public void Utility_AddMemory_EvictsLowestImportance()
        {
            var agent = CreateMemoryAgent(maxMemories: 2);

            var memories = Manager.GetBuffer<MemoryElement>(agent);
            var config = Manager.GetComponentData<MemoryConfig>(agent);

            MemoryUtility.AddMemory(ref memories, in config, 1, float3.zero, importance: 0.5f, 0, 0f, 1.0);
            MemoryUtility.AddMemory(ref memories, in config, 2, float3.zero, importance: 0.8f, 0, 0f, 2.0);

            Assert.AreEqual(2, memories.Length);

            // Add third — should evict lowest (0.5)
            MemoryUtility.AddMemory(ref memories, in config, 3, float3.zero, importance: 0.9f, 0, 0f, 3.0);

            Assert.AreEqual(2, memories.Length);
            // Remaining should be types 2 (0.8) and 3 (0.9)
            bool has2 = false, has3 = false;
            for (int i = 0; i < memories.Length; i++)
            {
                if (memories[i].MemoryType == 2) has2 = true;
                if (memories[i].MemoryType == 3) has3 = true;
            }
            Assert.IsTrue(has2 && has3, "Should keep highest importance memories");
        }

        [Test]
        public void Utility_FindNearest_ReturnsClosest()
        {
            var agent = CreateMemoryAgent();
            var memories = Manager.GetBuffer<MemoryElement>(agent);

            memories.Add(new MemoryElement
            {
                MemoryType = 1, Location = new float3(10, 0, 0), Importance = 1f
            });
            memories.Add(new MemoryElement
            {
                MemoryType = 1, Location = new float3(3, 0, 0), Importance = 1f
            });
            memories.Add(new MemoryElement
            {
                MemoryType = 2, Location = new float3(1, 0, 0), Importance = 1f // Different type
            });

            int idx = MemoryUtility.FindNearest(in memories, 1, float3.zero);
            Assert.AreEqual(1, idx, "Should find the memory at (3,0,0)");
        }

        [Test]
        public void Utility_FindMostRecent_ReturnsNewest()
        {
            var agent = CreateMemoryAgent();
            var memories = Manager.GetBuffer<MemoryElement>(agent);

            memories.Add(new MemoryElement { MemoryType = 1, Timestamp = 1.0, Importance = 1f });
            memories.Add(new MemoryElement { MemoryType = 1, Timestamp = 5.0, Importance = 1f });
            memories.Add(new MemoryElement { MemoryType = 1, Timestamp = 3.0, Importance = 1f });

            int idx = MemoryUtility.FindMostRecent(in memories, 1);
            Assert.AreEqual(1, idx, "Should find memory with timestamp 5.0");
        }

        [Test]
        public void Utility_CountByType()
        {
            var agent = CreateMemoryAgent();
            var memories = Manager.GetBuffer<MemoryElement>(agent);

            memories.Add(new MemoryElement { MemoryType = 1, Importance = 1f });
            memories.Add(new MemoryElement { MemoryType = 2, Importance = 1f });
            memories.Add(new MemoryElement { MemoryType = 1, Importance = 1f });

            Assert.AreEqual(2, MemoryUtility.CountByType(in memories, 1));
            Assert.AreEqual(1, MemoryUtility.CountByType(in memories, 2));
            Assert.AreEqual(0, MemoryUtility.CountByType(in memories, 3));
        }
    }
}
