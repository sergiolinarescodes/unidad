using System;

namespace Unidad.Core.Jobs
{
    /// <summary>
    /// Hands work to the main thread. <see cref="Post"/> is safe from any thread; the actions run
    /// in order during the frame's first tick group, a bounded number per frame, and an action that
    /// throws is logged and never stops the ones after it.
    /// </summary>
    public interface IMainThreadDispatcher
    {
        /// <summary>Actions posted and not yet run.</summary>
        int Pending { get; }

        void Post(Action action);
    }
}
