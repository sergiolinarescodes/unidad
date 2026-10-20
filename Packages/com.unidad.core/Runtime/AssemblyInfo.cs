using System.Runtime.CompilerServices;
using Unidad.Core.Bootstrap;

[assembly: UnidadModule("Unidad.Core")]
[assembly: InternalsVisibleTo("Unidad.Core.Smoke")]
// The adapter constructs internal core services (EventBus, RecordingEventBus, HistoryService) directly.
[assembly: InternalsVisibleTo("Unidad.Adapter")]
