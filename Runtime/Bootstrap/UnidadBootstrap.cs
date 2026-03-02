using System;
using System.Collections.Generic;
using Reflex.Core;
using Reflex.Extensions;
using UnityEngine;
using Unidad.Core.Abstractions;
using Unidad.Core.Debugger;
using Unidad.Core.EventBus;
using Unidad.Core.Factory;
using Unidad.Core.HistoryService;
using Unidad.Core.ObjectPool;

namespace Unidad.Core.Bootstrap
{
    /// <summary>
    /// Abstract base class for the single Bootstrap MonoBehaviour.
    /// The ONLY manually-placed MonoBehaviour in the scene.
    /// Handles DI container setup and delegates system installation to subclasses.
    /// </summary>
    public abstract class UnidadBootstrap : MonoBehaviour, IInstaller
    {
        private Container _container;
        private readonly List<ISystemInstaller> _installers = new();

        /// <summary>
        /// Access to the DI container after initialization.
        /// </summary>
        protected Container Container => _container;

        public void InstallBindings(ContainerBuilder builder)
        {
            // 1. Core services (EventBus, HistoryService, Time, Factory, Pools, Debug)
            InstallCoreServices(builder);

            // 2. Game-specific systems (implemented by subclass)
            RegisterInstallers(_installers);

            foreach (var installer in _installers)
            {
                installer.Install(builder);
            }

            // 3. Additional bindings from subclass
            InstallAdditionalBindings(builder);
        }

        private void Start()
        {
            try
            {
                _container = gameObject.scene.GetSceneContainer();
                SpawnTickRunner(_container);
                OnContainerReady(_container);
                Debug.Log($"[UnidadBootstrap] Initialization complete. {_installers.Count} systems installed.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnidadBootstrap] Initialization failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Install core framework services.
        /// Override to customize (e.g., skip history in production builds).
        /// </summary>
        protected virtual void InstallCoreServices(ContainerBuilder builder)
        {
            // EventBus + HistoryService (with recording)
            var historyService = new HistoryService.HistoryService();
            historyService.StartRecording();
            builder.AddSingleton(_ => (IHistoryService)historyService, typeof(IHistoryService));

            var innerEventBus = new EventBus.EventBus();
            var recordingEventBus = new RecordingEventBus(innerEventBus, historyService);
            builder.AddSingleton(_ => (IEventBus)recordingEventBus, typeof(IEventBus));

            // Time
            var timeProvider = new UnityTimeProvider();
            builder.AddSingleton(_ => (ITimeProvider)timeProvider, typeof(ITimeProvider));

            // Factory
            var factory = new GameObjectFactory();
            builder.AddSingleton(_ => (IGameObjectFactory)factory, typeof(IGameObjectFactory));

            // Pool Registry
            var poolRegistry = new PoolRegistry();
            builder.AddSingleton(_ => poolRegistry, typeof(PoolRegistry));

            // Debug
            var debugService = new DebugModeService();
            builder.AddSingleton(_ => debugService, typeof(DebugModeService));

            // UI Services
            InstallUIServices(builder);

            // Animation (default: real animations — subclass can override for instant in tests)
            InstallAnimationResolver(builder);
        }

        /// <summary>
        /// Install animation resolver. Override to swap in InstantAnimationResolver for tests.
        /// Default does nothing — games should provide their own implementation.
        /// </summary>
        protected virtual void InstallAnimationResolver(ContainerBuilder builder) { }

        /// <summary>
        /// Install UI system services (theme, text animation, element animator, dialog).
        /// Override to customize or skip UI services.
        /// Called from InstallCoreServices before animation resolver.
        /// </summary>
        protected virtual void InstallUIServices(ContainerBuilder builder)
        {
            var uiInstaller = new UI.UISystemInstaller();
            uiInstaller.Install(builder);

            var dialogInstaller = new UI.DialogSystemInstaller();
            dialogInstaller.Install(builder);
        }

        /// <summary>
        /// Spawns the TickRunner MonoBehaviour and wires it to all registered ITickable and IFixedTickable services.
        /// </summary>
        private void SpawnTickRunner(Container container)
        {
            var timeProvider = container.Resolve<ITimeProvider>();
            var tickables = ResolveTickables(container);
            var fixedTickables = ResolveFixedTickables(container);

            if (tickables.Count == 0 && fixedTickables.Count == 0) return;

            var tickRunnerObj = new GameObject("[TickRunner]");
            tickRunnerObj.transform.SetParent(transform);
            var tickRunner = tickRunnerObj.AddComponent<TickRunner>();
            tickRunner.Initialize(timeProvider, tickables, fixedTickables);
        }

        /// <summary>
        /// Resolves all ITickable services from the container.
        /// Override to customize which tickables are registered.
        /// </summary>
        protected virtual List<ITickable> ResolveTickables(Container container)
        {
            return new List<ITickable>();
        }

        /// <summary>
        /// Resolves all IFixedTickable services from the container.
        /// Override to customize which fixed tickables are registered.
        /// </summary>
        protected virtual List<IFixedTickable> ResolveFixedTickables(Container container)
        {
            return new List<IFixedTickable>();
        }

        /// <summary>
        /// Register all system installers. Subclasses add their game systems here.
        /// Order matters: systems are installed in registration order.
        /// </summary>
        protected abstract void RegisterInstallers(List<ISystemInstaller> installers);

        /// <summary>
        /// Optional: install additional bindings after all systems.
        /// </summary>
        protected virtual void InstallAdditionalBindings(ContainerBuilder builder) { }

        /// <summary>
        /// Called after the DI container is ready and TickRunner is spawned.
        /// Wire up late bindings, spawn runtime controllers, etc.
        /// </summary>
        protected virtual void OnContainerReady(Container container) { }
    }
}
