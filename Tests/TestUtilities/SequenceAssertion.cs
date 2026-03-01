using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unidad.Core.HistoryService;
using Unidad.Core.HistoryService.Data;
using UnityEngine;

namespace Unidad.Core.Tests.Tests.TestUtilities
{
    /// <summary>
    /// Fluent assertion helper for verifying event sequences in history.
    /// </summary>
    public sealed class SequenceAssertion
    {
        private readonly IHistoryService _history;
        private readonly List<Expectation> _expectations = new();
        private string _forEntity;
        private Vector2Int? _forPosition;
        private int? _atTick;
        private int? _fromTick;
        private int? _toTick;

        internal SequenceAssertion(IHistoryService history)
        {
            _history = history;
        }

        /// <summary>Expect an event of type T next in the sequence.</summary>
        public SequenceAssertion Then<T>() where T : struct
        {
            _expectations.Add(new Expectation
            {
                TypeName = typeof(T).Name,
                Predicate = e => e.Is<T>()
            });
            return this;
        }

        /// <summary>Expect an event of type T matching a predicate.</summary>
        public SequenceAssertion Then<T>(Func<T, bool> predicate) where T : struct
        {
            _expectations.Add(new Expectation
            {
                TypeName = typeof(T).Name,
                Predicate = e => e.Is<T>() && predicate(e.GetEvent<T>())
            });
            return this;
        }

        /// <summary>Expect any event matching the predicate.</summary>
        public SequenceAssertion ThenAny(Func<HistoryEntry, bool> predicate)
        {
            _expectations.Add(new Expectation
            {
                TypeName = "Any",
                Predicate = predicate
            });
            return this;
        }

        /// <summary>Filter to events for a specific entity.</summary>
        public SequenceAssertion ForEntity(string entityId)
        {
            _forEntity = entityId;
            return this;
        }

        /// <summary>Filter to events at a specific position.</summary>
        public SequenceAssertion ForPosition(Vector2Int position)
        {
            _forPosition = position;
            return this;
        }

        /// <summary>Filter to events at a specific tick.</summary>
        public SequenceAssertion AtTick(int tick)
        {
            _atTick = tick;
            return this;
        }

        /// <summary>Filter to events in a tick range.</summary>
        public SequenceAssertion InTickRange(int fromTick, int toTick)
        {
            _fromTick = fromTick;
            _toTick = toTick;
            return this;
        }

        /// <summary>Verify the sequence matches recorded history (strict order).</summary>
        public void Verify()
        {
            var entries = ExecuteQuery();

            Assert.That(entries.Count, Is.GreaterThanOrEqualTo(_expectations.Count),
                $"Expected at least {_expectations.Count} events, found {entries.Count}.\n" +
                $"Found events: {string.Join(", ", entries.Select(e => e.EventTypeName))}");

            for (int i = 0; i < _expectations.Count; i++)
            {
                var expectation = _expectations[i];
                var entry = entries[i];

                Assert.That(expectation.Predicate(entry), Is.True,
                    $"Event at index {i} did not match expectation.\n" +
                    $"Expected: {expectation.TypeName}\n" +
                    $"Got: {entry.EventTypeName}\n" +
                    $"Entry details: {entry}");
            }
        }

        /// <summary>
        /// Verify the sequence matches in order but allows other events in between.
        /// </summary>
        public void VerifyContainsInOrder()
        {
            var entries = ExecuteQuery();
            int expectationIndex = 0;

            foreach (var entry in entries)
            {
                if (expectationIndex >= _expectations.Count)
                    break;

                if (_expectations[expectationIndex].Predicate(entry))
                    expectationIndex++;
            }

            if (expectationIndex < _expectations.Count)
            {
                Assert.Fail(
                    $"Not all expected events were found in order.\n" +
                    $"Found {expectationIndex} of {_expectations.Count} expected events.\n" +
                    $"Missing from: {_expectations[expectationIndex].TypeName}\n" +
                    $"Available events: {string.Join(", ", entries.Select(e => e.EventTypeName))}");
            }
        }

        private List<HistoryEntry> ExecuteQuery()
        {
            var query = _history.Query();

            if (_forEntity != null)
                query = query.ForEntity(_forEntity);
            if (_forPosition.HasValue)
                query = query.ForPosition(_forPosition.Value);
            if (_atTick.HasValue)
                query = query.AtTick(_atTick.Value);
            if (_fromTick.HasValue && _toTick.HasValue)
                query = query.InTickRange(_fromTick.Value, _toTick.Value);

            return query.Execute().ToList();
        }

        private sealed class Expectation
        {
            public string TypeName { get; init; }
            public Func<HistoryEntry, bool> Predicate { get; init; }
        }
    }
}
