using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unidad.Core.Diagnostics;

namespace Unidad.Core.Jobs
{
    /// <summary>
    /// The <see cref="IJobQueue"/>: a FIFO of pure work, at most <see cref="MaxConcurrency"/> running,
    /// each result delivered through the <see cref="IMainThreadDispatcher"/>. Nothing here rethrows:
    /// a faulted job is a <see cref="JobResult{T}"/> with an <see cref="JobResult{T}.Error"/>, and a
    /// throwing completion callback is logged. Disposing cancels everything and runs no further callback.
    /// </summary>
    public sealed class JobQueue : IJobQueue, IDisposable
    {
        private readonly object _gate = new();
        private readonly List<JobBase> _waiting = new();
        private readonly HashSet<JobBase> _running = new();
        private readonly IMainThreadDispatcher _dispatcher;
        private readonly Action<Action> _startWorker;
        private int _pending;
        private volatile bool _disposed;

        /// <param name="startWorker">Runs one job's body off the main thread; the thread pool when null.
        /// A test passes one that collects the bodies and runs them when it chooses.</param>
        public JobQueue(IMainThreadDispatcher dispatcher, int maxConcurrency, Action<Action> startWorker = null)
        {
            if (maxConcurrency < 1)
                throw new ArgumentOutOfRangeException(nameof(maxConcurrency), maxConcurrency, "At least one job at a time.");
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            MaxConcurrency = maxConcurrency;
            _startWorker = startWorker ?? (body => Task.Run(body));
        }

        public int MaxConcurrency { get; }

        public int Waiting { get { lock (_gate) return _waiting.Count; } }

        public int Running { get { lock (_gate) return _running.Count; } }

        public int Pending => Volatile.Read(ref _pending);

        public IJob<T> Enqueue<T>(Func<CancellationToken, T> work, Action<JobResult<T>> onComplete = null,
                                  CancellationToken cancellation = default)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));
            if (_disposed) throw new ObjectDisposedException(nameof(JobQueue));

            var job = new Job<T>(this, work, onComplete, cancellation);
            Interlocked.Increment(ref _pending);
            List<JobBase> start;
            lock (_gate)
            {
                _waiting.Add(job);
                start = TakeStartable();
            }
            Start(start);
            return job;
        }

        public void CancelAll()
        {
            lock (_gate)
            {
                foreach (var job in _waiting) DropCancelled(job);
                _waiting.Clear();
                foreach (var job in _running) job.RequestCancel();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CancelAll();
        }

        internal bool IsDisposed => _disposed;

        internal void Cancel(JobBase job)
        {
            lock (_gate)
            {
                if (_waiting.Remove(job)) DropCancelled(job);
                else job.RequestCancel();
            }
        }

        internal void Delivered() => Interlocked.Decrement(ref _pending);

        private void DropCancelled(JobBase job)
        {
            job.MarkCancelled();
            _dispatcher.Post(job.Deliver);
        }

        // Under _gate. The caller starts the returned jobs after releasing it.
        private List<JobBase> TakeStartable()
        {
            List<JobBase> start = null;
            while (_running.Count < MaxConcurrency && _waiting.Count > 0)
            {
                var job = _waiting[0];
                _waiting.RemoveAt(0);
                if (job.IsCancellationRequested)
                {
                    DropCancelled(job);
                    continue;
                }
                job.MarkRunning();
                _running.Add(job);
                (start ??= new List<JobBase>()).Add(job);
            }
            return start;
        }

        private void Start(List<JobBase> jobs)
        {
            if (jobs == null) return;
            foreach (var job in jobs)
            {
                try
                {
                    _startWorker(() => Execute(job));
                }
                catch (Exception ex)
                {
                    job.MarkFaulted(ex);
                    Finished(job);
                }
            }
        }

        private void Execute(JobBase job)
        {
            job.Run();
            Finished(job);
        }

        private void Finished(JobBase job)
        {
            List<JobBase> start;
            lock (_gate)
            {
                _running.Remove(job);
                start = TakeStartable();
            }
            _dispatcher.Post(job.Deliver);
            Start(start);
        }

        internal abstract class JobBase
        {
            public abstract bool IsCancellationRequested { get; }
            public abstract void MarkRunning();
            public abstract void MarkCancelled();
            public abstract void MarkFaulted(Exception error);
            public abstract void RequestCancel();
            public abstract void Run();
            public abstract void Deliver();
        }

        private sealed class Job<T> : JobBase, IJob<T>
        {
            private readonly JobQueue _queue;
            private readonly Func<CancellationToken, T> _work;
            private readonly Action<JobResult<T>> _onComplete;
            private readonly CancellationTokenSource _cancellation;
            private int _status;
            private JobStatus _outcome;
            private T _value;
            private Exception _error;
            private JobResult<T> _result;

            public Job(JobQueue queue, Func<CancellationToken, T> work, Action<JobResult<T>> onComplete, CancellationToken external)
            {
                _queue = queue;
                _work = work;
                _onComplete = onComplete;
                _cancellation = CancellationTokenSource.CreateLinkedTokenSource(external);
            }

            public JobStatus Status => (JobStatus)Volatile.Read(ref _status);

            public bool IsDone => Status >= JobStatus.Succeeded;

            public JobResult<T> Result => IsDone ? _result : new JobResult<T>(Status, default, null);

            public void Cancel()
            {
                if (!IsDone) _queue.Cancel(this);
            }

            public override bool IsCancellationRequested => _cancellation.IsCancellationRequested;

            public override void MarkRunning() => Volatile.Write(ref _status, (int)JobStatus.Running);

            public override void MarkCancelled() => _outcome = JobStatus.Cancelled;

            public override void MarkFaulted(Exception error)
            {
                _error = error;
                _outcome = JobStatus.Faulted;
            }

            public override void RequestCancel()
            {
                try { _cancellation.Cancel(); }
                catch (AggregateException ex) { Log.Exception(ex); }
            }

            public override void Run()
            {
                var token = _cancellation.Token;
                try
                {
                    var value = _work(token);
                    if (token.IsCancellationRequested) _outcome = JobStatus.Cancelled;
                    else
                    {
                        _value = value;
                        _outcome = JobStatus.Succeeded;
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    _outcome = JobStatus.Cancelled;
                }
                catch (Exception ex)
                {
                    MarkFaulted(ex);
                }
            }

            public override void Deliver()
            {
                var outcome = _queue.IsDisposed ? JobStatus.Cancelled : _outcome;
                _result = new JobResult<T>(outcome, outcome == JobStatus.Succeeded ? _value : default,
                                           outcome == JobStatus.Faulted ? _error : null);
                Volatile.Write(ref _status, (int)outcome);
                _cancellation.Dispose();
                _queue.Delivered();

                if (outcome == JobStatus.Faulted)
                    Log.Warning($"[JobQueue] a job faulted and is delivered as a value. {_error.GetType().Name}: {_error.Message}");
                if (_onComplete == null || _queue.IsDisposed) return;
                try
                {
                    _onComplete(_result);
                }
                catch (Exception ex)
                {
                    Log.Error($"[JobQueue] a completion callback threw. {ex.GetType().Name}: {ex.Message}");
                    Log.Exception(ex);
                }
            }
        }
    }
}
