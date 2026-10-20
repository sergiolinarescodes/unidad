using System;

namespace Unidad.Core.Jobs;

/// <summary>A scene-owned, coalescing main-thread queue. Each step is an indivisible small unit; false reschedules it
/// behind other owners. CPU-heavy pure work belongs on IJobQueue. A time limit cannot preempt an individual step.</summary>
public sealed class FrameWorkQueue : IDisposable
{
    public int Pending => throw new NotImplementedException();
    public int Generation => throw new NotImplementedException();
    public Exception Failure => throw new NotImplementedException();
    public string FailedKey => throw new NotImplementedException();

    public FrameWorkQueue(Func<double> milliseconds, FrameWorkTuning tuning = null) => throw new NotImplementedException();

    public bool Enqueue(string key, Func<bool> step) => throw new NotImplementedException();

    public int Tick() => throw new NotImplementedException();

    public void Dispose() => throw new NotImplementedException();
}

public sealed record FrameWorkTuning
{
    public int MaxStepsPerFrame { get; init; } = 4;
    public double MillisecondsPerFrame { get; init; } = 2;
    public int MaxPending { get; init; } = 2048;
    public int PipelineQuietFrames { get; init; } = 12;
    public float PipelineTimeoutSeconds { get; init; } = 5;
    public void Validate()
    {
        if (MaxStepsPerFrame < 1 || MaxPending < 1 || !double.IsFinite(MillisecondsPerFrame) || MillisecondsPerFrame <= 0
            || PipelineQuietFrames < 1 || !float.IsFinite(PipelineTimeoutSeconds) || PipelineTimeoutSeconds <= 0)
            throw new ArgumentException("Invalid frame work budgets.");
    }
}

/// <summary>One pipeline barrier shared by every preparation contributor. New work reopens it; failures never read as ready.</summary>
public sealed class PreparationBarrier
{
    public bool Ready => throw new NotImplementedException();
    public bool TimedOut => throw new NotImplementedException();
    public PreparationBarrier(FrameWorkTuning tuning) => throw new NotImplementedException();

    public void Tick(FrameWorkQueue work, float dt, ulong compiles) => throw new NotImplementedException();
}
