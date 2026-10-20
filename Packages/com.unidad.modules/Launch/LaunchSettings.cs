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
        public static BootSeed From(LaunchArgs args, Func<int> draw)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            if (draw == null) throw new ArgumentNullException(nameof(draw));
            return new BootSeed(args.TryGetInt(LaunchSettings.Seed, out int seed) ? seed : draw() & int.MaxValue);
        }
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
        public void Install(ContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            builder.AddMany(_ => new LaunchSettingKey(LaunchSettings.Seed, "the boot seed every random stream derives from (an integer)"));
            builder.AddMany(_ => new LaunchSettingKey(LaunchSettings.Frames, "quit after this many frames of play (a positive integer)"));
            builder.AddMany(_ => new LaunchSettingKey(LaunchSettings.Strict, "true fails the boot when any stub still wins a binding"));
            builder.AddMany(_ => new LaunchSettingKey(LaunchSettings.History, "true records every bus event"));
            builder.AddMany(_ => new LaunchSettingKey(LaunchSettings.DebugOpen, "true opens the debug window at boot"));
            builder.AddMany(_ => new LaunchSettingKey(LaunchSettings.AnimSkip, "true completes every animation at once"));
        }
    }
}
