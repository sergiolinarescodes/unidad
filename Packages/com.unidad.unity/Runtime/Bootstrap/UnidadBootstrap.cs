using System;
using System.Collections.Generic;
using System.Reflection;
using Unidad.Core.Bootstrap;
using Unidad.Core.DI;
using Unidad.Core.Jobs;
using UnityEngine;

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
        /// <summary>The built container after Awake; null when the build failed before it.</summary>
        public Container Container => throw new NotImplementedException();

        /// <summary>The TickRunner; never null from <see cref="InstallCoreServices"/> on.</summary>
        protected ITickRegistry Ticks => throw new NotImplementedException();

        protected AssetLibrary Assets => throw new NotImplementedException();

        protected virtual bool RecordEventHistory => false;

        protected virtual JobTuning JobTuning => JobTuning.Default;

        /// <summary>The first step of Awake after the log seam (StageBootstrap reads the launch line here).</summary>
        protected virtual void BeforeInstall() { }

        /// <summary>The loaded assemblies that carry <c>[assembly: UnidadModule]</c> (CurrentAssemblies, AppDomain fallback).</summary>
        protected virtual IEnumerable<Assembly> InstallerAssemblies() => throw new NotImplementedException();

        /// <summary>Default: every installer type found in <see cref="InstallerAssemblies"/>, in discovery order.</summary>
        protected virtual void RegisterInstallers(List<ISystemInstaller> installers) => throw new NotImplementedException();

        protected virtual void InstallCoreServices(ContainerBuilder builder) => throw new NotImplementedException();

        protected virtual void InstallAdditionalBindings(ContainerBuilder builder) { }

        protected virtual void OnContainerReady(Container container) { }

        /// <summary>Base: in batch mode the process exits with code 1; otherwise nothing.</summary>
        protected virtual void OnInitializationFailed(Exception error) => throw new NotImplementedException();

        protected void Awake() => throw new NotImplementedException();

        protected void OnDestroy() => throw new NotImplementedException();
    }

    /// <summary>The process's exit: a report writer may set a code before the quit.</summary>
    public interface IAppQuit
    {
        int ExitCode { get; }

        void SetExitCode(int code);

        void Quit();
    }
}
