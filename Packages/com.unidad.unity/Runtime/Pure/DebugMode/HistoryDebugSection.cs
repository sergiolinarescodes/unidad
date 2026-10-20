using System.Linq;
using Unidad.Core.Diagnostics;
using Unidad.Core.HistoryService;

namespace Unidad.Adapter
{
    /// <summary>The History tab: the recorder's tick and event count, an entity filter, the last 100 matching entries, Clear History.</summary>
    internal sealed class HistoryDebugSection : IDebugSection
    {
        private const int MaxLines = 100;

        private readonly IHistoryService _history;
        private string _entityFilter = "";

        /// <param name="history">Null when the scene records no history.</param>
        public HistoryDebugSection(IHistoryService history) => _history = history;

        public string Title => "History";

        public int Order => int.MaxValue;

        public void Build(IDebugUi ui)
        {
            if (_history == null)
            {
                ui.Readout("History", () => "off (launch with history=true)");
                return;
            }
            ui.Readout("", () => $"Tick: {_history.CurrentTick} | Events: {_history.EntryCount}");
            ui.TextField("Entity", _entityFilter, text => _entityFilter = text ?? "");
            ui.Readout("", Lines);
            ui.Button("Clear History", _history.Clear);
        }

        private string Lines()
        {
            var entries = string.IsNullOrWhiteSpace(_entityFilter) ? _history.GetAll() : _history.ForEntity(_entityFilter.Trim());
            return string.Join("\n", entries.TakeLast(MaxLines).Select(e =>
                $"[{e.Timestamp.Tick}.{e.Timestamp.SubTick}] {e.EventTypeName}" +
                (string.IsNullOrEmpty(e.PrimaryEntityId) ? "" : $" [{e.PrimaryEntityId}]")));
        }
    }
}
