using System;
using System.Threading;

namespace Unidad.Core.Jobs
{
    /// <summary>
    /// Bounded background work. Pure work runs off the main thread, at most
    /// <see cref="MaxConcurrency"/> at a time; its result comes back on the main thread through
    /// <see cref="IMainThreadDispatcher"/>, a few per frame. Enqueue from the main thread.
    /// </summary>
    public interface IJobQueue
    {
        int MaxConcurrency { get; }

        /// <summary>Enqueued, not started.</summary>
        int Waiting { get; }

        /// <summary>Started, not finished.</summary>
        int Running { get; }

        /// <summary>Enqueued and not yet delivered: waiting, running, or finished and waiting for the main thread.</summary>
        int Pending { get; }

        /// <summary>
        /// Queues <paramref name="work"/>. It receives a token that is cancelled by
        /// <see cref="IJob{T}.Cancel"/>, <see cref="CancelAll"/>, disposal, or <paramref name="cancellation"/>;
        /// it must touch no engine object and no main-thread state. <paramref name="onComplete"/> runs on
        /// the main thread with the result, whatever it is.
        /// </summary>
        IJob<T> Enqueue<T>(Func<CancellationToken, T> work, Action<JobResult<T>> onComplete = null,
                           CancellationToken cancellation = default);

        /// <summary>Cancels every job not yet delivered.</summary>
        void CancelAll();
    }
}
