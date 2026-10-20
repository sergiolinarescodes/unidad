using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unidad.Adapter.Models;
using Unidad.Adapter.Motion;
using Unidad.Adapter.Stage;
using Unidad.Core.Abstractions;
using Unidad.Core.Bootstrap;
using Unidad.Core.Diagnostics;
using Unidad.Core.DI;
using Unidad.Core.EventBus;
using Unidad.Core.Jobs;
using Unidad.Core.Ticking;
using UnityEngine;
using CoreEventBus = Unidad.Core.EventBus.EventBus;

namespace Unidad.Adapter
{
    /// <summary>
    /// The scene's composition root. <c>Awake</c> is a template: log seam, <see cref="BeforeInstall"/>, core services, every
    /// discovered installer, <see cref="InstallAdditionalBindings"/>, build, tick registrations, <see cref="OnContainerReady"/>.
    /// Any exception anywhere is logged and routed to <see cref="OnInitializationFailed"/>; nothing fails quietly.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public abstract class UnidadBootstrap : MonoBehaviour
    {
        private readonly List<ISystemInstaller> _installers = new();
        private Container _container;
        private TickRunner _ticks;
        private AssetLibrary _assets;
        private Transform _objectsRoot;
        private MainThreadDispatcher _dispatcher;

        /// <summary>The built container after Awake; null when the build failed before it.</summary>
        public Container Container => _container;

        /// <summary>The TickRunner; never null from <see cref="InstallCoreServices"/> on.</summary>
        protected ITickRegistry Ticks => _ticks;

        protected AssetLibrary Assets => _assets;

        protected virtual bool RecordEventHistory => false;

        protected virtual JobTuning JobTuning => JobTuning.Default;

        /// <summary>The first step of Awake after the log seam (StageBootstrap reads the launch line here).</summary>
        protected virtual void BeforeInstall() { }

        /// <summary>The loaded assemblies that carry <c>[assembly: UnidadModule]</c> (CurrentAssemblies, AppDomain fallback).</summary>
        protected virtual IEnumerable<Assembly> InstallerAssemblies()
        {
            IReadOnlyList<Assembly> modules = null;
            try
            {
                modules = InstallerDiscovery.ModuleAssemblies(UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies());
            }
            catch (Exception ex)
            {
                Log.Warning($"[Boot] CurrentAssemblies failed: {ex.GetType().Name}: {ex.Message}");
            }
            if (modules is { Count: > 0 }) return modules;

            // netstandard2.1 has no AssemblyLoadContext; Unity 7 runs CoreCLR, so it is reached by reflection.
            var contextType = Type.GetType("System.Runtime.Loader.AssemblyLoadContext, System.Runtime.Loader");
            var getContext = contextType?.GetMethod("GetLoadContext", new[] { typeof(Assembly) });
            var defaultContext = contextType?.GetProperty("Default")?.GetValue(null);
            var own = getContext?.Invoke(null, new object[] { typeof(UnidadBootstrap).Assembly });
            var fallback = InstallerDiscovery.ModuleAssemblies(AppDomain.CurrentDomain.GetAssemblies().Where(assembly =>
            {
                if (getContext == null) return true;
                var context = getContext.Invoke(null, new object[] { assembly });
                return ReferenceEquals(context, own) || ReferenceEquals(context, defaultContext);
            }));
            if (fallback.Count > 0) Log.Warning("[Boot] CurrentAssemblies listed no Unidad module; using the AppDomain (C3 fallback).");
            return fallback;
        }

        /// <summary>Default: every installer type found in <see cref="InstallerAssemblies"/>, in discovery order.</summary>
        protected virtual void RegisterInstallers(List<ISystemInstaller> installers)
        {
            // CreateInstaller throws InstallerLoadException on a broken installer: a skipped system never reads as a boot.
            foreach (var type in InstallerDiscovery.FindInstallerTypes(InstallerAssemblies()))
                installers.Add(InstallerDiscovery.CreateInstaller(type));
        }

        protected virtual void InstallCoreServices(ContainerBuilder builder)
        {
            if (RecordEventHistory)
                // shortcut: RecordingEventBus/HistoryService are not in core yet (f1-core-runtime); wrap the bus once they land.
                Log.Warning("[UnidadBootstrap] history=true: core has no RecordingEventBus yet, so events are not recorded.");
            builder.AddSingleton(_ => (IEventBus)new CoreEventBus(), typeof(IEventBus));
            builder.AddSingleton(_ => (ITimeProvider)new UnityTimeProvider(), typeof(ITimeProvider));
            builder.AddSingleton(_ticks, typeof(ITickRegistry), typeof(TickRunner));
            builder.AddSingleton(new AppQuitService(), typeof(IAppQuit));
            builder.AddSingleton(_assets, typeof(AssetLibrary));
            var objectsRoot = _objectsRoot;
            var assets = _assets;
            builder.AddSingleton(c => (IGameObjectFactory)new GameObjectFactory(objectsRoot, assets, c.Resolve<IModelCatalog>,
                c.ResolveAll<IModelDecorator>), typeof(IGameObjectFactory));
            builder.AddSingleton(_ => new ScenePreparation(assets, OnInitializationFailed), typeof(ScenePreparation));
            builder.AddSingleton(_ => new PoolRegistry(), typeof(PoolRegistry));
            var tuning = JobTuning;
            _dispatcher = new MainThreadDispatcher(tuning.DeliveriesPerTick);
            builder.AddSingleton(_dispatcher, typeof(IMainThreadDispatcher));
            builder.AddSingleton(c => (IJobQueue)new JobQueue(c.Resolve<IMainThreadDispatcher>(), tuning.MaxConcurrency), typeof(IJobQueue));
            builder.AddSingleton(c => (IMotionRunner)new MotionRunner(c.Resolve<ITickRegistry>(), c.Resolve<ITimeProvider>()), typeof(IMotionRunner));
            builder.AddSingleton(c => (IModelCatalog)new ModelCatalog(c.ResolveAll<IModelSource>()), typeof(IModelCatalog));
            builder.AddSingleton(_ => (IAnchorRegistry)new AnchorRegistry(), typeof(IAnchorRegistry));
            builder.AddSingleton(_ => new SceneLoader(), typeof(SceneLoader));
        }

        protected virtual void InstallAdditionalBindings(ContainerBuilder builder) { }

        protected virtual void OnContainerReady(Container container) { }

        /// <summary>Base: in batch mode the process exits with code 1; otherwise nothing.</summary>
        protected virtual void OnInitializationFailed(Exception error)
        {
            if (Application.isBatchMode) AppQuit.Now(1);
        }

        protected void Awake()
        {
            Log.Current = new UnityLog();
            try
            {
                BeforeInstall();
                _assets = new AssetLibrary();
                using (_assets.AllowColdLoads())
                {
                    _assets.BeginScene();
                    _ticks = Child("[TickRunner]").AddComponent<TickRunner>();
                    _objectsRoot = Child("[Objects]").transform;

                    var builder = new ContainerBuilder();
                    InstallCoreServices(builder);
                    RegisterInstallers(_installers);
                    Log.Info(InstallerOrderLog.Line(_installers));
                    foreach (var installer in _installers) installer.Install(builder);
                    InstallAdditionalBindings(builder);
                    _container = builder.Build();

                    // The dispatcher is the first Input entry: job results land before anything reads them this frame.
                    _ticks.Register(_dispatcher, TickGroup.Input);
                    _ticks.Register(_container.Resolve<ScenePreparation>(), TickGroup.Presentation);
                    if (_container.Resolve<IMotionRunner>() is ITickable motion) _ticks.Register(motion, TickGroup.Presentation, runWhilePaused: true);
                    OnContainerReady(_container);
                }
                Log.Info($"[UnidadBootstrap] Initialization complete. {_installers.Count} installers.");
            }
            catch (Exception ex)
            {
                Log.Error($"[UnidadBootstrap] Initialization failed: {ex.Message}");
                Log.Exception(ex);
                OnInitializationFailed(ex);
            }
        }

        protected void OnDestroy()
        {
            if (_ticks) _ticks.enabled = false;
            _container?.Dispose();
        }

        private GameObject Child(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            return child;
        }
    }

    /// <summary>The process's exit: a report writer may set a code before the quit.</summary>
    public interface IAppQuit
    {
        int ExitCode { get; }

        void SetExitCode(int code);

        void Quit();
    }
}
