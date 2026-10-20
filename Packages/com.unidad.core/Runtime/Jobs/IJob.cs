namespace Unidad.Core.Jobs
{
    /// <summary>A handle on one enqueued job. Poll it from a tickable, or pass a completion callback.</summary>
    public interface IJob<T>
    {
        JobStatus Status { get; }

        /// <summary>True once the result has been delivered on the main thread.</summary>
        bool IsDone { get; }

        /// <summary>The delivered result; a Waiting/Running status with a default value until <see cref="IsDone"/>.</summary>
        JobResult<T> Result { get; }

        /// <summary>Signals the job's token; a waiting job is dropped and delivered as cancelled.</summary>
        void Cancel();
    }
}
