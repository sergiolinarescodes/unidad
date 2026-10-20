using System;

namespace Unidad.Core.Jobs
{
    /// <summary>
    /// A finished job as a value: the result, or the exception it threw, or that it was cancelled.
    /// A faulted job is read, never rethrown.
    /// </summary>
    public readonly struct JobResult<T>
    {
        public JobResult(JobStatus status, T value, Exception error)
        {
            Status = status;
            Value = value;
            Error = error;
        }

        public JobStatus Status { get; }

        /// <summary>The work's return value; default unless <see cref="Succeeded"/>.</summary>
        public T Value { get; }

        /// <summary>What the work threw; null unless <see cref="Faulted"/>.</summary>
        public Exception Error { get; }

        public bool Succeeded => Status == JobStatus.Succeeded;
        public bool Faulted => Status == JobStatus.Faulted;
        public bool Cancelled => Status == JobStatus.Cancelled;
    }
}
