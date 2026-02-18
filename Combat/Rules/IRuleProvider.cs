using System.Collections.Generic;

namespace QDND.Combat.Rules
{
    /// <summary>
    /// Passive or reactive rule provider that can respond to one or more rule windows.
    /// </summary>
    public interface IRuleProvider
    {
        string ProviderId { get; }
        string OwnerId { get; }
        int Priority { get; }
        IReadOnlyCollection<RuleWindow> Windows { get; }

        bool IsEnabled(RuleEventContext context);
        void OnWindow(RuleEventContext context);
    }

    /// <summary>
    /// A rule provider that grants persistent modifiers to its owner's modifier stack
    /// when registered (and removes them when unregistered).
    /// </summary>
    internal interface IModifierGrantProvider
    {
        IEnumerable<Modifier> GetGrantedModifiers();
    }
}
