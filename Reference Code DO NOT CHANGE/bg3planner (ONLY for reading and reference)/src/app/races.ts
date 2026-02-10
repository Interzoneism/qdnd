import { Feature, Effect } from './features'
import { EquipmentProficiency } from './equipment'
import { Skill } from './skills'
import { Spell, WIZARD_SPELLS, SPELL_ENHANCE_LEAP, SPELL_MISTY_STEP, SPELL_DANCING_LIGHTS, SPELL_FAERIE_FIRE, SPELL_DARKNESS, SPELL_ENLARGE, SPELL_INVISIBILITY, SPELL_SPEAK_WITH_ANIMALS, SPELL_MAGE_HAND, SPELL_PRODUCE_FLAME, SPELL_HELLISH_REBUKE, SPELL_BURNING_HANDS, SPELL_FLAME_BLADE, SPELL_THAUMATURGY, SPELL_SEARING_SMITE, SPELL_BRANDING_SMITE } from './spells'

export interface Race {
  name: string;
  icon: string;
  description: string;
  speed: number;
  parentRace?: Race;
  features: Feature[];
}

const RACE_HUMAN = {
  name: 'Human',
  icon: 'assets/img/races/60px-Race_Human.png',
  description: 'The most common face to see in Faerûn, humans are known for their tenacity, creativity, and endless capacity for growth.',
  speed: 9,
  features: [
    {
      name: 'Civil Militia',
      description: 'You gain proficiency with Pikes, Spears, Halberds, Glaives, Light Armor, and Shields.',
      effect:
        {
          equipmentProficiencies: [
            EquipmentProficiency.Pikes,
            EquipmentProficiency.Spears,
            EquipmentProficiency.Halberds,
            EquipmentProficiency.Glaives,
            EquipmentProficiency.LightArmor,
            EquipmentProficiency.Shields
          ] as EquipmentProficiency[]
        } as Effect
    }  as Feature,
    {
      name: 'Human Versatility',
      description: 'You gain proficiency in a skill of your choosing, and your carrying capacity is increased by 25%.',
      skillSelection: Object.values(Skill),
      selectionCount: 1,
      selectSameAllowed: false
    }  as Feature
  ] as Feature[]
} as Race;

const RACE_ELF = {
  name: 'Elf',
  icon: 'assets/img/races/60px-Race_Elf.png',
  description: 'With an ethereal countenance and long lifespans, elves are at home with nature\'s power, flourishing in light and dark alike.',
  speed: 9,
  features: [
    {
      name: 'Elven Weapon Training',
      description: 'You gain proficiency with Longswords, Shortswords, Longbows, and Shortbows.',
      effect:
        {
          equipmentProficiencies: [
            EquipmentProficiency.Longswords,
            EquipmentProficiency.Shortswords,
            EquipmentProficiency.Longbows,
            EquipmentProficiency.Shortbows
          ] as EquipmentProficiency[]
        } as Effect
    } as Feature,
    {
      name: 'Darkvision',
      description: 'You can see in the dark up to 12m/40ft.'
    } as Feature,
    {
      name: 'Fey Ancestry',
      description: 'You have Advantage on Saving Throws against being Charmed, and magic can\'t put you to Sleep.'
    } as Feature,
    {
      name: 'Keen Senses',
      description: 'You gain proficiency in the perception skill.',
      effect:
        {
          skillProficiencies: [
            Skill.Perception
          ] as Skill[]
        } as Effect
    } as Feature
  ]  as Feature[]
} as Race;

const RACE_HIGH_ELF = {
  name: 'High Elf',
  icon: 'assets/img/races/40px-Race_High_Elf.png',
  description: 'Heirs of the mystical Feywild, high elves value magic in all its forms, and even those who do not study spellcraft can manipulate the Weave.',
  speed: 9,
  parentRace: RACE_ELF,
  features: [
    {
      name: 'Cantrip',
      description: 'Choose 1 cantrip from the wizard spell list.',
      spellSelection: WIZARD_SPELLS[0],
      selectionCount: 1,
      selectSameAllowed: false
    } as Feature
  ] as Feature[]
} as Race;

const RACE_WOOD_ELF = {
  name: 'Wood Elf',
  icon: 'assets/img/races/40px-Race_Wood_Elf.png',
  description: 'Wood elves spend their reclusive lives in Faerûn\'s forests. Decades of training in archery and camouflage are enhanced by an otherworldly swiftness.',
  speed: 10.5,
  parentRace: RACE_ELF,
  features: [
    {
      name: 'Fleet of Foot',
      description: 'Your movement speed is 10.5m/35ft.'
    },
    {
      name: 'Mask of the Wild',
      description: 'You gain proficiency in stealth.',
      effect:
        {
          skillProficiencies: [
            Skill.Stealth
          ] as Skill[]
        } as Effect
    } as Feature
  ] as Feature[]
} as Race;

const RACE_DROW = {
  name: 'Drow',
  icon: 'assets/img/races/60px-Race_Drow.png',
  description: 'Driven to the Underdark, most drow have adopted a ruthless pragmatism. While the Lolth-sworn delight in the goddess\' evil tenets, the Seldarine reject Her attempt to overthrow the leader of the elven pantheon. ',
  speed: 9,
  features: [
    {
      name: 'Drow Weapon Training',
      description: 'You gain proficiency with Rapiers, Shortswords, and Hand Crossbows.',
      effect:
        {
          equipmentProficiencies: [
            EquipmentProficiency.Rapiers,
            EquipmentProficiency.Shortswords,
            EquipmentProficiency.HandCrossbows
          ] as EquipmentProficiency[]
        } as Effect
    } as Feature,
    {
      name: 'Superior Darkvision',
      description: 'You can see in the dark up to 24m/80ft.'
    } as Feature,
    {
      name: 'Fey Ancestry',
      description: 'You have Advantage on Saving Throws against being Charmed, and magic can\'t put you to Sleep.'
    } as Feature,
    {
      name: 'Keen Senses',
      description: 'You gain proficiency in the perception skill.',
      effect:
        {
          skillProficiencies: [Skill.Perception] as Skill[]
        } as Effect
    } as Feature,
    {
      name: 'Drow Magic',
      description: 'You gain access to the 1st level spell Fairie Fire (at level 3), and the second level spell Darkness (at level 5), which can be cast once per each Long Rest.',
      effect:
        {
          spells: [SPELL_FAERIE_FIRE, SPELL_DARKNESS] as Spell[]
        } as Effect
    } as Feature
  ] as Feature[]
} as Race;

const RACE_LOLTH_SWORN_DROW = {
  name: 'Lolth-Sworn Drow',
  icon: 'assets/img/races/40px-Race_Lolth-Sworn_Drow.png',
  description: 'Raised by Lolth\'s cult in the city of Menzoberranzan, these drow extol the virtues of their corrupt and merciless goddess. Lolth marks her followers with bright red eyes so the Underdark will learn to fear Drow on sight.',
  speed: 9,
  parentRace: RACE_DROW,
  features: [] as Feature[]
} as Race;

const RACE_SELDARINE_DROW = {
  name: 'Seldarine Drow',
  icon: 'assets/img/races/40px-Race_Seldarine_Drow.png',
  description: 'Drow are the result of an ancient schism between the elven dieties Corellon Larethian and Lolth. The latter\'s treachery drove the drow into the Underdark, where they splintered into warring factions. Seldarine drow can be found seeking allies from all over Faerûn, aiming to settle their conflict with Lolth - and themselves - by any means necessary.',
  speed: 9,
  parentRace: RACE_DROW,
  features: [] as Feature[]
} as Race;

const RACE_HALF_ELF = {
  name: 'Half-Elf',
  icon: 'assets/img/races/60px-Race_Half-Elf.png',
  description: 'Half-Elves inherit blessings from both their parents, but at the price of never quite fitting in. Curious, ambitious, and versatile, half-elves are welcome everywhere, but struggle without a community to call their own.',
  speed: 9,
  features: [
    {
      name: 'Civil Militia',
      description: 'You gain proficiency with Pikes, Spears, Halberds, Glaives, Light Armor, and Shields.',
      effect:
        {
          equipmentProficiencies: [
            EquipmentProficiency.Pikes,
            EquipmentProficiency.Spears,
            EquipmentProficiency.Halberds,
            EquipmentProficiency.Glaives,
            EquipmentProficiency.LightArmor,
            EquipmentProficiency.Shields
          ] as EquipmentProficiency[]
        } as Effect
    }  as Feature,
    {
      name: 'Darkvision',
      description: 'You can see in the dark up to 12m/40ft.'
    } as Feature,
    {
      name: 'Fey Ancestry',
      description: 'You have Advantage on Saving Throws against being Charmed, and magic can\'t put you to Sleep.'
    } as Feature
  ] as Feature[]
} as Race;

const RACE_HIGH_HALF_ELF = {
  name: 'High Half-Elf',
  icon: 'assets/img/races/40px-Race_High_Half-Elf.png',
  description: 'A touch of the Feywild remains in half-elves with this bloodline, and even those untrained in magic possess a hint of wild power.',
  speed: 9,
  parentRace: RACE_HALF_ELF,
  features: [
    {
      name: 'Cantrip',
      description: 'Choose 1 cantrip from the wizard spell list.',
      spellSelection: WIZARD_SPELLS[0],
      selectionCount: 1,
      selectSameAllowed: false
    } as Feature
  ] as Feature[]
} as Race;

const RACE_WOOD_HALF_ELF = {
  name: 'Wood Half-Elf',
  icon: 'assets/img/races/40px-Race_Wood_Half-Elf.png',
  description: 'Like their wood elf parent, these half-elves have a quickened stride and eye for stealth. Yet many break away from isolation in Faerûn\'s forests to explore the rest of the Realms.',
  speed: 10.5,
  parentRace: RACE_HALF_ELF,
  features: [
    {
      name: 'Fleet of Foot',
      description: 'Your movement speed is 10.5m/35ft.'
    },
    {
      name: 'Mask of the Wild',
      description: 'You gain proficiency in stealth.',
      effect:
        {
          skillProficiencies: [
            Skill.Stealth
          ] as Skill[]
        } as Effect
    } as Feature
  ] as Feature[]
} as Race;

const RACE_DROW_HALF_ELF = {
  name: 'Drow Half-Elf',
  icon: 'assets/img/races/40px-Race_Drow_Half-Elf.png',
  description: 'Most Half-Drow result from liaisons between Seldarine Drow and surfacers. While Half-Drow inherit a few magical gifts, they aren\'t usually raised in the Underdark.',
  speed: 9,
  parentRace: RACE_HALF_ELF,
  features: [
    {
      name: 'Drow Magic',
      description: 'You gain access to the Cantrip Dancing Lights (at level 1), 1st level spell Fairie Fire (at level 3), and the second level spell Darkness (at level 5), which can be cast once per each Long Rest.',
      effect:
        {
          spells: [SPELL_DANCING_LIGHTS, SPELL_FAERIE_FIRE, SPELL_DARKNESS] as Spell[]
        } as Effect
    } as Feature
  ] as Feature[]
} as Race;

const RACE_HALF_ORC = {
  name: 'Half-Orc',
  icon: 'assets/img/races/60px-Race_Half-Orc.png',
  description: 'Half-Orcs exhibit a blend of orcish and human characteristics, and their appearance varies widely. Creatures of intense emotion, Half-Orcs are more inclined to act than contemplate - whether the rage burning their bodies compels them to fight, or the love filling their hearts inspires acts of incredible kindness.',
  speed: 9,
  features: [
    {
      name: 'Darkvision',
      description: 'You can see in the dark up to 12m/40ft.'
    } as Feature,
    {
      name: 'Menacing',
      description: 'You gain proficiency in the Intimidation skill.',
      effect: { skillProficiencies: [Skill.Intimidation] as Skill[] } as Effect
    } as Feature,
    {
      name: 'Relentless Endurance',
      description: 'Once per Long Rest, if you reach 0 Hit Points, you regain 1 hit point instead of becoming Downed.'
    } as Feature,
    {
      name: 'Savage Attacks',
      description: 'When you score a Critical Hit with a melee Weapon attack, an additional Damage Dice is added to your total damage.'
    } as Feature
  ] as Feature[]
} as Race;

const RACE_HALFLING = {
  name: 'Halfling',
  icon: 'assets/img/races/60px-Race_Halfling.png',
  description: 'Small yet capable, halflings prefer the comforts of home and hearth - but their natural luck and dexterity makes them fine adventurers.',
  speed: 7.5,
  features: [
    {
      name: 'Lucky',
      description: 'When you roll a 1 for an Attack Roll, Ability Check, or Saving Throw, you can reroll the die and must use the new roll.'
    } as Feature,
    {
      name: 'Brave',
      description: 'You have Advantage on Saving Throws against being Frightened.'
    } as Feature
  ] as Feature[]
} as Race;

const RACE_LIGHTFOOT_HALFLING = {
  name: 'Lightfoot Halfling',
  icon: 'assets/img/races/40px-Race_Lightfoot_Halfling.png',
  description: 'Lightfoot Halflings are stealthy but social, travelling all over Faerûn to make a name for themselves.',
  speed: 7.5,
  parentRace: RACE_HALFLING,
  features: [
    {
      name: 'Naturally Stealthy',
      description: 'You have Advantage on Stealth Checks.'
    } as Feature
  ] as Feature[]
} as Race;

const RACE_STRONGHEART_HALFLING = {
  name: 'Strongheart Halfling',
  icon: 'assets/img/races/40px-Race_Strongheart_Halfling.png',
  description: 'Legends say dwarven blood gave stronghearts their hardiness. Resistant to poison and wellsprings of endurance, these halflings easily hold their own.',
  speed: 7.5,
  parentRace: RACE_HALFLING,
  features: [
    {
      name: 'Strongheart Resilience',
      description: 'You have Advantage on Saving Throws against Poison, and have Resistance to Poison damage.'
    } as Feature
  ] as Feature[]
} as Race;

const RACE_DWARF = {
  name: 'Dwarf',
  icon: 'assets/img/races/60px-Race_Dwarf.png',
  description: 'As durable and unyielding as their homes of stone, dwarves are some of the finest warriors, miners, and smiths of Faerun',
  speed: 7.5,
  features: [
    {
      name: 'Dwarven Combat Training',
      description: 'You gain proficiency with Battleaxes, Handaxes, Light Hammers, and Warhammers.',
      effect:
        {
          equipmentProficiencies: [
            EquipmentProficiency.Battleaxes,
            EquipmentProficiency.Handaxes,
            EquipmentProficiency.LightHammers,
            EquipmentProficiency.Warhammers
          ] as EquipmentProficiency[]
        } as Effect
    }  as Feature,
    {
      name: 'Darkvision',
      description: 'You can see in the dark up to 12m/40ft.'
    } as Feature,
    {
      name: 'Dwarven Resilience',
      description: 'You have Advantage on Saving Throws against Poison, and have Resistance to Poison damage.'
    } as Feature
  ] as Feature[]
} as Race;

const RACE_GOLD_DWARF = {
  name: 'Gold Dwarf',
  icon: 'assets/img/races/40px-Race_Hill_Dwarf.png',
  description: 'Gold Dwarves are known for their confidence and keen intuition. The culture of their Deep Kingdom values family, ritual, and fine craftsmanship.',
  speed: 7.5,
  parentRace: RACE_DWARF,
  features: [
    {
      name: 'Dwarven Toughness',
      description: 'Your Hit Point maximum increases by 1, and increases by 1 again every time you gain a level.'
    } as Feature
  ] as Feature[]
} as Race;

const RACE_SHIELD_DWARF = {
  name: 'Shield Dwarf',
  icon: 'assets/img/races/40px-Race_Mountain_Dwarf.png',
  description: 'Shield Dwarves survived a long fall from grace, surrendering many of their ancient kingdoms in wars with Goblins and Orcs. These losses have lead to a cynical mindset, yet Shield Dwarves will endure anything to restore their ancestral homelands.',
  speed: 7.5,
  parentRace: RACE_DWARF,
  features: [
    {
      name: 'Dwarven Armor Training',
      description: 'You gain proficiency with Light and Medium Armor.',
      effect:
        {
          equipmentProficiencies: [
            EquipmentProficiency.LightArmor,
            EquipmentProficiency.MediumArmor
          ] as EquipmentProficiency[]
        } as Effect
    }  as Feature
  ] as Feature[]
} as Race;

const RACE_DUERGAR = {
  name: 'Duergar',
  icon: 'assets/img/races/40px-Race_Duergar.png',
  description: 'Once enslaved by the eldritch Mind Flayers, Duergar adapted to freedom with harsh practicality. Their cold demeanours and gift of stealth are well-known through the Underdark.',
  speed: 7.5,
  parentRace: RACE_DWARF,
  features: [
    {
      name: 'Superior Darkvision',
      description: 'You can see in the dark up to 24m/80ft.'
    } as Feature,
    {
      name: 'Duergar Resilience',
      description: 'You have Advantage on Saving Throws against illusions, as well as against being Charmed or Paralysed.'
    } as Feature,
    {
      name: 'Duergar Magic',
      description: 'You gain access to the 1st level spell Enlarge (at level 3), and the 2nd level spell Invisibility (at level 5), which can be cast once per each Long Rest.',
      effect: { spells: [SPELL_ENLARGE, SPELL_INVISIBILITY] as Spell[] } as Effect
    } as Feature
  ] as Feature[]
} as Race;

const RACE_GNOME = {
  name: 'Gnome',
  icon: 'assets/img/races/60px-Race_Gnome.png',
  description: 'Small, clever, and energetic, gnomes use their long lives to explore Faerûn\'s brightest corners and darkest depths.',
  speed: 7.5,
  features: [
    {
      name: 'Gnome Cunning',
      description: 'You gain Advantage on Intelligence, Wisdom, and Charisma Saving Throws.'
    } as Feature,
  ] as Feature[]
} as Race;

const RACE_ROCK_GNOME = {
  name: 'Rock Gnome',
  icon: 'assets/img/races/40px-Race_Rock_Gnome.png',
  description: 'The most commonly seen gnome on Faerûn\'s surface, Rock Gnomes are named as such for their hardiness and affinity to metal.',
  speed: 7.5,
  parentRace: RACE_GNOME,
  features: [
    {
      name: 'Darkvision',
      description: 'You can see in the dark up to 12m/40ft.'
    } as Feature,
    {
      name: 'Artificer\'s Lore',
      description: 'Add twice your Proficiency Bonus to History Checks.'
    } as Feature
  ] as Feature[]
} as Race;

const RACE_FOREST_GNOME = {
  name: 'Forest Gnome',
  icon: 'assets/img/races/40px-Race_Forest_Gnome.png',
  description: 'Even smaller than their cousins and twice as reclusive, Forest Gnomes are rare sight in Faerûn. They master magic and craftmanship in their distant, idyllic groves.',
  speed: 7.5,
  parentRace: RACE_GNOME,
  features: [
    {
      name: 'Darkvision',
      description: 'You can see in the dark up to 12m/40ft.'
    } as Feature,
    {
      name: 'Speak with Animals',
      description: 'Once per Long Rest, you may cast the spell \'Speak with Animals\'.',
      effect: { spells: [SPELL_SPEAK_WITH_ANIMALS] as Spell[] } as Effect
    } as Feature
  ] as Feature[]
} as Race;

const RACE_DEEP_GNOME = {
  name: 'Deep Gnome',
  icon: 'assets/img/races/40px-Race_Deep_Gnome.png',
  description: 'More guarded than their surface cousins, Deep Gnomes survive in the Underdark with darkvision and skillful stealth.',
  speed: 7.5,
  parentRace: RACE_GNOME,
  features: [
      {
        name: 'Superior Darkvision',
        description: 'You can see in the dark up to 24m/80ft.'
      } as Feature,
      {
        name: 'Stone Camouflage',
        description: 'You have Advantage on Stealth Checks'
      } as Feature
  ] as Feature[]
} as Race;

const RACE_TIEFLING = {
  name: 'Tiefling',
  icon: 'assets/img/races/60px-Race_Tiefling.png',
  description: 'Descended from devils of the Nine Hells, tieflings face constant suspicion in Faerûn. Thankfully, their arcane abilities make them natural survivors.',
  speed: 9,
  features: [
    {
      name: 'Darkvision',
      description: 'You can see in the dark up to 12m/40ft.'
    } as Feature,
    {
      name: 'Hellish Resistance',
      description: 'Your blood protects you from flame, abyssal or otherwise. Gain resistance to Fire damage, taking only half damage from it.'
    } as Feature
  ] as Feature[]
} as Race;

const RACE_ASMODEUS_TIEFLING = {
  name: 'Asmodeus Tiefling',
  icon: 'assets/img/races/40px-Race_Asmodeus_Tiefling.png',
  description: 'Bound to Nessus, the deepest layer of the Hells, these Tieflings inherit the ability to wield fire and darkness from the archdevil Asmodeus\' infernal bloodline.',
  speed: 9,
  parentRace: RACE_TIEFLING,
  features: [
    {
      name: 'Asmodeus Magic',
      description: 'You gain access to the Cantrip Produce Flame (at level 1), 1st level spell Hellish Rebuke (at level 3), and the 2nd level spell Darkness (at level 5), which can be cast once per each Long Rest.',
      effect: { spells: [SPELL_PRODUCE_FLAME, SPELL_HELLISH_REBUKE, SPELL_DARKNESS] as Spell[] } as Effect
    } as Feature
  ] as Feature[]
} as Race;

const RACE_MEPHISTOPHELES_TIEFLING = {
  name: 'Mephistopheles Tiefling',
  icon: 'assets/img/races/40px-Race_Mephistopheles_Tiefling.png',
  description: 'Descended from the archdevil Mephistopheles, these Tieflings are gifted with a particular affinity for arcane magic.',
  speed: 9,
  parentRace: RACE_TIEFLING,
  features: [
    {
      name: 'Mephistopheles Magic',
      description: 'You gain access to the Cantrip Mage Hand (at level 1), 1st level spell Burning Hands (at level 3), and the 2nd level spell Flame Blade (at level 5), which can be cast once per each Long Rest.',
      effect: { spells: [SPELL_MAGE_HAND, SPELL_BURNING_HANDS, SPELL_FLAME_BLADE] as Spell[] } as Effect
    } as Feature
  ] as Feature[]
} as Race;

const RACE_ZARIEL_TIEFLING = {
  name: 'Zariel Tiefling',
  icon: 'assets/img/races/40px-Race_Zariel_Tiefling.png',
  description: 'Tieflings from Zariel\'s bloodlines are empowered with martial strength, and can channel searing flame to punish their enemies.',
  speed: 9,
  parentRace: RACE_TIEFLING,
  features: [
    {
      name: 'Zariel Magic',
      description: 'You gain access to the Cantrip Thaumaturgy (at level 1), 1st level spell Searing Smite (at level 3), and the 2nd level spell Branding Smite (at level 5), which can be cast once per each Long Rest.',
      effect: { spells: [SPELL_THAUMATURGY, SPELL_SEARING_SMITE, SPELL_BRANDING_SMITE] as Spell[] } as Effect
    } as Feature
  ] as Feature[]
} as Race;

const RACE_GITHYANKI = {
  name: 'Githyanki',
  icon: 'assets/img/races/60px-Race_Githyanki.png',
  description: 'Githyanki are peerless warriors from the Astral Plane, known for their legendary silver blades and red dragon mounts. They seek the total destruction of mind flayers, whose ancient empire enslaved the githyanki for millennia.',
  speed: 9,
  features: [
    {
      name: 'Astral Knowledge',
      description: 'Once per long rest, gain Proficiency in all Skills corresponding to a chosen Ability.'
    } as Feature,
    {
      name: 'Githyanki Psionics',
      description: 'You gain access to the Cantrip Mage Hand (at level 1), 1st level spell Enhance Leap (at level 3), and 2nd level spell Misty Step (at level 5), which can be cast once per each Long Rest.',
      effect: { spells: [SPELL_MAGE_HAND, SPELL_ENHANCE_LEAP, SPELL_MISTY_STEP] as Spell[] } as Effect
    } as Feature,
    {
      name: 'Martial Prodigy',
      description: 'You gain proficiency with Light Armor, Medium Armor, Shortswords, Longswords, and Greatswords.',
      effect:
        {
          equipmentProficiencies: [
            EquipmentProficiency.LightArmor,
            EquipmentProficiency.MediumArmor,
            EquipmentProficiency.Shortswords,
            EquipmentProficiency.Longswords,
            EquipmentProficiency.Greatswords
          ] as EquipmentProficiency[]
        } as Effect
    }  as Feature
  ] as Feature[]
} as Race;

const RACE_DRAGONBORN = {
  name: 'Dragonborn',
  icon: 'assets/img/races/60px-Race_Dragonborn.png',
  description: 'Dragonborn resemble draconic humanoids. They are a proud race that values clan and skills above all else. Once enslaved by dragons, they strive to be self-sufficient, not wanting to be beholden to anyone, not even the gods. Dragonborn can have scales in a variety of colours, and may or may not have a tail.',
  speed: 9,
  features: [] as Feature[]
} as Race;

const RACE_BLACK_DRAGONBORN = {
  name: 'Black Dragonborn',
  icon: 'assets/img/races/40px-Race_Black_Dragonborn.png',
  description: 'Despite no ancestral links to the mighty creatures, these dragonborn share the charcoal colouration and fizzling, acid breath of black dragons.',
  speed: 9,
  parentRace: RACE_DRAGONBORN,
  features: [
    {
      name: 'Acid Breath',
      description: 'Spew forth a cone of acid.',
    } as Feature,
    {
      name: 'Draconic Ancestry (Acid)',
      description: 'The blood of ancient dragons flow through your veins. You are Resistant to Acid damage.',
    } as Feature
  ] as Feature[]
} as Race;

const RACE_BLUE_DRAGONBORN = {
  name: 'Blue Dragonborn',
  icon: 'assets/img/races/40px-Race_Blue_Dragonborn.png',
  description: 'Despite no ancestral links to the mighty creatures, these dragonborn share the deep sapphire scales and charged, crackling breath of blue dragons.',
  speed: 9,
  parentRace: RACE_DRAGONBORN,
  features: [
    {
      name: 'Lightning Breath',
      description: 'Spew forth a cone of lightning.',
    } as Feature,
    {
      name: 'Draconic Ancestry (Lightning)',
      description: 'The blood of ancient dragons flow through your veins. You are Resistant to Lightning damage.',
    } as Feature
  ] as Feature[]
} as Race;

const RACE_BRASS_DRAGONBORN = {
  name: 'Brass Dragonborn',
  icon: 'assets/img/races/40px-Race_Brass_Dragonborn.png',
  description: 'Despite no ancestral links to the mighty creatures, these dragonborn share the burnished ochre hue and flickering, fiery breath of brass dragons.',
  speed: 9,
  parentRace: RACE_DRAGONBORN,
  features: [
    {
      name: 'Fire Breath (Line)',
      description: 'Spew forth a column of fire.',
    } as Feature,
    {
      name: 'Draconic Ancestry (Fire)',
      description: 'The blood of ancient dragons flow through your veins. You are Resistant to Fire damage.',
    } as Feature
  ] as Feature[]
} as Race;

const RACE_BRONZE_DRAGONBORN = {
  name: 'Bronze Dragonborn',
  icon: 'assets/img/races/40px-Race_Bronze_Dragonborn.png',
  description: 'Despite no ancestral links to the mighty creatures, these dragonborn share the shining sepia scales and sparkling breath of bronze dragons.',
  speed: 9,
  parentRace: RACE_DRAGONBORN,
  features: [
    {
      name: 'Lightning Breath',
      description: 'Spew forth a cone of lightning.',
    } as Feature,
    {
      name: 'Draconic Ancestry (Lightning)',
      description: 'The blood of ancient dragons flow through your veins. You are Resistant to Lightning damage.',
    } as Feature
  ] as Feature[]
} as Race;

const RACE_COPPER_DRAGONBORN = {
  name: 'Copper Dragonborn',
  icon: 'assets/img/races/40px-Race_Copper_Dragonborn.png',
  description: 'Despite no ancestral links to the mighty creatures, these dragonborn share the pink-gold colouration and corrosive breath of copper dragons.',
  speed: 9,
  parentRace: RACE_DRAGONBORN,
  features: [
    {
      name: 'Acid Breath',
      description: 'Spew forth a cone of acid.',
    } as Feature,
    {
      name: 'Draconic Ancestry (Acid)',
      description: 'The blood of ancient dragons flow through your veins. You are Resistant to Acid damage.',
    } as Feature
  ] as Feature[]
} as Race;

const RACE_GOLD_DRAGONBORN = {
  name: 'Gold Dragonborn',
  icon: 'assets/img/races/40px-Race_Gold_Dragonborn.png',
  description: 'Despite no ancestral links to the mighty creatures, these dragonborn share the resplendent shine and roiling, blazing breath of gold dragons.',
  speed: 9,
  parentRace: RACE_DRAGONBORN,
  features: [
    {
      name: 'Fire Breath (Cone)',
      description: 'Spew forth a cone of fire.',
    } as Feature,
    {
      name: 'Draconic Ancestry (Fire)',
      description: 'The blood of ancient dragons flow through your veins. You are Resistant to Fire damage.',
    } as Feature
  ] as Feature[]
} as Race;

const RACE_GREEN_DRAGONBORN = {
  name: 'Green Dragonborn',
  icon: 'assets/img/races/40px-Race_Green_Dragonborn.png',
  description: 'Despite no ancestral links to the mighty creatures, these dragonborn share the brilliant emerald aspect and stinking, putrid breath of green dragons.',
  speed: 9,
  parentRace: RACE_DRAGONBORN,
  features: [
    {
      name: 'Poison Breath',
      description: 'Spew forth a cone of poison.',
    } as Feature,
    {
      name: 'Draconic Ancestry (Poison)',
      description: 'The blood of ancient dragons flow through your veins. You are Resistant to Poison damage.',
    } as Feature
  ] as Feature[]
} as Race;

const RACE_RED_DRAGONBORN = {
  name: 'Red Dragonborn',
  icon: 'assets/img/races/40px-Race_Red_Dragonborn.png',
  description: 'Despite no ancestral links to the mighty creatures, these dragonborn share the bright scarlet likeness and roiling, burning breath of red dragons.',
  speed: 9,
  parentRace: RACE_DRAGONBORN,
  features: [
    {
      name: 'Fire Breath (Cone)',
      description: 'Spew forth a cone of fire.',
    } as Feature,
    {
      name: 'Draconic Ancestry (Fire)',
      description: 'The blood of ancient dragons flow through your veins. You are Resistant to Fire damage.',
    } as Feature
  ] as Feature[]
} as Race;

const RACE_SILVER_DRAGONBORN = {
  name: 'Silver Dragonborn',
  icon: 'assets/img/races/40px-Race_Silver_Dragonborn.png',
  description: 'Despite no ancestral links to the mighty creatures, these dragonborn share the glinting shine and scorching cold breath of silver dragons.',
  speed: 9,
  parentRace: RACE_DRAGONBORN,
  features: [
    {
      name: 'Frost Breath',
      description: 'Spew forth a cone of ice.',
    } as Feature,
    {
      name: 'Draconic Ancestry (Cold)',
      description: 'The blood of ancient dragons flow through your veins. You are Resistant to Cold damage.',
    } as Feature
  ] as Feature[]
} as Race;

const RACE_WHITE_DRAGONBORN = {
  name: 'White Dragonborn',
  icon: 'assets/img/races/40px-Race_White_Dragonborn.png',
  description: 'Despite no ancestral links to the mighty creatures, these dragonborn share the snowy aspect and frosty breath of white dragons.',
  speed: 9,
  parentRace: RACE_DRAGONBORN,
  features: [
    {
      name: 'Frost Breath',
      description: 'Spew forth a cone of ice.',
    } as Feature,
    {
      name: 'Draconic Ancestry (Cold)',
      description: 'The blood of ancient dragons flow through your veins. You are Resistant to Cold damage.',
    } as Feature
  ] as Feature[]
} as Race;


export const RACES = [
  RACE_HUMAN,
  RACE_HIGH_ELF,
  RACE_WOOD_ELF,
  RACE_LOLTH_SWORN_DROW,
  RACE_SELDARINE_DROW,
  RACE_HIGH_HALF_ELF,
  RACE_WOOD_HALF_ELF,
  RACE_DROW_HALF_ELF,
  RACE_HALF_ORC,
  RACE_LIGHTFOOT_HALFLING,
  RACE_STRONGHEART_HALFLING,
  RACE_GOLD_DWARF,
  RACE_SHIELD_DWARF,
  RACE_DUERGAR,
  RACE_ROCK_GNOME,
  RACE_FOREST_GNOME,
  RACE_DEEP_GNOME,
  RACE_ASMODEUS_TIEFLING,
  RACE_MEPHISTOPHELES_TIEFLING,
  RACE_ZARIEL_TIEFLING,
  RACE_GITHYANKI,
  RACE_BLACK_DRAGONBORN,
  RACE_BLUE_DRAGONBORN,
  RACE_BRASS_DRAGONBORN,
  RACE_BRONZE_DRAGONBORN,
  RACE_COPPER_DRAGONBORN,
  RACE_GOLD_DRAGONBORN,
  RACE_GREEN_DRAGONBORN,
  RACE_RED_DRAGONBORN,
  RACE_SILVER_DRAGONBORN,
  RACE_WHITE_DRAGONBORN
] as Race[];
