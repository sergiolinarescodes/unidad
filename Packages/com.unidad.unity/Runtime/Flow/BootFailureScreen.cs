using System;
using Unidad.Core.DI;
using UnityEngine;

namespace Unidad.Adapter
{
    /// <summary>The boot failure page (layer Error), or exit code 1 in batch mode.</summary>
    internal static class BootFailureScreen
    {
        public const string FallbackTitle = "The game could not start";

        public static void Show(MonoBehaviour scene, Container containerOrNull, Exception error) => throw new NotImplementedException();
    }
}
