using System;
using Unidad.Core.Bootstrap;
using Unidad.Core.DI;

namespace Unidad.Launch
{
    /// <summary>One launch key a module reads, with its help line; added with <c>AddMany&lt;LaunchSettingKey&gt;</c>.</summary>
    public sealed record LaunchSettingKey(string Key, string Help);

    /// <summary>The seed every random stream of the run derives from.</summary>
    public sealed record BootSeed(int Value)
    {
        /// <summary><c>seed</c> when given, else <c>draw() &amp; int.MaxValue</c>.</summary>
        public static BootSeed From(LaunchArgs args, Func<int> draw) => throw new NotImplementedException();
    }

    /// <summary>The generic launch keys this module declares.</summary>
    public static class LaunchSettings
    {
        public const string Seed = "seed", Frames = "frames", Strict = "strict", History = "history",
                            DebugOpen = "debug.open", AnimSkip = "anim.skip";
    }

    /// <summary>Declares the six <see cref="LaunchSettings"/> keys.</summary>
    public sealed class LaunchInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder) => throw new NotImplementedException();
    }
}
