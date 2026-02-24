using System.Collections.Generic;

namespace QDND.Data.Backgrounds;

public static class BackgroundData
{
    public static readonly IReadOnlyList<BackgroundEntry> All = new List<BackgroundEntry>
    {
        new("acolyte",      "Acolyte",       new[] { "Insight",       "Religion"     }),
        new("charlatan",    "Charlatan",     new[] { "Deception",     "SleightOfHand"}),
        new("criminal",     "Criminal",      new[] { "Deception",     "Stealth"      }),
        new("entertainer",  "Entertainer",   new[] { "Acrobatics",    "Performance"  }),
        new("folk_hero",    "Folk Hero",     new[] { "AnimalHandling","Survival"     }),
        new("guild_artisan","Guild Artisan", new[] { "Insight",       "Persuasion"   }),
        new("noble",        "Noble",         new[] { "History",       "Persuasion"   }),
        new("outlander",    "Outlander",     new[] { "Athletics",     "Survival"     }),
        new("sage",         "Sage",          new[] { "Arcana",        "History"      }),
        new("soldier",      "Soldier",       new[] { "Athletics",     "Intimidation" }),
        new("urchin",       "Urchin",        new[] { "SleightOfHand", "Stealth"      }),
    };
}

public record BackgroundEntry(string Id, string Name, string[] SkillProficiencies);
