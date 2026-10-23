using NUnit.Framework;
using Unidad.Launch;

namespace Unidad.Launch.Smoke
{
    public sealed class LaunchSelfQuitSmokeTests
    {
        [Test]
        public void FramesOrReportMeansSelfQuitting_EvenWhenTheLineDoesNotParse()
        {
            Assert.That(LaunchLine.SelfQuitting(new[] { "p.exe", "--", "--set", "frames=60" }, null), Is.True);
            Assert.That(LaunchLine.SelfQuitting(new[] { "p.exe", "--", "--set", "report=C:/r.json" }, null), Is.True);
            // the 11-unidad-unity failure: an empty value fails the parse, the run must still quit
            Assert.That(LaunchLine.SelfQuitting(new[] { "p.exe", "--", "--set", "frames=60", "--set", "look.objects=" }, null), Is.True);
            Assert.That(LaunchLine.SelfQuitting(new[] { "editor.exe" }, "--set frames=60 --set bad="), Is.True);
        }

        [Test]
        public void InteractiveLineKeepsTheFailureScreen()
        {
            Assert.That(LaunchLine.SelfQuitting(new[] { "p.exe", "--", "--set", "start=room" }, null), Is.False);
            Assert.That(LaunchLine.SelfQuitting(new[] { "p.exe" }, null), Is.False);
            Assert.That(LaunchLine.SelfQuitting(new[] { "p.exe" }, "--set \"frames=60"), Is.False);
            Assert.That(LaunchLine.SelfQuitting(new[] { "p.exe", "--", "--set", "seed=7" }, "--set frames=60"), Is.False);
        }
    }
}
