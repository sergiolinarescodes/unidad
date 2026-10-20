using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unidad.Core.Abstractions;
using Unidad.Core.Diagnostics;
using Unidad.Core.Jobs;
using Unidad.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unidad.Adapter
{
    /// <summary>The only SceneManager caller; a SceneId's value is the scene name in the build profile.</summary>
    public sealed class SceneLoader
    {
        public SceneLoader() { }

        public AsyncOperation Load(SceneId scene)
        {
            if (!Application.CanStreamedLevelBeLoaded(scene.Value))
                throw new InvalidOperationException($"Scene '{scene.Value}' is not in the build profile.");
            return SceneManager.LoadSceneAsync(scene.Value, LoadSceneMode.Single);
        }
    }

    /// <summary>Carries one <see cref="ISceneTicket"/> across a scene switch.</summary>
    public sealed class SceneTicketHolder : MonoBehaviour
    {
        private ISceneTicket _ticket;

        public static SceneTicketHolder FindOrCreate()
        {
            var found = UnityEngine.Object.FindAnyObjectByType<SceneTicketHolder>();
            if (found) return found;
            var holder = new GameObject("[SceneTicket]");
            DontDestroyOnLoad(holder);
            return holder.AddComponent<SceneTicketHolder>();
        }

        /// <summary>Overwrites; null clears.</summary>
        public void Put(ISceneTicket ticket) => _ticket = ticket;

        /// <summary>Read once: clears whatever is parked and returns it if it is a <typeparamref name="T"/>, else null.</summary>
        public T Take<T>() where T : class, ISceneTicket
        {
            var parked = _ticket;
            _ticket = null;
            return parked as T;
        }
    }

    /// <summary>Loads and warms what a scene needs before the curtain lifts, a few steps per frame; ready when nothing is pending.</summary>
    public sealed class ScenePreparation : ITickable, IDisposable
    {
        private readonly AssetLibrary _assets;
        private readonly FrameWorkQueue _work;
        private readonly PreparationBarrier _barrier;
        private readonly List<Action> _release = new();
        private readonly List<IDisposable> _batches = new();
        private readonly Action<Exception> _failed;
        private bool _disposed, _reported;

        public ScenePreparation(AssetLibrary assets, Action<Exception> failed = null, FrameWorkTuning tuning = null)
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _failed = failed;
            tuning ??= new FrameWorkTuning();
            _work = new FrameWorkQueue(() => Stopwatch.GetTimestamp() * (1000.0 / Stopwatch.Frequency), tuning);
            _barrier = new PreparationBarrier(tuning);
        }

        public bool IsReady => !_disposed && _work.Pending == 0 && _work.Failure == null && _barrier.Ready && _release.Count == 0;

        public int Pending => _work.Pending;

        public Exception Failure => _work.Failure;

        public void Enqueue(string key, Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _work.Enqueue(key, () =>
            {
                using (_assets.AllowColdLoads()) action();
                return true;
            });
        }

        public void Add(string key, IEnumerable<Action> steps, Action release = null)
        {
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            var batch = steps.GetEnumerator();
            var added = _work.Enqueue(key, () =>
            {
                using (_assets.AllowColdLoads())
                {
                    if (batch.MoveNext()) { batch.Current(); return false; }
                }
                batch.Dispose();
                _batches.Remove(batch);
                return true;
            });
            if (!added) { batch.Dispose(); return; }
            _batches.Add(batch);
            if (release != null) _release.Add(release);
        }

        public void Tick(float deltaTime)
        {
            if (_disposed) return;
            _work.Tick();
            if (_disposed) return;
            if (_work.Failure != null && !_reported)
            {
                _reported = true;
                Log.Error($"[Preparation] '{_work.FailedKey}' failed: {_work.Failure}");
                _failed?.Invoke(new InvalidOperationException($"Preparation '{_work.FailedKey}' failed: {_work.Failure.Message}", _work.Failure));
            }
            // Unity has no pipeline-compile counter; the barrier sees zero compiles.
            _barrier.Tick(_work, deltaTime, 0);
            if (!_barrier.Ready || _release.Count == 0) return;
            var release = _release.ToArray();
            _release.Clear();
            foreach (var finish in release) finish();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _work.Dispose();
            foreach (var batch in _batches) batch.Dispose();
            _batches.Clear();
            foreach (var finish in _release) finish();
            _release.Clear();
        }
    }
}
