using System;
using Unidad.Core.Abstractions;

namespace Unidad.Core.Jobs
{
    /// <summary>
    /// The main-thread end of <see cref="IMainThreadDispatcher"/>: a tickable that runs at most
    /// <see cref="ActionsPerTick"/> posted actions per tick. An action posted while it drains waits
    /// for the next tick, so a chain of posts cannot pin one frame.
    /// </summary>
    public sealed class MainThreadDispatcher : IMainThreadDispatcher, ITickable
    {
        public MainThreadDispatcher(int actionsPerTick) => throw new NotImplementedException();

        public int ActionsPerTick => throw new NotImplementedException();

        public int Pending => throw new NotImplementedException();

        public void Post(Action action) => throw new NotImplementedException();

        public void Tick(float deltaTime) => throw new NotImplementedException();
    }
}
