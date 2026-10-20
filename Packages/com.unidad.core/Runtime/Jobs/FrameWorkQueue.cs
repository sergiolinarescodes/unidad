using System;
using System.Collections.Generic;

namespace Unidad.Core.Jobs;

/// <summary>A scene-owned, coalescing main-thread queue. Each step is an indivisible small unit; false reschedules it
/// behind other owners. CPU-heavy pure work belongs on IJobQueue. A time limit cannot preempt an individual step.</summary>
public sealed class FrameWorkQueue : IDisposable
{
    private sealed record Work(string Key, Func<bool> Step);
    private readonly Queue<Work> _queue = new();
    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);
    private readonly Func<double> _milliseconds;
    private readonly FrameWorkTuning _tuning;
    private bool _disposed;
    public int Pending => _queue.Count;
    public int Generation { get; private set; }
    public Exception Failure { get; private set; }
    public string FailedKey { get; private set; }

    public FrameWorkQueue(Func<double> milliseconds, FrameWorkTuning tuning = null)
    {
        _milliseconds = milliseconds ?? throw new ArgumentNullException(nameof(milliseconds));
        _tuning = tuning ?? new();
        _tuning.Validate();
    }

    public bool Enqueue(string key, Func<bool> step)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FrameWorkQueue));
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Work needs an owner/key.", nameof(key));
        if (step == null) throw new ArgumentNullException(nameof(step));
        if (_keys.Contains(key)) return false;
        if (_keys.Count >= _tuning.MaxPending) throw new InvalidOperationException($"Frame work capacity exceeded by '{key}'.");
        _keys.Add(key);
        _queue.Enqueue(new(key, step));
        Generation++;
        return true;
    }

    public int Tick()
    {
        if (_disposed || Failure != null) return 0;
        double start = _milliseconds();
        int limit = Math.Min(_queue.Count, _tuning.MaxStepsPerFrame), done = 0;
        while (done < limit && _queue.TryDequeue(out var work))
        {
            try
            {
                bool complete = work.Step();
                if (_disposed) return done + 1;
                if (complete) _keys.Remove(work.Key);
                else _queue.Enqueue(work);
            }
            catch (Exception e)
            {
                Failure = e;
                FailedKey = work.Key;
                return done + 1;
            }
            done++;
            if (_milliseconds() - start >= _tuning.MillisecondsPerFrame) break;
        }
        return done;
    }

    public void Dispose()
    {
        _disposed = true;
        _queue.Clear();
        _keys.Clear();
    }
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
    private readonly FrameWorkTuning _tuning;
    private int _generation = -1, _quiet;
    private ulong _compiles;
    private float _elapsed;
    public bool Ready { get; private set; }
    public bool TimedOut { get; private set; }
    public PreparationBarrier(FrameWorkTuning tuning) { tuning.Validate(); _tuning = tuning; }

    public void Tick(FrameWorkQueue work, float dt, ulong compiles)
    {
        if (_generation != work.Generation || work.Pending > 0 || work.Failure != null)
        {
            _generation = work.Generation;
            _quiet = 0;
            _elapsed = 0;
            _compiles = compiles;
            Ready = TimedOut = false;
            return;
        }
        if (Ready) return;
        _elapsed += float.IsFinite(dt) ? Math.Clamp(dt, 0, .1f) : 0;
        _quiet = compiles == _compiles ? _quiet + 1 : 0;
        _compiles = compiles;
        TimedOut = _elapsed >= _tuning.PipelineTimeoutSeconds;
        Ready = _quiet >= _tuning.PipelineQuietFrames || TimedOut;
    }
}
