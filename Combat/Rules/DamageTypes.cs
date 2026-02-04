namespace QDND.Combat.Rules
{
    public static class DamageTypes
    {
        // Common damage type constants
        public const string Fire = "fire";
        public const string Cold = "cold";
        public const string Lightning = "lightning";
        public const string Poison = "poison";
        public const string Acid = "acid";
        public const string Thunder = "thunder";
        public const string Necrotic = "necrotic";
        public const string Radiant = "radiant";
        public const string Psychic = "psychic";
        public const string Force = "force";
        public const string Bludgeoning = "bludgeoning";
        public const string Piercing = "piercing";
        public const string Slashing = "slashing";
        public const string Untyped = "untyped";

        /// <summary>
        /// Normalizes damage type to lowercase, trimmed. Returns "untyped" if null/empty.
        /// </summary>
        public static string NormalizeType(string damageType)
        {
            if (string.IsNullOrWhiteSpace(damageType))
                return Untyped;
            return damageType.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Produces normalized "damage:<typeId>" tag
        /// </summary>
        public static string ToTag(string damageType)
        {
            return $"damage:{NormalizeType(damageType)}";
        }
    }
}
