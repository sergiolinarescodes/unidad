using System;
using NUnit.Framework;
using Unidad.Launch;

namespace Unidad.Launch.Smoke
{
    public sealed class LaunchSmokeTests
    {
        [Test]
        public void SetPairsParse_UnknownAndRepeatedRefused()
        {
            var args = LaunchArgs.Parse(new[] { "--set", "seed=7", "--set", "anim.skip=on" });
            Assert.That(args.Get("seed"), Is.EqualTo("7"));
            Assert.That(args.TryGetInt("seed", out int seed) && seed == 7, Is.True);
            Assert.That(args.IsOn("anim.skip"), Is.True);
            Assert.That(args.Get("absent"), Is.Null);

            Assert.That(LaunchArgs.TryParse(new[] { "--movie", "x" }, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("'--movie'"));
            Assert.That(LaunchArgs.TryParse(new[] { "--set", "a=1", "--set", "a=2" }, out _, out error), Is.False);
            Assert.That(error, Is.EqualTo("--set a is given twice."));

            var unknown = Assert.Throws<ArgumentException>(() => LaunchArgs.Parse(new[] { "--set", "bogus=1" })
                .RequireKnownSettings(new[] { new LaunchSettingKey("seed", "the seed"), new LaunchSettingKey("frames", "frames") }));
            Assert.That(unknown.Message, Does.Contain("bogus").And.Contain("  seed: the seed").And.Contain("  frames: frames"));
            Assert.That(BootSeed.From(args, () => -1).Value, Is.EqualTo(7));
            Assert.That(BootSeed.From(LaunchArgs.Empty, () => -1).Value, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void LineReadsAfterSentinelElseEnvironment()
        {
            var fromArgs = LaunchLine.Read(new[] { "game.exe", "-batchmode", "--", "--set", "a=1" }, "--set b=2");
            Assert.That(fromArgs.Get("a"), Is.EqualTo("1"));
            Assert.That(fromArgs.Get("b"), Is.Null);

            var fromEnv = LaunchLine.Read(new[] { "game.exe", "-batchmode" }, "--set \"b=x y\"");
            Assert.That(fromEnv.Get("b"), Is.EqualTo("x y"));
            Assert.That(LaunchLine.Read(new[] { "game.exe" }, null).Settings, Is.Empty);
            Assert.Throws<ArgumentException>(() => LaunchLine.Split("--set \"a=1"));
        }
    }
}
