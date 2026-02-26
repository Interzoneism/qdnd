using System;
using System.Collections.Generic;

namespace QDND.Combat.Reactions
{
    public interface IReactionAliasResolver
    {
        string Resolve(string reactionId);
        bool IsKnown(string reactionId);
        IReadOnlyDictionary<string, string> Aliases { get; }
    }

    /// <summary>
    /// Canonical reaction IDs + legacy aliases.
    /// </summary>
    public static class ReactionIds
    {
        public const string OpportunityAttack = "reaction.opportunity_attack";
        public const string Counterspell = "reaction.counterspell";
        public const string Shield = "reaction.shield";

        public static readonly IReadOnlyDictionary<string, string> CanonicalByAlias =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [OpportunityAttack] = OpportunityAttack,
                ["opportunity_attack"] = OpportunityAttack,
                ["BG3_OpportunityAttack"] = OpportunityAttack,

                [Counterspell] = Counterspell,
                ["counterspell_reaction"] = Counterspell,
                ["BG3_Counterspell"] = Counterspell,

                [Shield] = Shield,
                ["shield_reaction"] = Shield,
                ["BG3_Shield"] = Shield,
            };
    }

    public sealed class ReactionAliasResolver : IReactionAliasResolver
    {
        public IReadOnlyDictionary<string, string> Aliases => ReactionIds.CanonicalByAlias;

        public string Resolve(string reactionId)
        {
            if (string.IsNullOrWhiteSpace(reactionId))
            {
                return reactionId;
            }

            return ReactionIds.CanonicalByAlias.TryGetValue(reactionId.Trim(), out var canonical)
                ? canonical
                : reactionId.Trim();
        }

        public bool IsKnown(string reactionId)
        {
            if (string.IsNullOrWhiteSpace(reactionId))
            {
                return false;
            }

            return ReactionIds.CanonicalByAlias.ContainsKey(reactionId.Trim());
        }
    }
}
