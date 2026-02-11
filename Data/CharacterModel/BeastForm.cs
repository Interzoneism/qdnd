using System.Collections.Generic;

namespace QDND.Data.CharacterModel
{
    /// <summary>
    /// Defines a Wild Shape beast form for Druids.
    /// Represents the physical stats and abilities of a beast transformation.
    /// </summary>
    public class BeastForm
    {
        /// <summary>
        /// Unique identifier for this beast form (e.g., "wolf", "bear", "giant_spider").
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Display name for the beast form.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Override Strength score while in this form.
        /// </summary>
        public int StrengthOverride { get; set; }

        /// <summary>
        /// Override Dexterity score while in this form.
        /// </summary>
        public int DexterityOverride { get; set; }

        /// <summary>
        /// Override Constitution score while in this form.
        /// </summary>
        public int ConstitutionOverride { get; set; }

        /// <summary>
        /// Base hit points for this beast form.
        /// This becomes temporary HP for the druid.
        /// </summary>
        public int BaseHP { get; set; }

        /// <summary>
        /// Armor class while in this form.
        /// </summary>
        public int AC { get; set; }

        /// <summary>
        /// Movement speed in feet per turn while in this form.
        /// </summary>
        public float MovementSpeed { get; set; } = 30f;

        /// <summary>
        /// List of ability IDs that are granted while in this form.
        /// These are beast-specific attacks and abilities (e.g., "bite", "claw", "web").
        /// </summary>
        public List<string> GrantedAbilities { get; set; } = new List<string>();

        /// <summary>
        /// Challenge rating of this beast form (determines druid level requirement).
        /// </summary>
        public float ChallengeRating { get; set; } = 0.25f;

        /// <summary>
        /// Tags for this beast form (e.g., "beast", "aquatic", "flying").
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Description of the beast form.
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// Pack container for deserializing multiple beast forms from JSON.
    /// </summary>
    public class BeastFormPack
    {
        public string PackId { get; set; } = "beast_forms";
        public string Version { get; set; } = "1.0";
        public List<BeastForm> BeastForms { get; set; } = new List<BeastForm>();
    }
}
