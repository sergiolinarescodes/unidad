using System;
using Unidad.Audio;
using Unidad.Core.Abstractions;
using Unidad.Core.DI;
using Unidad.Launch;
using UnityEngine;

namespace Unidad.Adapter
{
    /// <summary>The sound's Unity half: pooled AudioSources play the sound events; bus levels are set in code.</summary>
    internal sealed class SoundHost : MonoBehaviour, ITickable
    {
        public static SoundHost Bind(Container container, IGameObjectFactory factory, ITickRegistry ticks, ScenePreparation preparation,
            AssetLibrary assets, LaunchArgs launch)
            => throw new NotImplementedException();

        public void SetBusLevel(AudioBus bus, float linear) => throw new NotImplementedException();

        public void Tick(float deltaTime) => throw new NotImplementedException();
    }
}
