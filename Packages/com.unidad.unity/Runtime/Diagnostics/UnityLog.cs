using System;
using Unidad.Core.Diagnostics;

namespace Unidad.Adapter
{
    /// <summary>The core log seam on the Unity console.</summary>
    public sealed class UnityLog : ILog
    {
        public void Info(string message) => throw new NotImplementedException();

        public void Warning(string message) => throw new NotImplementedException();

        public void Error(string message) => throw new NotImplementedException();

        public void Exception(Exception exception) => throw new NotImplementedException();
    }
}
