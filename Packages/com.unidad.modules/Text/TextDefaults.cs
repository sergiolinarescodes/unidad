namespace Unidad.Text
{
    /// <summary>The in-code English rows of the generic keys, used for any key no text file defines.</summary>
    public static class TextDefaults
    {
        public const string Source = "(Unidad English defaults)";

        private const string EnglishJson = @"{
  ""pause.title"": ""Paused"",
  ""pause.resume"": ""Resume"",
  ""pause.quit"": ""Quit"",
  ""quit.title"": ""Quit the game?"",
  ""quit.stay"": ""Stay"",
  ""quit.confirm"": ""Quit"",
  ""boot.failed"": ""The game could not start"",
  ""save.refused"": ""Your save is from a newer version of the game (save version {0}). It was not loaded and will not be overwritten."",
  ""save.unreadable"": ""Your save could not be read. It was not loaded and will not be overwritten.""
}";

        /// <summary>A new table per call (no static state).</summary>
        public static TextTable English() => TextTable.Parse(EnglishJson, Source);
    }
}
