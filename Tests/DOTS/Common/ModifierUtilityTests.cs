using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS.Tests
{
    [TestFixture]
    public class ModifierUtilityTests : DOTSTestFixture
    {
        Entity _entity;
        DynamicBuffer<ModifierElement> _buffer;

        public override void SetUp()
        {
            base.SetUp();
            _entity = CreateEntity(ComponentType.ReadWrite<ModifierElement>());
            _buffer = Manager.GetBuffer<ModifierElement>(_entity);
        }

        // --- Evaluate ---

        [Test]
        public void Evaluate_EmptyBuffer_ReturnsBaseValue()
        {
            Assert.AreEqual(10f, ModifierUtility.Evaluate(in _buffer, 10f));
        }

        [Test]
        public void Evaluate_SingleAdd()
        {
            _buffer.Add(new ModifierElement { Id = 1, Op = ModifierOp.Add, Value = 5f, IsActive = true });
            Assert.AreEqual(15f, ModifierUtility.Evaluate(in _buffer, 10f));
        }

        [Test]
        public void Evaluate_SingleMultiply()
        {
            _buffer.Add(new ModifierElement { Id = 1, Op = ModifierOp.Multiply, Value = 3f, IsActive = true });
            Assert.AreEqual(30f, ModifierUtility.Evaluate(in _buffer, 10f));
        }

        [Test]
        public void Evaluate_SingleOverride()
        {
            _buffer.Add(new ModifierElement { Id = 1, Op = ModifierOp.Override, Value = 42f, IsActive = true });
            Assert.AreEqual(42f, ModifierUtility.Evaluate(in _buffer, 10f));
        }

        [Test]
        public void Evaluate_SingleClampMin()
        {
            _buffer.Add(new ModifierElement { Id = 1, Op = ModifierOp.ClampMin, Value = 15f, IsActive = true });
            Assert.AreEqual(15f, ModifierUtility.Evaluate(in _buffer, 10f));
        }

        [Test]
        public void Evaluate_SingleClampMax()
        {
            _buffer.Add(new ModifierElement { Id = 1, Op = ModifierOp.ClampMax, Value = 5f, IsActive = true });
            Assert.AreEqual(5f, ModifierUtility.Evaluate(in _buffer, 10f));
        }

        [Test]
        public void Evaluate_InactiveModifier_IsSkipped()
        {
            _buffer.Add(new ModifierElement { Id = 1, Op = ModifierOp.Add, Value = 100f, IsActive = false });
            Assert.AreEqual(10f, ModifierUtility.Evaluate(in _buffer, 10f));
        }

        [Test]
        public void Evaluate_PrioritySorting_HigherPriorityFirst()
        {
            // Multiply@10 runs before Add@0: base=10 → *2=20 → +5=25
            _buffer.Add(new ModifierElement { Id = 1, Op = ModifierOp.Add, Value = 5f, Priority = 0, IsActive = true });
            _buffer.Add(new ModifierElement { Id = 2, Op = ModifierOp.Multiply, Value = 2f, Priority = 10, IsActive = true });
            Assert.AreEqual(25f, ModifierUtility.Evaluate(in _buffer, 10f));
        }

        [Test]
        public void Evaluate_SamePriority_BufferOrderPreserved()
        {
            // Both priority=0, buffer order: Add(5) then Multiply(2): 10+5=15, 15*2=30
            _buffer.Add(new ModifierElement { Id = 1, Op = ModifierOp.Add, Value = 5f, Priority = 0, IsActive = true });
            _buffer.Add(new ModifierElement { Id = 2, Op = ModifierOp.Multiply, Value = 2f, Priority = 0, IsActive = true });
            Assert.AreEqual(30f, ModifierUtility.Evaluate(in _buffer, 10f));
        }

        [Test]
        public void Evaluate_MixedActiveInactive_OnlyActiveApplied()
        {
            _buffer.Add(new ModifierElement { Id = 1, Op = ModifierOp.Add, Value = 5f, IsActive = true });
            _buffer.Add(new ModifierElement { Id = 2, Op = ModifierOp.Multiply, Value = 100f, IsActive = false });
            _buffer.Add(new ModifierElement { Id = 3, Op = ModifierOp.Add, Value = 3f, IsActive = true });
            Assert.AreEqual(18f, ModifierUtility.Evaluate(in _buffer, 10f)); // 10+5+3
        }

        // --- Apply ---

        [Test]
        public void Apply_Add_ReturnsSum()
        {
            var mod = new ModifierElement { Op = ModifierOp.Add, Value = 7f };
            Assert.AreEqual(17f, ModifierUtility.Apply(in mod, 10f));
        }

        [Test]
        public void Apply_Multiply_ReturnsProduct()
        {
            var mod = new ModifierElement { Op = ModifierOp.Multiply, Value = 3f };
            Assert.AreEqual(30f, ModifierUtility.Apply(in mod, 10f));
        }

        [Test]
        public void Apply_Override_ReturnsModValue()
        {
            var mod = new ModifierElement { Op = ModifierOp.Override, Value = 42f };
            Assert.AreEqual(42f, ModifierUtility.Apply(in mod, 10f));
        }

        [Test]
        public void Apply_ClampMin_ClampsBelow()
        {
            var mod = new ModifierElement { Op = ModifierOp.ClampMin, Value = 15f };
            Assert.AreEqual(15f, ModifierUtility.Apply(in mod, 10f));
            Assert.AreEqual(20f, ModifierUtility.Apply(in mod, 20f));
        }

        [Test]
        public void Apply_ClampMax_ClampsAbove()
        {
            var mod = new ModifierElement { Op = ModifierOp.ClampMax, Value = 5f };
            Assert.AreEqual(5f, ModifierUtility.Apply(in mod, 10f));
            Assert.AreEqual(3f, ModifierUtility.Apply(in mod, 3f));
        }

        // --- Remove ---

        [Test]
        public void Remove_ExistingModifier_ReturnsTrue()
        {
            _buffer.Add(new ModifierElement { Id = 1, Op = ModifierOp.Add, Value = 5f, IsActive = true });
            Assert.IsTrue(ModifierUtility.Remove(ref _buffer, 1));
            Assert.AreEqual(0, _buffer.Length);
        }

        [Test]
        public void Remove_NonExistentModifier_ReturnsFalse()
        {
            _buffer.Add(new ModifierElement { Id = 1, Op = ModifierOp.Add, Value = 5f, IsActive = true });
            Assert.IsFalse(ModifierUtility.Remove(ref _buffer, 99));
            Assert.AreEqual(1, _buffer.Length);
        }

        // --- Has ---

        [Test]
        public void Has_ExistingModifier_ReturnsTrue()
        {
            _buffer.Add(new ModifierElement { Id = 42, Op = ModifierOp.Add, Value = 1f, IsActive = true });
            Assert.IsTrue(ModifierUtility.Has(in _buffer, 42));
        }

        [Test]
        public void Has_MissingModifier_ReturnsFalse()
        {
            Assert.IsFalse(ModifierUtility.Has(in _buffer, 99));
        }

        // --- SetActive ---

        [Test]
        public void SetActive_TogglesFlag()
        {
            _buffer.Add(new ModifierElement { Id = 1, Op = ModifierOp.Add, Value = 5f, IsActive = true });
            ModifierUtility.SetActive(ref _buffer, 1, false);
            Assert.IsFalse(_buffer[0].IsActive);
            ModifierUtility.SetActive(ref _buffer, 1, true);
            Assert.IsTrue(_buffer[0].IsActive);
        }

        [Test]
        public void SetActive_NonExistent_NoOp()
        {
            _buffer.Add(new ModifierElement { Id = 1, Op = ModifierOp.Add, Value = 5f, IsActive = true });
            ModifierUtility.SetActive(ref _buffer, 99, false);
            Assert.IsTrue(_buffer[0].IsActive); // unchanged
        }

        // --- EvaluateSorted ---

        [Test]
        public void EvaluateSorted_SortsAndEvaluates()
        {
            var active = new NativeList<ModifierElement>(3, Allocator.Temp);
            active.Add(new ModifierElement { Op = ModifierOp.Add, Value = 5f, Priority = 0, IsActive = true });
            active.Add(new ModifierElement { Op = ModifierOp.Multiply, Value = 2f, Priority = 10, IsActive = true });
            // Sorted: Multiply@10 first, Add@0 second → 10*2=20 → 20+5=25
            float result = ModifierUtility.EvaluateSorted(ref active, 10f);
            Assert.AreEqual(25f, result);
            active.Dispose();
        }
    }
}
