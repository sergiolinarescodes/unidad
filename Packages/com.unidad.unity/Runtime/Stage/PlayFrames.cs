using System;
using Unidad.Core.Abstractions;
using Unidad.Core.DI;
using Unidad.Launch;
using UnityEngine;

namespace Unidad.Adapter
{
    /// <summary>The <c>frames</c> key: counts frames from the curtain lift, publishes the elapsed event, then quits.</summary>
    internal sealed class PlayFrames : MonoBehaviour, ITickable
    {
        public static PlayFrames Bind(Container container, IGameObjectFactory factory, ITickRegistry ticks, LaunchArgs launch)
            => throw new NotImplementedException();

        public void Tick(float deltaTime) => throw new NotImplementedException();
    }
}
