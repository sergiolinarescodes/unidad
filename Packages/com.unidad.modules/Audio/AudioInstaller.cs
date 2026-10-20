using System;
using Unidad.Core.Bootstrap;
using Unidad.Core.Content;
using Unidad.Core.DI;
using Unidad.Core.EventBus;
using Unidad.Launch;

namespace Unidad.Audio
{
    /// <summary>Binds the <see cref="SoundCatalog"/> loaded from content and <see cref="ISoundService"/>; declares the mute key.</summary>
    public sealed class AudioInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            builder.AddSingleton(c => SoundCatalog.Load(c.Resolve<IContentSource>()), typeof(SoundCatalog));
            builder.AddSingleton(c => (ISoundService)new SoundService(c.Resolve<IEventBus>(), c.Resolve<SoundCatalog>()), typeof(ISoundService));
            builder.AddMany(_ => new LaunchSettingKey(AudioSettings.Mute, "true silences every bus"));
        }
    }
}
