using System;
using System.Collections.Generic;
using Unidad.Core.Abstractions;
using Unidad.Core.Jobs;
using Unidad.Flow;
using UnityEngine;

namespace Unidad.Adapter
{
    /// <summary>The only SceneManager caller; a SceneId's value is the scene name in the build profile.</summary>
    public sealed class SceneLoader
    {
        public SceneLoader() => throw new NotImplementedException();

        public AsyncOperation Load(SceneId scene) => throw new NotImplementedException();
    }

    /// <summary>Carries one <see cref="ISceneTicket"/> across a scene switch.</summary>
    public sealed class SceneTicketHolder : MonoBehaviour
    {
        public static SceneTicketHolder FindOrCreate() => throw new NotImplementedException();

        public void Put(ISceneTicket ticket) => throw new NotImplementedException();

        public T Take<T>() where T : class, ISceneTicket => throw new NotImplementedException();
    }

    /// <summary>Loads and warms what a scene needs before the curtain lifts, a few steps per frame; ready when nothing is pending.</summary>
    public sealed class ScenePreparation : ITickable, IDisposable
    {
        public ScenePreparation(AssetLibrary assets, Action<Exception> failed = null, FrameWorkTuning tuning = null) => throw new NotImplementedException();

        public bool IsReady => throw new NotImplementedException();

        public int Pending => throw new NotImplementedException();

        public Exception Failure => throw new NotImplementedException();

        public void Enqueue(string key, Action action) => throw new NotImplementedException();

        public void Add(string key, IEnumerable<Action> steps, Action release = null) => throw new NotImplementedException();

        public void Tick(float deltaTime) => throw new NotImplementedException();

        public void Dispose() => throw new NotImplementedException();
    }
}
