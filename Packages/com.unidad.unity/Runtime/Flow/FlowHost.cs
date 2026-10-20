using System;
using Unidad.Adapter.Ui;
using Unidad.Core.DI;
using Unidad.Flow;
using UnityEngine;

namespace Unidad.Adapter
{
    /// <summary>The flow's Unity half: the loading curtain, the pause menu, the quit question and the time scale.</summary>
    internal sealed class FlowHost : MonoBehaviour
    {
        public static FlowHost Bind(Container container, IGameObjectFactory factory, ITickRegistry ticks, IPanelLibrary panels,
            IWorldReadiness readiness, ScenePreparation preparation, AssetLibrary assets)
            => throw new NotImplementedException();
    }
}
