using NUnit.Framework;
using Unidad.Core.Patterns.Modifier;

namespace Unidad.Core.Tests.Patterns
{
    [TestFixture]
    public class ModifierStackTests
    {
        private sealed class TestModifier : IModifier<float>
        {
            public string Id { get; }
            public int Priority { get; }
            public bool IsActive { get; set; } = true;
            private readonly float _addAmount;

            public TestModifier(string id, int priority, float addAmount)
            {
                Id = id;
                Priority = priority;
                _addAmount = addAmount;
            }

            public float Apply(float value) => value + _addAmount;
        }

        private sealed class MultiplyModifier : IModifier<float>
        {
            public string Id { get; }
            public int Priority { get; }
            public bool IsActive { get; set; } = true;
            private readonly float _factor;

            public MultiplyModifier(string id, int priority, float factor)
            {
                Id = id;
                Priority = priority;
                _factor = factor;
            }

            public float Apply(float value) => value * _factor;
        }

        [Test]
        public void Evaluate_WithNoModifiers_ReturnsBaseValue()
        {
            var stack = new ModifierStack<float>();
            Assert.That(stack.Evaluate(10f), Is.EqualTo(10f));
        }

        [Test]
        public void Evaluate_AppliesModifierToBaseValue()
        {
            var stack = new ModifierStack<float>();
            stack.Add(new TestModifier("bonus", 0, 5f));

            Assert.That(stack.Evaluate(10f), Is.EqualTo(15f));
        }

        [Test]
        public void Evaluate_AppliesModifiersInPriorityOrder_HighestFirst()
        {
            var stack = new ModifierStack<float>();
            // Add +5 at priority 0, then multiply by 2 at priority 10
            // Priority 10 runs first: base * 2 = 20, then +5 = 25
            stack.Add(new TestModifier("add", 0, 5f));
            stack.Add(new MultiplyModifier("multiply", 10, 2f));

            Assert.That(stack.Evaluate(10f), Is.EqualTo(25f));
        }

        [Test]
        public void Evaluate_SkipsInactiveModifiers()
        {
            var stack = new ModifierStack<float>();
            var mod = new TestModifier("disabled", 0, 100f) { IsActive = false };
            stack.Add(mod);

            Assert.That(stack.Evaluate(10f), Is.EqualTo(10f));
        }

        [Test]
        public void Remove_RemovesModifierById()
        {
            var stack = new ModifierStack<float>();
            stack.Add(new TestModifier("a", 0, 5f));
            stack.Add(new TestModifier("b", 0, 10f));

            Assert.That(stack.Remove("a"), Is.True);
            Assert.That(stack.Evaluate(10f), Is.EqualTo(20f));
        }

        [Test]
        public void Remove_ReturnsFalseForUnknownId()
        {
            var stack = new ModifierStack<float>();
            Assert.That(stack.Remove("nonexistent"), Is.False);
        }

        [Test]
        public void Has_ReturnsTrueForExistingModifier()
        {
            var stack = new ModifierStack<float>();
            stack.Add(new TestModifier("test", 0, 1f));

            Assert.That(stack.Has("test"), Is.True);
            Assert.That(stack.Has("other"), Is.False);
        }

        [Test]
        public void Clear_RemovesAllModifiers()
        {
            var stack = new ModifierStack<float>();
            stack.Add(new TestModifier("a", 0, 5f));
            stack.Add(new TestModifier("b", 0, 10f));
            stack.Clear();

            Assert.That(stack.Evaluate(10f), Is.EqualTo(10f));
            Assert.That(stack.Modifiers.Count, Is.EqualTo(0));
        }

        [Test]
        public void Modifiers_ReturnsReadOnlyList()
        {
            var stack = new ModifierStack<float>();
            stack.Add(new TestModifier("a", 0, 5f));
            stack.Add(new TestModifier("b", 0, 10f));

            Assert.That(stack.Modifiers.Count, Is.EqualTo(2));
        }

        // --- Context-aware variant ---

        private sealed class ContextModifier : IModifier<float, string>
        {
            public string Id { get; }
            public int Priority { get; }
            public bool IsActive { get; set; } = true;
            private readonly float _bonus;

            public ContextModifier(string id, int priority, float bonus)
            {
                Id = id;
                Priority = priority;
                _bonus = bonus;
            }

            public float Apply(float value, string context)
            {
                return context == "boosted" ? value + _bonus * 2 : value + _bonus;
            }
        }

        [Test]
        public void ContextStack_Evaluate_PassesContextToModifiers()
        {
            var stack = new ModifierStack<float, string>();
            stack.Add(new ContextModifier("ctx", 0, 5f));

            Assert.That(stack.Evaluate(10f, "normal"), Is.EqualTo(15f));
            Assert.That(stack.Evaluate(10f, "boosted"), Is.EqualTo(20f));
        }

        [Test]
        public void ContextStack_Remove_Works()
        {
            var stack = new ModifierStack<float, string>();
            stack.Add(new ContextModifier("a", 0, 5f));

            Assert.That(stack.Remove("a"), Is.True);
            Assert.That(stack.Evaluate(10f, "normal"), Is.EqualTo(10f));
        }

        [Test]
        public void ContextStack_SkipsInactiveModifiers()
        {
            var stack = new ModifierStack<float, string>();
            stack.Add(new ContextModifier("off", 0, 100f) { IsActive = false });

            Assert.That(stack.Evaluate(10f, "normal"), Is.EqualTo(10f));
        }

        [Test]
        public void ContextStack_Clear_Works()
        {
            var stack = new ModifierStack<float, string>();
            stack.Add(new ContextModifier("a", 0, 5f));
            stack.Clear();

            Assert.That(stack.Modifiers.Count, Is.EqualTo(0));
        }
    }
}
