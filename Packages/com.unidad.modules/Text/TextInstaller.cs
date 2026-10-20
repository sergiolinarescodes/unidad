using System;
using Unidad.Core.Bootstrap;
using Unidad.Core.Content;
using Unidad.Core.DI;
using Unidad.Launch;

namespace Unidad.Text
{
    /// <summary>Declares the locale key; binds the TextTable loaded from content and <see cref="ITextService"/> over it.</summary>
    public sealed class TextInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            builder.AddMany(_ => new LaunchSettingKey(TextSettings.Locale, "the text locale, such as en or es (default en)"));
            builder.AddSingleton(c => TextContent.Load(c.Resolve<IContentSource>(), c.ResolveAll<TextKeySet>(), LocaleOf(c)), typeof(TextTable));
            builder.AddSingleton(c => (ITextService)new TextService(c.Resolve<TextTable>()), typeof(ITextService));
        }

        private static string LocaleOf(Container c) =>
            c.TryResolve<LaunchArgs>(out var args) ? args.Get(TextSettings.Locale) ?? TextContent.DefaultLocale : TextContent.DefaultLocale;
    }
}
