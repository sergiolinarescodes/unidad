using System;

namespace Unidad.Core.Timer
{
    public interface ITimerService
    {
        TimerHandle Start(float duration, Action onComplete = null, bool loop = false);
        void Cancel(TimerHandle handle);
        void Pause(TimerHandle handle);
        void Resume(TimerHandle handle);
        float GetRemaining(TimerHandle handle);
        float GetProgress(TimerHandle handle);
        bool IsRunning(TimerHandle handle);
    }
}
