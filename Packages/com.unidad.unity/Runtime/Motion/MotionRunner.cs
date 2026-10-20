using System;
using Unidad.Adapter.Motion;
using Unidad.Core.Abstractions;
using Unidad.Core.Motion;
using UnityEngine;

namespace Unidad.Adapter
{
    internal sealed class MotionRunner : IMotionRunner, ITickable
    {
        public MotionRunner(ITickRegistry ticks, ITimeProvider time) => throw new NotImplementedException();

        public bool SkipAll
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public int Active => throw new NotImplementedException();

        public MotionHandle Play<T>(T target, IMotionTarget<T> adapter, Vector4 to, MotionSpec spec, string channel,
            MotionCurve curve = default, bool runWhilePaused = false, Action onComplete = null) where T : class
            => throw new NotImplementedException();

        public bool IsPlaying(MotionHandle handle) => throw new NotImplementedException();

        public bool IsPlaying(object owner, string channel) => throw new NotImplementedException();

        public void Cancel(object owner, string channel) => throw new NotImplementedException();

        public void Complete(object owner, string channel) => throw new NotImplementedException();

        public void CancelAll(object owner) => throw new NotImplementedException();

        public void Tick(float deltaTime) => throw new NotImplementedException();
    }
}
