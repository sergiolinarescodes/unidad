using System;
using System.Collections.Generic;
using Unidad.Adapter.Ui;
using Unidad.Core.DI;
using Unidad.Flow;
using Unidad.Launch;
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
        protected abstract SceneId Scene { get; }

        protected virtual IWorldReadiness Readiness => ImmediateReadiness.Instance;

        /// <summary>The AssetLibrary key of the overlay PanelSettings every framework panel uses.</summary>
        protected virtual string OverlayPanelKey => PanelKeys.Overlay;

        /// <summary>Default: "[Camera]" under the stage root, perspective, solid black, with an AudioListener.</summary>
        protected virtual Camera CreateCamera(IGameObjectFactory factory, Transform stageRoot) => throw new NotImplementedException();

        protected abstract void BuildStage(Container container, StageContext context);

        /// <summary>The launch line, parsed in BeforeInstall.</summary>
        protected LaunchArgs Launch => throw new NotImplementedException();
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
