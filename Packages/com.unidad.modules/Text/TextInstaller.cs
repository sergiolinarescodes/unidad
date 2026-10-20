using System;
using Unidad.Core.Bootstrap;
using Unidad.Core.DI;

namespace Unidad.Text
{
    /// <summary>Declares the locale key; binds the TextTable loaded from content and <see cref="ITextService"/> over it.</summary>
    public sealed class TextInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder) => throw new NotImplementedException();
    }
}
