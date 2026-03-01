using System.Collections.Generic;
using System.Linq;

namespace Unidad.Core.Debugger
{
    /// <summary>
    /// Service for managing debug mode and debug providers.
    /// Toggle debug mode on/off, register providers, and query debug info.
    /// </summary>
    public sealed class DebugModeService
    {
        private readonly List<IDebugProvider> _providers = new();
        private bool _isEnabled;

        public bool IsEnabled => _isEnabled;
        public IReadOnlyList<IDebugProvider> Providers => _providers;

        public void Enable() => _isEnabled = true;
        public void Disable() => _isEnabled = false;
        public void Toggle() => _isEnabled = !_isEnabled;

        public void RegisterProvider(IDebugProvider provider)
        {
            if (_providers.Any(p => p.SystemName == provider.SystemName))
                return;
            _providers.Add(provider);
        }

        public void UnregisterProvider(string systemName)
        {
            _providers.RemoveAll(p => p.SystemName == systemName);
        }

        /// <summary>Get all debug info from all active providers.</summary>
        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> GetAllDebugInfo()
        {
            var result = new Dictionary<string, IReadOnlyDictionary<string, string>>();
            foreach (var provider in _providers)
            {
                if (_isEnabled || provider.IsActiveByDefault)
                {
                    result[provider.SystemName] = provider.GetDebugInfo();
                }
            }
            return result;
        }
    }
}
