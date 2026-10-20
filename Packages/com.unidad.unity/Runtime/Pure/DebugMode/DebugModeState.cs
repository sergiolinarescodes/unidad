using System;
using System.Collections.Generic;
using System.Linq;
using Unidad.Core.Diagnostics;
using Unidad.Core.EventBus;

namespace Unidad.Adapter
{
    /// <summary>The Alt+D debug mode; Alt+Q hides or shows every debug window without changing the mode.</summary>
    internal sealed class DebugModeState : IDebugModeState
    {
        private readonly IEventBus _bus;

        public DebugModeState(IEventBus bus) => _bus = bus ?? throw new ArgumentNullException(nameof(bus));

        public bool IsActive { get; private set; }

        public bool WindowsHidden { get; private set; }

        public void Toggle()
        {
            if (IsActive) Deactivate();
            else Activate();
        }

        public void Activate()
        {
            if (IsActive) return;
            IsActive = true;
            _bus.Publish(new DebugModeActivatedEvent());
        }

        public void Deactivate()
        {
            if (!IsActive) return;
            IsActive = false;
            _bus.Publish(new DebugModeDeactivatedEvent());
        }

        public void ToggleWindowsHidden()
        {
            WindowsHidden = !WindowsHidden;
            _bus.Publish(new CloseAllDebugWindowsEvent(WindowsHidden));
        }
    }

    internal readonly record struct DebugTab(string Title, IReadOnlyList<IDebugSection> Sections);

    /// <summary>One tab per distinct section title, tabs and their rows by <see cref="IDebugSection.Order"/>.</summary>
    internal static class DebugLayout
    {
        public static IReadOnlyList<DebugTab> Group(IEnumerable<IDebugSection> sections)
            => sections.OrderBy(s => s.Order)
                       .GroupBy(s => s.Title)
                       .Select(g => new DebugTab(g.Key, g.ToList()))
                       .ToList();
    }
}
