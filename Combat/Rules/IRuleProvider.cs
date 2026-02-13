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
}
