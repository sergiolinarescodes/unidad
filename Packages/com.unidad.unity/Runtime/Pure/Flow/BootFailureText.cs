using System;
using Unidad.Text;

namespace Unidad.Adapter
{
    internal static class BootFailureText
    {
        public const string FallbackTitle = "The game could not start";

        /// <summary>The page title; the text table may itself be what failed the boot, so any trouble reading it falls back.</summary>
        public static string Title(Func<ITextService> resolve)
        {
            try
            {
                var text = resolve?.Invoke();
                return text != null && text.Has(TextKeys.BootFailed) ? text.Get(TextKeys.BootFailed) : FallbackTitle;
            }
            catch (Exception)
            {
                return FallbackTitle;
            }
        }
    }
}
