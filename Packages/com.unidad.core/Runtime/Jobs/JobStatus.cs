namespace Unidad.Core.Jobs
{
    /// <summary>Where a job is. The three terminal states are set on the main thread, at delivery.</summary>
    public enum JobStatus
    {
        Waiting = 0,
        Running = 1,
        Succeeded = 2,
        Faulted = 3,
        Cancelled = 4,
    }
}
