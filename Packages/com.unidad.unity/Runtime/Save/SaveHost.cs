using System;
using System.Collections.Generic;
using Unidad.Adapter.Ui;
using Unidad.Core.DI;
using Unidad.Launch;
using Unidad.Save;
using UnityEngine;

namespace Unidad.Adapter
{
    /// <summary>The save's Unity half: registers participants, ticks the save and autosave, saves on quit, shows the refusal notice.</summary>
    internal sealed class SaveHost : MonoBehaviour
    {
        public static SaveHost Bind(Container container, IGameObjectFactory factory, ITickRegistry ticks, IPanelLibrary panels,
            IReadOnlyList<ISaveParticipant> participants, LaunchArgs launch)
            => throw new NotImplementedException();
    }
}
