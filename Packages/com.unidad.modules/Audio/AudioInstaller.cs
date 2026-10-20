using System;
using Unidad.Core.Bootstrap;
using Unidad.Core.DI;

namespace Unidad.Audio
{
    /// <summary>Binds the <see cref="SoundCatalog"/> loaded from content and <see cref="ISoundService"/>; declares the mute key.</summary>
    public sealed class AudioInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder) => throw new NotImplementedException();
    }
}
