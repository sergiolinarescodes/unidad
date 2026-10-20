using System;
using System.Threading;

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
        /// <param name="startWorker">Runs one job's body off the main thread; the thread pool when null.
        /// A test passes one that collects the bodies and runs them when it chooses.</param>
        public JobQueue(IMainThreadDispatcher dispatcher, int maxConcurrency, Action<Action> startWorker = null)
            => throw new NotImplementedException();

        public int MaxConcurrency => throw new NotImplementedException();

        public int Waiting => throw new NotImplementedException();

        public int Running => throw new NotImplementedException();

        public int Pending => throw new NotImplementedException();

        public IJob<T> Enqueue<T>(Func<CancellationToken, T> work, Action<JobResult<T>> onComplete = null,
                                  CancellationToken cancellation = default)
            => throw new NotImplementedException();

        public void CancelAll() => throw new NotImplementedException();

        public void Dispose() => throw new NotImplementedException();
    }
}
