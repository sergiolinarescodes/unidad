using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Unidad.Core.Diagnostics;
using Unidad.Core.EventBus;
using Unidad.Core.Systems;

namespace Unidad.Save
{
    /// <summary>
    /// The one save slot: gathers every participant's section into a versioned document, and reads one back whole or not at
    /// all. Sections no participant claims are kept and written back.
    /// </summary>
    internal sealed class SaveService : SystemServiceBase, ISaveService
    {
        private readonly JsonSerializerOptions _indented = new() { WriteIndented = true };
        private readonly ISaveStore _store;
        private readonly List<ISaveParticipant> _participants = new();
        private readonly List<Func<bool>> _holds = new();
        private readonly List<KeyValuePair<string, JsonNode>> _kept = new();
        private bool _looked, _refused, _warned, _requested;

        public SaveService(IEventBus eventBus, ISaveStore store) : base(eventBus)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public bool CanWrite => _looked && !_refused;

        public bool Pending { get; private set; }

        public bool HasSave
        {
            get
            {
                try
                {
                    return _store.Read() != null;
                }
                catch (IOException)
                {
                    return true;
                }
            }
        }

        public IDisposable HoldWhile(Func<bool> busy)
        {
            if (busy == null) throw new ArgumentNullException(nameof(busy));
            _holds.Add(busy);
            return new Release(() => _holds.Remove(busy));
        }

        /// <summary>Writes a requested save, or a deferred one on the first frame no hold holds.</summary>
        public void Tick(float deltaTime)
        {
            if (_requested)
            {
                _requested = false;
                Save();
            }
            else if (Pending && !Held()) Save();
        }

        public void Register(ISaveParticipant participant)
        {
            if (participant == null) throw new ArgumentNullException(nameof(participant));
            if (string.IsNullOrWhiteSpace(participant.Key)) throw new ArgumentException("A save participant needs a key.", nameof(participant));
            foreach (var p in _participants)
                if (p.Key == participant.Key) throw new ArgumentException($"Two save participants share the key '{participant.Key}'.", nameof(participant));
            _participants.Add(participant);
        }

        public SaveLoadResult Load()
        {
            var result = Read();
            _looked = true;
            _refused = result.Outcome is SaveLoadOutcome.RefusedFutureVersion or SaveLoadOutcome.Unreadable;
            if (_refused) Log.Warning($"[Save] {_store.Location}: {result.Message} Nothing was loaded, and the game will not overwrite it.");
            Publish(new GameLoadedEvent(result));
            return result;
        }

        public SaveLoadResult StartFresh()
        {
            _looked = true;
            _refused = false;
            _kept.Clear();
            var result = new SaveLoadResult(SaveLoadOutcome.NoSave, 0, "fresh start");
            Publish(new GameLoadedEvent(result));
            return result;
        }

        public bool Save()
        {
            if (!CanWrite)
            {
                if (!_warned) Log.Warning($"[Save] Not saving to {_store.Location}: " + (_looked ? "it holds a save this build cannot read." : "it has not been loaded yet."));
                _warned = true;
                return false;
            }
            if (Held())
            {
                Pending = true;
                return false;
            }
            Pending = false;
            var sections = new JsonObject();
            foreach (var kept in _kept) sections[kept.Key] = kept.Value?.DeepClone();
            foreach (var p in _participants) sections[p.Key] = p.Capture();
            var document = new JsonObject { [SaveFormat.VersionField] = SaveFormat.CurrentVersion, [SaveFormat.SectionsField] = sections };
            try
            {
                _store.Write(document.ToJsonString(_indented));
            }
            catch (IOException ex)
            {
                Log.Error($"[Save] {_store.Location}: {ex.Message}");
                return false;
            }
            Publish(new GameSavedEvent(SaveFormat.CurrentVersion));
            return true;
        }

        public IDisposable AutosaveOn<TEvent>(Func<TEvent, bool> when = null) where TEvent : struct =>
            Track(EventBus.Subscribe<TEvent>(e =>
            {
                if (when == null || when(e)) Save();
            }));

        public IDisposable AutosaveAfter<TEvent>(Func<TEvent, bool> when = null) where TEvent : struct =>
            Track(EventBus.Subscribe<TEvent>(e =>
            {
                if (when == null || when(e)) _requested = true;
            }));

        private IDisposable Track(IDisposable subscription)
        {
            Subscriptions.Add(subscription);
            return subscription;
        }

        private bool Held()
        {
            foreach (var hold in _holds)
                if (hold()) return true;
            return false;
        }

        private SaveLoadResult Read()
        {
            string text;
            try
            {
                text = _store.Read();
            }
            catch (IOException ex)
            {
                return Unreadable(0, ex.Message);
            }
            if (text == null) return new SaveLoadResult(SaveLoadOutcome.NoSave, 0, "");
            JsonNode root;
            try
            {
                root = JsonNode.Parse(text);
            }
            catch (JsonException ex)
            {
                return Unreadable(0, $"not JSON ({ex.Message}).");
            }
            if (root is not JsonObject document) return Unreadable(0, "not a save document.");
            if (document[SaveFormat.VersionField] is not JsonValue v || v.GetValueKind() != JsonValueKind.Number || !v.TryGetValue(out int version))
                return Unreadable(0, "it has no whole version number.");
            if (version > SaveFormat.CurrentVersion)
                return new SaveLoadResult(SaveLoadOutcome.RefusedFutureVersion, version,
                    $"save version {version} is newer than this build reads ({SaveFormat.CurrentVersion}).");
            if (version < 1) return Unreadable(version, $"save version {version} does not exist.");
            if (document[SaveFormat.SectionsField] is not JsonObject sections) return Unreadable(version, "it has no sections.");

            // Every section is read before any is applied: one bad section refuses the whole save.
            var apply = new List<Action>(_participants.Count);
            var claimed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in _participants)
            {
                claimed.Add(p.Key);
                if (!sections.TryGetPropertyValue(p.Key, out var section)) continue;
                try
                {
                    apply.Add(p.Read(section ?? throw new SaveFormatException("it is null.")));
                }
                catch (Exception ex) when (ex is SaveFormatException or InvalidOperationException or FormatException)
                {
                    return Unreadable(version, $"section '{p.Key}': {ex.Message}");
                }
            }
            foreach (var a in apply) a();
            _kept.Clear();
            foreach (var section in sections)
                if (!claimed.Contains(section.Key))
                    _kept.Add(new KeyValuePair<string, JsonNode>(section.Key, section.Value?.DeepClone()));
            return new SaveLoadResult(SaveLoadOutcome.Loaded, version, "");
        }

        private static SaveLoadResult Unreadable(int version, string message) => new(SaveLoadOutcome.Unreadable, version, "unreadable: " + message);
    }

    /// <summary>Runs its action once, on the first Dispose.</summary>
    internal sealed class Release : IDisposable
    {
        private Action _release;

        public Release(Action release) => _release = release;

        public void Dispose()
        {
            _release?.Invoke();
            _release = null;
        }
    }
}
