using System;
using Unidad.Core.Bootstrap;
using Unidad.Core.DI;

namespace Unidad.Save
{
    /// <summary>
    /// Binds <see cref="ISaveService"/> over the bound <see cref="ISaveStore"/>, <see cref="AutosaveTuning"/> when none is bound,
    /// and <see cref="IAutosave"/>; declares the save.dir and fresh keys.
    /// </summary>
    public sealed class SaveInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder) => throw new NotImplementedException();
    }
}
