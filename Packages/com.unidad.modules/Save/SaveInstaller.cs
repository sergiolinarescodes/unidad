using System;
using Unidad.Core.Bootstrap;
using Unidad.Core.DI;
using Unidad.Core.EventBus;
using Unidad.Launch;

namespace Unidad.Save
{
    /// <summary>
    /// Binds <see cref="ISaveService"/> over the bound <see cref="ISaveStore"/>, <see cref="AutosaveTuning"/> when none is bound,
    /// and <see cref="IAutosave"/>; declares the save.dir and fresh keys.
    /// </summary>
    public sealed class SaveInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            builder.AddSingleton(c => (ISaveService)new SaveService(c.Resolve<IEventBus>(), c.Resolve<ISaveStore>()), typeof(ISaveService));
            if (!builder.HasBinding<AutosaveTuning>()) builder.AddSingleton(new AutosaveTuning());
            builder.AddSingleton(c => (IAutosave)new AutosaveService(c.Resolve<IEventBus>(), c.Resolve<ISaveService>(), c.Resolve<AutosaveTuning>()),
                typeof(IAutosave));
            builder.AddMany(_ => new LaunchSettingKey(SaveSettings.SaveDir, "an absolute folder for the save slot (keeps the player's save untouched)"));
            builder.AddMany(_ => new LaunchSettingKey(SaveSettings.Fresh,
                "true starts a new game without reading the slot (the old slot is kept as slot0.json.bak)"));
        }
    }
}
