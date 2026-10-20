using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unidad.Adapter.Motion;
using Unidad.Adapter.Ui;
using Unidad.Core.Content;
using Unidad.Core.Diagnostics;
using Unidad.Core.DI;
using Unidad.Core.Random;
using Unidad.Core.Ticking;
using Unidad.Flow;
using Unidad.Launch;
using Unidad.Save;
using Unidad.Text;
using UnityEngine;

namespace Unidad.Adapter.Stage
{
    /// <summary>
    /// A gameplay scene's bootstrap: reads the launch line, binds content, save store, seed and panels, then (after the
    /// build) checks launch keys, stubs and content, binds the save, flow and sound hosts, registers every TickBinding,
    /// builds the stage and binds every <see cref="IStageHost"/> that applies to <see cref="Scene"/>.
    /// </summary>
    public abstract class StageBootstrap : UnidadBootstrap
    {
        private LaunchArgs _launch;

        protected abstract SceneId Scene { get; }

        protected virtual IWorldReadiness Readiness => ImmediateReadiness.Instance;

        /// <summary>The AssetLibrary key of the overlay PanelSettings every framework panel uses.</summary>
        protected virtual string OverlayPanelKey => PanelKeys.Overlay;

        /// <summary>Default: "[Camera]" under the stage root, perspective, solid black, with an AudioListener.</summary>
        protected virtual Camera CreateCamera(IGameObjectFactory factory, Transform stageRoot)
        {
            var camera = factory.Create<Camera>("[Camera]", stageRoot);
            camera.orthographic = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.gameObject.AddComponent<AudioListener>();
            return camera;
        }

        protected abstract void BuildStage(Container container, StageContext context);

        /// <summary>The launch line, parsed in BeforeInstall.</summary>
        protected LaunchArgs Launch => _launch;

        protected override bool RecordEventHistory => _launch != null && _launch.IsOn(LaunchSettings.History);

        protected override void BeforeInstall() =>
            _launch = LaunchLine.Read(Environment.GetCommandLineArgs(), Environment.GetEnvironmentVariable(LaunchLine.EnvironmentVariable));

        protected override void InstallAdditionalBindings(ContainerBuilder builder)
        {
            builder.AddSingleton(_launch, typeof(LaunchArgs));
            builder.AddSingleton(new DirectoryContentSource(Path.Combine(Application.streamingAssetsPath, "Content")),
                typeof(IContentSource), typeof(DirectoryContentSource));
            builder.AddSingleton(new FileSaveStore(SaveSettings.SlotPath(_launch, Path.Combine(Application.persistentDataPath, "saves"))),
                typeof(ISaveStore));
            var seed = BootSeed.From(_launch, () => Environment.TickCount);
            builder.AddSingleton(seed, typeof(BootSeed));
            builder.AddSingleton(new SeedSource(seed.Value), typeof(ISeedSource));
            var assets = Assets;
            var overlay = OverlayPanelKey;
            builder.AddSingleton(c => (IPanelLibrary)new PanelLibrary(c.Resolve<IGameObjectFactory>(), assets, overlay), typeof(IPanelLibrary));
            builder.AddSingleton(SceneTicketHolder.FindOrCreate(), typeof(SceneTicketHolder));
            // A debug installer binds the real mode in development builds; without one the mode is always off.
            if (!builder.HasBinding<IDebugModeState>()) builder.AddSingleton(new InactiveDebugMode(), typeof(IDebugModeState));
        }

        protected override void OnContainerReady(Container c)
        {
            _launch.RequireKnownSettings(c.ResolveAll<LaunchSettingKey>());
            var stubWarning = InstallerOrderLog.StubWarning(c.WinningStubs);
            if (stubWarning != null) Log.Warning(stubWarning);

            c.Resolve<ITextService>();
            ContentReport.Run(c.ResolveAll<IContentCheck>()).ThrowIfAny();
            Log.Info($"[{Scene}] seed {c.Resolve<BootSeed>().Value}" + (_launch.Settings.Count > 0
                ? "; settings " + string.Join(", ", _launch.Settings.Select(setting => $"{setting.Key}={setting.Value}"))
                : ""));

            var factory = c.Resolve<IGameObjectFactory>();
            var panels = c.Resolve<IPanelLibrary>();
            var preparation = c.Resolve<ScenePreparation>();
            // The slot loads before anything reads a participant, and SaveHost's quit hook precedes FlowHost's.
            SaveHost.Bind(c, factory, Ticks, panels, c.ResolveAll<ISaveParticipant>(), _launch);
            FlowHost.Bind(c, factory, Ticks, panels, Readiness, preparation, Assets);
            SoundHost.Bind(c, factory, Ticks, preparation, Assets, _launch);
            foreach (var binding in c.ResolveAll<TickBinding>()) Ticks.Register(binding);

            var stageRoot = factory.Create("[Stage]", transform).transform;
            var context = new StageContext(factory, stageRoot, panels, CreateCamera(factory, stageRoot), c.Resolve<IAnchorRegistry>());
            BuildStage(c, context);
            foreach (var host in c.ResolveAll<IStageHost>().OrderBy(host => host.Order))
                if (host.AppliesTo(Scene)) host.Bind(c, context);

            // After the hosts, so a boot report host has bound before strict fails the boot.
            var strictFailure = _launch.IsOn(LaunchSettings.Strict) ? InstallerOrderLog.StrictFailure(c.WinningStubs) : null;
            if (strictFailure != null) throw new InvalidOperationException(strictFailure);

            DebugHost.Bind(c, factory, panels, _launch);
            PlayFrames.Bind(c, factory, Ticks, _launch);
            c.Resolve<IMotionRunner>().SkipAll = _launch.IsOn(LaunchSettings.AnimSkip);
        }

        protected override void OnInitializationFailed(Exception error)
        {
            try
            {
                BootFailureScreen.Show(this, Container, error);
            }
            finally
            {
                base.OnInitializationFailed(error);
            }
        }
    }

    /// <summary>A view that binds itself to the stage; added with <c>AddMany&lt;IStageHost&gt;</c> and bound by <see cref="Order"/>.</summary>
    public interface IStageHost
    {
        int Order { get; }

        bool AppliesTo(SceneId scene);

        void Bind(Container container, StageContext context);
    }

    /// <summary>What every stage host binds against.</summary>
    public sealed record StageContext(IGameObjectFactory Factory, Transform StageRoot, IPanelLibrary Panels, Camera MainCamera, IAnchorRegistry Anchors);

    /// <summary>Named transforms views place things at, registered by the stage.</summary>
    public interface IAnchorRegistry
    {
        /// <summary>A duplicate id throws InvalidOperationException.</summary>
        void Register(string id, Transform anchor);

        /// <summary>An unknown id throws KeyNotFoundException listing the known ones.</summary>
        Transform Get(string id);

        bool TryGet(string id, out Transform anchor);

        /// <summary>In registration order.</summary>
        IReadOnlyList<string> Ids { get; }
    }

    /// <summary>Published just before the <c>frames</c> self-quit, so a report writer runs first.</summary>
    public readonly record struct PlayFramesElapsedEvent(int Frames);
}
