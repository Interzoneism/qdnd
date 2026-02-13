using System;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Combat.Rules
{
    /// <summary>
    /// Dispatches canonical rule windows to registered providers in deterministic order.
    /// </summary>
    public class RuleWindowBus
    {
        private class ProviderEntry
        {
            public IRuleProvider Provider { get; init; }
            public int RegisteredOrder { get; init; }
        }

        private readonly List<ProviderEntry> _providers = new();
        private int _registrationOrder;

        public IReadOnlyCollection<IRuleProvider> GetProviders()
            => _providers.Select(p => p.Provider).ToList();

        public void Register(IRuleProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            if (string.IsNullOrWhiteSpace(provider.ProviderId))
                throw new ArgumentException("Rule provider must declare a ProviderId.", nameof(provider));

            Unregister(provider.ProviderId);

            _providers.Add(new ProviderEntry
            {
                Provider = provider,
                RegisteredOrder = _registrationOrder++
            });
        }

        public void Unregister(string providerId)
        {
            if (string.IsNullOrWhiteSpace(providerId))
                return;

            _providers.RemoveAll(p => string.Equals(p.Provider.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));
        }

        public void UnregisterByOwner(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                return;

            _providers.RemoveAll(p => string.Equals(p.Provider.OwnerId, ownerId, StringComparison.OrdinalIgnoreCase));
        }

        public void Dispatch(RuleWindow window, RuleEventContext context)
        {
            context ??= new RuleEventContext();
            context.Window = window;

            var handlers = _providers
                .Where(p => p.Provider.Windows != null && p.Provider.Windows.Contains(window))
                .OrderBy(p => p.Provider.Priority)
                .ThenBy(p => p.RegisteredOrder)
                .ToList();

            foreach (var entry in handlers)
            {
                if (context.Cancel)
                    break;

                try
                {
                    if (!entry.Provider.IsEnabled(context))
                        continue;

                    entry.Provider.OnWindow(context);
                }
                catch (Exception ex)
                {
                    Godot.GD.PushError($"RuleWindow provider '{entry.Provider.ProviderId}' failed on {window}: {ex.Message}");
                }
            }
        }

        public void Clear()
        {
            _providers.Clear();
            _registrationOrder = 0;
        }
    }
}
