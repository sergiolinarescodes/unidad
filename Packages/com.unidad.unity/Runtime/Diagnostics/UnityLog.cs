using System;
using Unidad.Core.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Unidad.Adapter
{
    /// <summary>The core log seam on the Unity console.</summary>
    public sealed class UnityLog : ILog
    {
        public void Info(string message) => Debug.Log(message);

        public void Warning(string message) => Debug.LogWarning(message);

        public void Error(string message) => Debug.LogError(message);

        public void Exception(Exception exception) => Debug.LogException(exception);
    }
}
