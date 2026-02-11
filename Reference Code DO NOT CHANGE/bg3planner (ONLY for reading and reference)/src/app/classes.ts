import { Skill } from './skills'
import { EquipmentProficiency } from './equipment'
import { Feature, Effect } from './features'
import { Spell, SPELL_SPEAK_WITH_ANIMALS, SPELL_BURNING_HANDS, SPELL_MAGE_HAND } from './spells'
import { Ability } from './abilities'

export interface Class {
  name: string;
  icon: string;
  flavorText: string;
  description: string;
  savingThrowProficiencies: Ability[];
  skillProficiencies: Feature;
  equipmentProficiencies: EquipmentProficiency[];
  initialEquipmentProficiencies: EquipmentProficiency[];
  features: Feature[][];
  featLevels: number[];
  subclassLevel: number;
  subclasses: Subclass[];
}

export interface Subclass {
  name: string;
  icon: string;
  description: string;
  equipmentProficiencies?: EquipmentProficiency[];
  features: Feature[][];
}


const BARBARIAN_SKILLS =
  [
    Skill.Athletics,
    Skill.Nature,
    Skill.AnimalHandling,
    Skill.Perception,
    Skill.Survival,
    Skill.Intimidation
  ] as Skill[];

const BARD_SKILLS =
  [
    Skill.Athletics,
    Skill.Acrobatics,
    Skill.SleightOfHand,
    Skill.Stealth,
    Skill.Arcana,
    Skill.History,
    Skill.Investigation,
    Skill.Nature,
    Skill.Religion,
    Skill.AnimalHandling,
    Skill.Insight,
    Skill.Medicine,
    Skill.Perception,
    Skill.Survival,
    Skill.Deception,
    Skill.Intimidation,
    Skill.Performance,
    Skill.Persuasion
  ] as Skill[];

const CLERIC_SKILLS =
  [
    Skill.History,
    Skill.Religion,
    Skill.Insight,
    Skill.Medicine,
    Skill.Persuasion
  ] as Skill[];

const DRUID_SKILLS =
  [
    Skill.Arcana,
    Skill.Nature,
    Skill.Religion,
    Skill.AnimalHandling,
    Skill.Insight,
    Skill.Medicine,
    Skill.Perception,
    Skill.Survival
  ] as Skill[];

const FIGHTER_SKILLS =
  [
    Skill.Athletics,
    Skill.Acrobatics,
    Skill.History,
    Skill.AnimalHandling,
    Skill.Insight,
    Skill.Perception,
    Skill.Survival,
    Skill.Intimidation
  ] as Skill[];

const MONK_SKILLS =
  [
    Skill.Athletics,
    Skill.Acrobatics,
    Skill.Stealth,
    Skill.History,
    Skill.Religion,
    Skill.Insight
  ] as Skill[];

const PALADIN_SKILLS =
  [
    Skill.Athletics,
    Skill.Religion,
    Skill.Insight,
    Skill.Medicine,
    Skill.Intimidation,
    Skill.Persuasion
  ] as Skill[];

const RANGER_SKILLS =
  [
    Skill.Athletics,
    Skill.Stealth,
    Skill.Investigation,
    Skill.Nature,
    Skill.AnimalHandling,
    Skill.Insight,
    Skill.Perception,
    Skill.Survival
  ] as Skill[];

const ROGUE_SKILLS =
  [
    Skill.Athletics,
    Skill.Acrobatics,
    Skill.SleightOfHand,
    Skill.Stealth,
    Skill.Investigation,
    Skill.Insight,
    Skill.Perception,
    Skill.Deception,
    Skill.Intimidation,
    Skill.Performance,
    Skill.Persuasion
  ] as Skill[];

const SORCERER_SKILLS =
  [
    Skill.Arcana,
    Skill.Religion,
    Skill.Insight,
    Skill.Deception,
    Skill.Intimidation,
    Skill.Persuasion
  ] as Skill[];

const WARLOCK_SKILLS =
  [
    Skill.Arcana,
    Skill.History,
    Skill.Investigation,
    Skill.Nature,
    Skill.Religion,
    Skill.Deception,
    Skill.Intimidation
  ] as Skill[];

const WIZARD_SKILLS =
  [
    Skill.Arcana,
    Skill.History,
    Skill.Investigation,
    Skill.Religion,
    Skill.Insight,
    Skill.Medicine
  ] as Skill[];


  const ELDRITCH_INVOCATIONS_LV2 = [
    {
      name: 'Agonising Blast',
      description: 'When you cast Eldritch Blast, add your Charisma Modifier to the damage it deals, unless it is negative.'
    },
    {
      name: 'Armour of Shadows',
      description: 'You can cast Mage Armour on yourself at will, without expending a Spell Slot. Mage Armour increases your Armour Class when you are not wearing armour.'
      // todo: add Mage Armor spell
    },
    {
      name: 'Beast Speech',
      description: 'You can cast Speak with Animals on yourself at will, without expending a Spell Slot.',
      effect: {
        spells: [SPELL_SPEAK_WITH_ANIMALS]
      }
    },
    {
      name: 'Beguiling Influence',
      description: 'You invoke your patron\'s bewitching charm. You gain Proficiency in the Deception and Persuasion.',
      effect: {
        skillProficiencies: [Skill.Deception, Skill.Persuasion] as Skill[]
      }
    },
    {
      name: 'Devil\'s Sight',
      description: 'You can see normally in darkness, both magical and non-magical, to a distance of 24m / 80ft.'
    },
    {
      name: 'Fiendish Vigour',
      description: 'You can cast False Life on yourself at will as a 1st-level spell, without expending a Spell Slot. False Life grants you 7 Temporary Hit Points.'
      // todo: add False Life spell
    },
    {
      name: 'Mask of Many Faces',
      description: 'You can cast Disguise Self on yourself at will, without expending a Spell Slot.'
      // todo: add Disguise Self spell
    },
    {
      name: 'One with Shadows',
      description: 'When you are in an area of dim light or darkness, you can use your action to become invisible. Invisibility ends early if you move, attack, cast another spell, take an action, or take damage.'
    },
    {
      name: 'Repelling Blast',
      description: 'When you hit a creature with Eldritch Blast, you can push the creature up to 4.5m / 15ft away from you.'
    },
    {
      name: 'Thief of Five Fates',
      description: 'You can cast Bane using a Warlock Spell Slot once a long rest. Bane targets up to 3 creatures (+1 creature per spell slot level above 1st). They receive a 1d4 penalty to Attack Rolls and Saving Throws.'
      // todo: add Bane spell
    }
  ] as Feature[]

  const ELDRITCH_INVOCATIONS_LV5 = [
    {
      name: 'Sign of Ill Omen',
      description: 'You learn how to cast Bestow Curse with a Warlock spell slot.'
      // todo: add Bestow Curse spell
    },
    {
      name: 'Mire the Mind',
      description: 'You can cast Slow with a Warlock spell slot.'
      // todo: add Slow spell
    }
  ] as Feature[]

  const ELDRITCH_INVOCATIONS_LV7 = [
    {
      name: 'Book of Ancient Secrets',
      description: 'You can inscribe magical rituals in your Book of Shadows. Gain Ray of Sickness, Chromatic Orb, and Silence spells. You can cast them once per Long Rest and they don\'t expend spell slots.'
      // todo: add Ray of Sickness, Chromatic Orb, Silence spell
    },
    {
      name: 'Dreadful Word',
      description: 'You can cast Confusion with a Warlock spell slot.'
      // todo: add Confusion spell
    },
    {
      name: 'Sculptor of Flesh',
      description: 'You can cast Polymorph with a Warlock spell slot.'
      // todo: add Polymorph spell
    }
  ] as Feature[]

  const ELDRITCH_INVOCATIONS_LV9 = [
    {
      name: 'Minions of Chaos',
      description: 'You can cast Conjure Elemental with a Warlock spell slot.'
      // todo: add Conjure Elemental spell
    }
  ] as Feature[]

  const ELDRITCH_INVOCATIONS_LV12 = [
    {
      name: 'Lifedrinker',
      description: 'Your melee attacks deal additional necrotic damage equal to your Charisma modifier.'
    }
  ] as Feature[]


const FAVOURED_ENEMY = {
  name: 'Favoured Enemy',
  description: 'Choose 1 favoured enemy type.',
  featureSelection: [
    {
      name: 'Bounty Hunter',
      description: 'Gain Proficiency in Investigation. Creatures you hit with Ensnaring Strike (either ranged or melee) have Disadvantage on their Saving Throw.',
      effect: {
        skillProficiencies: [Skill.Investigation]
      }
    },
    {
      name: 'Keeper of the Veil',
      description: 'You specialise in hunting creatures from other planes of existence. You gain Proficiency in Arcana and can grant protection against aberrations, celestials, elementals, fey, fiends, and undead.',
      effect: {
        skillProficiencies: [Skill.Arcana]
        // todo: Add Protection from Evil and Good spell
      }
    },
    {
      name: 'Mage Breaker',
      description: 'You have a history of battling spellcasters. Gain Proficiency with Arcana and the True Strike Cantrip, which gives you Advantage on Attack Rolls against a creature. Wisdom is your Spellcasting Ability for this spell.',
      effect: {
        skillProficiencies: [Skill.Arcana]
        // todo: Add True Strike spell
      }
    },
    {
      name: 'Ranger Knight',
      description: 'You have sworn to serve a crown or nation and seek to bring its foes to ruin. Gain Proficiency with History and Proficiency with Heavy Armour.',
      effect: {
        equipmentProficiencies: [EquipmentProficiency.HeavyArmor],
        skillProficiencies: [Skill.History]
      }
    },
    {
      name: 'Sanctified Stalker',
      description: 'You swore to hunt the enemies of a holy or druidic order. Gain Proficiency with Religion and the Sacred Flame Cantrip, which deals 1d8 Radiant damage. Wisdom is your Spellcasting Ability for the Cantrip.',
      effect: {
        skillProficiencies: [Skill.Religion]
        // todo: Add Sacred Flame spell
      }
    }
  ] as Feature[],
  selectionCount: 1,
  selectSameAllowed: false
}


export const BATTLE_MANOEUVERS = [
  {
    name: 'Commander\'s Strike',
    description: 'Expend one attack from your attack action and a bonus action to direct an ally to strike a foe. The ally uses their reaction to make a weapon attack on their turn.'
  },
  {
    name: 'Disarming Attack',
    description: 'Spend a superiority die to make an attack that deals an additional 1d8 damage and possibly forces the target to drop the weapons they are holding.'
  },
  {
    name: 'Distracting Strike',
    description: 'Distract your target, giving your allies Advantage on their next Attack Roll against the target.'
  },
  {
    name: 'Evasive Footwork',
    description: 'You can evade attacks by imposing Disadvantage on melee attacks against you for a round.'
  },
  {
    name: 'Feinting Attack',
    description: 'Use can use both your action and bonus action on a turn to attack a target with Advantage and deal an addition 1d8 damage.'
  },
  {
    name: 'Goading Attack',
    description: 'Deal an additional 1d and attempt to goad the target attacking you. Target recieved Disadvantage on attacking any other creature.'
  },
  {
    name: 'Manoeuvring Attack',
    description: 'Spend a superiority die to make an attack that deals an additional 1d8 damage. On a hit, select which friendly creature will gain half its movement speed. It will not provoke attacks of opportunity.'
  },
  {
    name: 'Menacing Attack',
    description: 'Spend a superiority die to make an attack that deals an additional 1d8 damage and possibly Frightens the target.'
  },
  {
    name: 'Precision Attack',
    description: 'Your can spend a Superiority Die to add it to a result to an Attack Roll.'
  },
  {
    name: 'Pushing Attack',
    description: 'Spend a superiority die to make an attack that deals an additional 1d8 damage and possibly pushes the target back 4.5m.'
  },
  {
    name: 'Rally',
    description: 'Expend a superiority die to grant an ally 8 temporary hit points, bolstering their resolve.'
  },
  {
    name: 'Riposte',
    description: 'When a hostile creature misses you with a melee attack, expend a superiority die to retaliate with a powerful attack that deals an additional 1d8 damage.'
  },
  {
    name: 'Sweeping Attack',
    description: 'Swing your weapon in a rapid, sweeping arc to attack multiple enemies at once. Roll you superiority die for damage.'
  },
  {
    name: 'Trip Attack',
    description: 'Spend a superiority die to make an attack that deals an additional 1d8 damage and possibly knocks the target Prone. The target must be Large or smaller.'
  }
] as Feature[]


const NATURAL_EXPLORER = {
  name: 'Natural Explorer',
  description: 'Choose 1 natural explorer archetype.',
  featureSelection: [
    {
      name: 'Beast Tamer',
      description: 'You have cultivated a strong bond with animals. You can cast Find Familiar as a ritual.',
      // todo add Find Familiar spell
    },
    {
      name: 'Urban Tracker',
      description: 'An expert at navigating the wild within the city, you gain Sleight of Hand Proficiency.',
      effect: {
        skillProficiencies: [Skill.SleightOfHand]
      }
    },
    {
      name: 'Wasteland Wanderer (Cold)',
      description: 'You have spent endless days surviving desolate tundras. Gain resistance to Cold damage, taking only half damage from it.'
    },
    {
      name: 'Wasteland Wanderer (Fire)',
      description: 'You have spent endless days surviving forbidding deserts. Gain resistance to Fire, taking only half damage from it.'
    },
    {
      name: 'Wasteland Wanderer (Poison)',
      description: 'You have spent endless days surviving fetid swamps. Gain resistance to Poison, taking only half damage from it.'
    }
  ] as Feature[],
  selectionCount: 1,
  selectSameAllowed: false
}

const ANIMAL_ASPECT = {
  name: 'Animal Aspect',
  description: 'Choose an Aspect of the Beast.',
  featureSelection: [
    {
      name: 'Bear',
      description: 'Your carrying capacity is doubled, and you have Advantage on strength checks.'
    } as Feature,
    {
      name: 'Chimpanzee',
      description: 'You gain Resistance to fall damage, and throwing Camp Supplies Blinds targets.'
    } as Feature,
    {
      name: 'Crocodile',
      description: 'Your movement speed increases by 3 m / 10 ft while standing in water-based surfaces. On slippery surfaces, you also have Advantage on Saving Throws against being knocked prone.'
    } as Feature,
    {
      name: 'Eagle',
      description: 'You see in the dark up to 12m / 40 ft, and gain advantage on Perception checks.'
    } as Feature,
    {
      name: 'Elk',
      description: 'Grant yourself and nearby allies (18 m / 60 ft) +1.5 m / +5 ft permanent movement speed.'
    } as Feature,
    {
      name: 'Honey Badger',
      description: 'If you\'re Poisoned, Frightened or Charmed at the start of your turn, you have a 50% chance to begin Raging without expanding a Rage Charge.'
    } as Feature,
    {
      name: 'Stallion',
      description: 'Dashing grants you temporary hit points equal to twice your level.'
    } as Feature,
    {
      name: 'Tiger',
      description: 'You add an addition Strength modifier against Bleeding or Poisoned targets, and gain proficiency in Survival.'
    } as Feature,
    {
      name: 'Wolf',
      description: 'You and nearby allies (18 m / 60 ft) add your Dexterity modifier as a bonus to your Stealth checks.'
    } as Feature,
    {
      name: 'Wolverine',
      description: 'When you attack a Bleeding or Poisoned target, you inflict Maim on them.'
    } as Feature,
  ] as Feature[],
  selectionCount: 1,
  selectSameAllowed: false
} as Feature;

const KI_SPELLS_LV3 = [
  {
    name: 'Blade of Rime',
    description: 'Throw a shard of ice that deals 1d10 Piercing damage. It explodes and deals 2d6 Cold to anyone nearby. It leaves an ice surface. This spell can be cast while you are Silenced.'
  },
  {
    name: 'Chill of the Mountain',
    description: 'Call forth the cold mountain winds and reduce the target\'s movement speed by 3 m / 10 ft.'
  },
  {
    name: 'Fangs of the Fire Snake',
    description: 'Hit foes from afar, dealing Unarmed damage with 1d10 Fire damage at up to 2.5m / 8ft. Your next melee attacks deal an additional 1d4 Fire damage.'
  },
  {
    name: 'Fist of Four Thunders',
    description: 'Release a wave of thunderous force that pushes away all creatures and objects up to 5m / 17ft, and dealing 2d8 Thunder damage. Targets succeeding in a Constitution save take half damage.'
  },
  {
    name: 'Fist of Unbroken Air',
    description: 'Push the target back 6 m / 20 ft, dealing 3d10 Bludgeoning damage and knocking it Prone.'
  },
  {
    name: 'Rush of the Gale Spirits',
    description: 'Summon a strong wind that clears all clouds and pushes creatures back Range Icon.png 5 m / 17ft ft, forcing them Off Balance.'
  },
  {
    name: 'Shaping of the Ice',
    description: 'Create a climbable ice cube.'
  },
  {
    name: 'Sphere of Elemental Balance',
    description: 'Hurl a sphere that deals 3d8 Thunder damage and possibly creates a surface on impact.'
  },
  {
    name: 'Sweeping Cinder Strike',
    description: 'Expel fire from your outstretched hands, deadling 3d6 Fire damage and igniting anything flammable.'
  },
  {
    name: 'Touch of the Storm',
    description: 'Perform a melee attack taht deals 1d10 Lighning damage. The target cannot use reactions. This spell has Advantage on creatures with metal armour.'
  },
  {
    name: 'Water Whip',
    description: 'Deal 3d10 Bludgeoning damage to a target. Possibly pulls a target within 9m toward you or knocks it Prone.'
  }
] as Feature[];

const KI_SPELLS_LV6 = [
  {
    name: 'Clench of the North Wind',
    description: 'Hold a humanoid enemy still. They can\'t Move, Act, or React. Attacks from within 3 m / 10 ft are always Critical Hits. At the end of each turn, the affected creature can make a Wisdom Saving Throw Icons.png Saving Throw to end this condition.'
  },
  {
    name: 'Embrace of the Inferno',
    description: 'Hurl 3 rays of fire. Each ray deals 2d6 Fire damage.'
  },
  {
    name: 'Gong of the Summit',
    description: 'Deals 3d8 Thunder damage to all creatures and objects in a 3m / 10ft radius. Creatures made of inorganic material such as stone have Disadvantage on their Saving Throw. On a successful Constitution Saving Throw, targets take half damage.'
  }
] as Feature[];

const KI_SPELLS_LV11 = [
  {
    name: 'Flames of the Phoenix',
    description: 'Shoot a bright flame from your fingers that explodes upon contact, dealing 8d6 Fire damage to all creatures and objects in a 3m / 10ft radius. On a successful Dexterity Save, targets take half damage.'
  },
  {
    name: 'Mist Stance',
    description: 'Transform yourself into a tiny gas cloud. It can\'t fall, and fits through small openings. The cloud has Advantage on Constitution, Dexterity, and Strength Saving Throws. While transformed, you won\'t be able to attack, cast spells, or talk. Requires Concentration.'
  },
  {
    name: 'Ride the Wind',
    description: 'Gain the ability to Fly until the next Long Rest. Requires Concentration.'
  }
] as Feature[];


const BARD_SUBCLASSES = [
  {
    name: 'College of Lore',
    icon: 'assets/img/classes/150px-College_Of_Lore_Icon.png',
    description: 'You pursue beauty and truth, collecting knowledge from scholarly tomes to peasants\' tales, and use your gifts to hold both audiences and enemies spellbound.',
    features: [
      [ ], [ ], [ // Level 3
        {
          name: 'College of Lore Skill Proficiencies',
          description: 'Choose three additional class skill in which to gain profiency.',
          skillSelection: BARD_SKILLS,
          selectionCount: 3,
          selectSameAllowed: false
        } as Feature
      ], [ ], [ ], [ // Level 6
        {
          name: 'Magical Secrets',
          description: 'Learn 2 non-bard spells from the Magical Secrets spell list.'
          // todo: spell selection
        } as Feature,
      ], [ ], [ ], [ ], [ ], [ ], [ ]
    ] as Feature[][],
  },
  {
    name: 'College of Valour',
    icon: 'assets/img/classes/150px-Class_Bard_Valour_Badge_Icon.png',
    description: 'You wander the land to witness and relate the deeds of the mighty, keeping alive the memory of heroes of the past and inspiring heroes of the future.',
    equipmentProficiencies: [
      EquipmentProficiency.MediumArmor,
      EquipmentProficiency.Shields,
      EquipmentProficiency.MartialWeapons
    ] as EquipmentProficiency[],
    features: [
      [ ], [ ], [ // Level 3
        {
          name: 'Combat Inspiration',
          description: 'Your Bardic Inspiration receives an upgrade, allowing inspired allies to add bonus damage to their next weapon attack or a bonus to their Armour Class for one attack.'
        } as Feature
      ], [ ], [ ], [ // Level 6
        {
          name: 'Extra Attack',
          description: 'You can make an additional free attack after making an unarmed or weapon attack. If you gain the Extra Attack feature from more than one class, they don\'t add together.'
        } as Feature
      ], [ ], [ ], [ ], [ ], [ ], [ ]
    ] as Feature[][],
  },
  {
    name: 'College of Swords',
    icon: 'assets/img/classes/150px-Class_Bard_Swords_Badge_Icon.png',
    description: 'Bards of the College of Swords are called blades, and they entertain through daring feats of weapon prowess.',
    equipmentProficiencies: [
      EquipmentProficiency.MediumArmor,
      EquipmentProficiency.Scimitars
    ] as EquipmentProficiency[],
    features: [
      [ ], [ ], [ // Level 3
        {
          name: 'Fighting Style',
          description: 'Choose a fighting style.',
          featureSelection: [
            {
              name: 'Duelling',
              description: 'When you are wielding a melee weapon that is not Two-Handed or Versatile in one hand, and no weapon in the other, you deal an additional 2 damage with that weapon.'
            } as Feature,
            {
              name: 'Two-Weapon Fighting',
              description: 'When you make an offhand attack, you can add your Ability Score Modifier to the damage of the attack.'
            } as Feature
          ] as Feature[],
          selectionCount: 1,
          selectSameAllowed: false
        } as Feature,
        {
          name: 'Blade Flourish',
          description: 'Whenever you take the attack Action on your turn, your Movement Speed increases by 3 m / 10 ft until the end of the turn,and if a weapon attack that you make as part of this action hits a creature, you can use a Blade Flourish option of your choice. You can use only one Blade Flourish option per turn.'
        } as Feature
      ], [ ], [ ], [ // Level 6
        {
          name: 'Extra Attack',
          description: 'You can make an additional free attack after making an unarmed or weapon attack. If you gain the Extra Attack feature from more than one class, they don\'t add together.'
        } as Feature
      ], [ ], [ ], [ ], [ ], [ ], [ ]
    ] as Feature[][],
  }
] as Subclass[];

const BARBARIAN_SUBCLASSES = [
  {
    name: 'Berserker',
    icon: 'assets/img/classes/150px-Berserker_Subclass_300px.png',
    description: 'Violence is both a means and an end. You follow a path of untrammeled fury, slick with blood, as you thrill in the chaos of battle, heedless of your own well-being.',
    features: [
      [], [],
      [ // level 3
        {
          name: 'Frenzy',
          description: 'Your Rage turns into a frenzy as you release your unquenchable fury and bloodlust. You gain the use of Frenzied Strike and Enraged Throw. You can also use an Improvised Melee Weapon as a bonus action. Frenzy ends early if you haven\'t attacked a creature or taken damage since your last turn.'
        } as Feature,
        {
          name: 'Frenzied Strike',
          description: 'As a bonus action, make a melee attack with your equipped weapon. Available only when Frenzied.'
        } as Feature,
        {
          name: 'Enraged Throw',
          description: 'As a bonus action, pick up an item or creature and throw it at a target, dealing additional damage and knocking it Prone. Your Strength affects the additional damage and how much weight you can throw. Heavier items deal more damage. The damage of weapons with the thrown property is the same as the weapon\'s melee damage. Available only when Frenzied.'
        } as Feature
      ], [], [],
      [ // level 6
        {
          name: 'Mindless Rage',
          description: 'Your rage becomes all-consuming, repelling outside influence. While Frenzied, you can\'t be Charmed or Frightened, and Calm Emotions Icon.png Calm Emotions no longer ends your rage.'
        } as Feature
      ], [], [], [],
      [ // level 10
        {
          name: 'Intimidating Presence',
          description: 'As an action, you can menace an enemy and instill a terrible Fear within them. You can use Maintain Intimidating Presence to prolong the targets fear.'
        } as Feature
      ], [], []
    ]
  },
  {
    name: 'Wildheart',
    icon: 'assets/img/classes/150px-Wildheart_Subclass_300px.png',
    description: 'Your attunement with nature and its beasts inspires your rage, empowering you with supernatural might.',
    features: [
      [], [],
      [ // level 3
        {
          name: 'Speak with Animals',
          description: 'Once per Long Rest, you can use the 1st Level Spell Speak with Animals without expending spell slots.',
          effect:
            {
              spells: [SPELL_SPEAK_WITH_ANIMALS] as Spell[]
            } as Effect
        } as Feature,
        {
          name: 'Bestial Heart',
          description: 'Choose a Beastial Heart.',
          featureSelection: [
            {
              name: 'Bear Heart',
              description: 'While Raging, you can use Unrelenting Ferocity to heal 1d8 + Constitution modifier hit points. You also have Resistance to all damage except psychic damage.'
            } as Feature,
            {
              name: 'Eagle Heart',
              description: 'While Raging, you can use Diving Strike, leaping down unto a foe below you, dealing Weapon Damage and knocking them Prone. Foes also have Disadvantage on Opportunity Attacks against you, and you can use Dash as a bonus action.'
            } as Feature,
            {
              name: 'Elk Heart',
              description: 'While Raging, you can use Primal Stampede, charging forward and dealing 1d4+2 Bludgeon damage to all hostile creatures in your way and knocking them prone. Your Movement Speed also increases by 4.5m / 15ft.'
            } as Feature,
            {
              name: 'Tiger Heart',
              description: 'While Raging, you can use Tiger\'s Bloodlust, lashing out to attack up to 3 enemies at once. They each take half the damage your weapon usually deals and start to Bleed. Your jump distance also increases by 4.5m / 15ft.'
            } as Feature,
            {
              name: 'Wolf Heart',
              description: 'While Raging, you can use Inciting Howl, allowing each ally within earshot to move an additional 3m / 10ft during their next turn. Your allies also have Advantage on melee Attack Rolls against enemies within 2m / 7ft of you.'
            } as Feature
          ] as Feature[],
          selectionCount: 1,
          selectSameAllowed: false
        } as Feature
      ], [], [],
      [ // level 6
        ANIMAL_ASPECT
      ], [],
      [ // level 8
        {
          name: 'Land\'s Stride: Difficult Terrain',
          description: 'You have become an expert at moving through the wilderness. Difficult Terrain no longer slows you down.'
        } as Feature
      ], [],
      [ // level 10
        ANIMAL_ASPECT
      ], [], []
    ]
  },
  {
    name: 'Wild Magic Barbarian',
    icon: 'assets/img/classes/150px-Wild_Magic_Barbarian_Subclass_Icon.png',
    description: 'The wild influence of magic has transformed you, suffusing you with arcane power that churns within you, waiting to be released.',
    features: [
      [], [],
      [ // level 3
        {
          name: 'Rage: Wild Magic',
          description: 'Rage instantly causes a magical effect randomly rolled from the Wild Magic table. Some of these effects can be activated every subsequent turn as a bonus action.'
        } as Feature,
        {
          name: 'Magic Awareness',
          description: 'Anyone within a 3m range adds their Proficiency Bonus to Saving Throws against spells.'
        } as Feature
      ], [], [],
      [ // level 6
        {
          name: 'Bolstering Magic: Boon',
          description: 'As an action, you or an ally receive +1d4 bonus to Attack Rolls and Ability Checks for 10 turns.'
        } as Feature,
        {
          name: 'Bolstering Magic: Level 1 Spell Slot',
          description: 'As an action, you or an ally recover a level 1 spell slot.'
        } as Feature,
        {
          name: 'Bolstering Magic: Level 2 Spell Slot',
          description: 'As an action, you or an ally recover a level 2 spell slot.'
        } as Feature
      ], [], [],
      [ // level 9
        {
          name: 'Bolstering Magic: Level 3 Spell Slot',
          description: 'As an action, you or an ally recover a level 3 spell slot.'
        } as Feature
      ],
      [ // level 10
        {
          name: 'Unstable Backlash',
          description: 'While enraged, when you take damage or fail a Saving Throw, you trigger another Wild Magic effect that replaces the current one, as a reaction.'
        } as Feature
      ], [], []
    ]
  }
] as Subclass[];

// todo: Add cleric domain spells
const CLERIC_SUBCLASSES = [
  {
    name: 'Knowledge Domain',
    icon: 'assets/img/classes/150px-Knowledge_Domain_Icon.png',
    description: 'Adaptable and adroit in all manner of languages and skills, your mind is an intellectual cup brimming with exquisite knowing.',
    features: [
      [ // level 1
        {
          name: 'Blessings of Knowledge',
          description: 'You gain Proficiency in your choice of two of the following skills: Arcana, History, Nature, or Religion. Your Proficiency Bonus Proficiency Bonus is doubled for any ability check you make that uses either of these skills.',
          skillSelection: [Skill.Arcana, Skill.History, Skill.Nature, Skill.Religion],
          selectionCount: 2,
          selectSameAllowed: false
        },
        {
          name: 'Domain Spells',
          description: 'You gain the Command and Sleep spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // level 2
        {
          name: 'Knowledge of the Ages',
          description: 'Use a Channel Divinity Charge to gain Proficiency in all Skills of a chosen Ability Score until the next Long Rest.'
        }
      ],
      [ // Level 3
        {
          name: 'Domain Spells',
          description: 'You gain the Calm Emotions and Hold Person spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Domain Spells',
          description: 'You gain the Speak with Dead and Slow spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 6

      ],
      [ // Level 7
        {
          name: 'Domain Spells',
          description: 'You gain the Confusion and Otiluke\'s Resilient Sphere spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 8
        {
          name: 'Potent Spellcasting',
          description: 'Your god grants you even more intense power. You can add your Wisdom Modifier to the damage you deal with Cleric Cantrips.'
        }
      ],
      [ // Level 9
        {
          name: 'Domain Spells',
          description: 'You gain the Dominate Person and Telekinesis spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 10

      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Life Domain',
    icon: 'assets/img/classes/150px-Class_Cleric_Life_Badge_Icon.png',
    description: 'The Life domain is an aspect of many good deities, offering spells that protect and restore the mind, body, and soul.',
    equipmentProficiencies: [
      EquipmentProficiency.HeavyArmor
    ] as EquipmentProficiency[],
    features: [
      [ // level 1
        {
          name: 'Disciple of Life',
          description: 'Your devotion empowers your healing Spells. When casting a healing spell, the target regains additional Hit Points equal to 2 + Spell\'s level.'
        },
        {
          name: 'Domain Spells',
          description: 'You gain the Bless and Cure Wounds spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // level 2
        {
          name: 'Preserve Life',
          description: 'Use a Channel Divinity Charge to evoke a healing energy that restores (3 x Cleric\'s Level) + 2 Hit Points to allied creatures. Has no effect on undead and constructs.'
        }
      ],
      [ // Level 3
        {
          name: 'Domain Spells',
          description: 'You gain the Aid and Lesser Restoration spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Domain Spells',
          description: 'You gain the Revivify and Beacon of Hope spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 6
        {
          name: 'Blessed Healer',
          description: 'Regain 2 + Spell Slot Level Hit Points when casting a healing spell on another creature.'
        }
      ],
      [ // Level 7
        {
          name: 'Domain Spells',
          description: 'You gain the Guardian of Faith and Death Ward spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 8
        {
          name: 'Divine Strike - Life',
          description: 'Once per turn, deal 1d8 Radiant damage in addition to your weapon\'s weapon damage when you make a melee attack.'
        }
      ],
      [ // Level 9
        {
          name: 'Domain Spells',
          description: 'You gain the Greater Restoration and Mass Cure Wounds spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 10

      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Light Domain',
    icon: 'assets/img/classes/150px-Light_Domain_Subclass_300px.png',
    description: 'The Light domain is offered by deities of justice, majesty, and primordial flame, providing spells that dispel darkness and harm the undead.',
    features: [
      [ // level 1
        {
          name: 'Warding Flare',
          description: 'Shield yourself with divine light. Use your Reaction to impose Disadvantage on an attacker, potentially causing their attack to miss. An attacker that can’t be Blinded is immune to this effect.'
        },
        {
          name: 'Domain Spells',
          description: 'You gain the Light Cantrip and Burning Hands and Faerie Fire spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // level 2
        {
          name: 'Radiance of the Dawn',
          description: 'Use a Channel Divinity Charge to harness the sun\'s divine power to dispel any magical darkness and deal 2d10 + Character Level Radiant damage to hostile creatures. On a successful save, targets still take half damage. Doesn\'t affect creatures that have total cover from you.'
        }
      ],
      [ // Level 3
        {
          name: 'Domain Spells',
          description: 'You gain the Flaming Sphere and Scorching Ray spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Domain Spells',
          description: 'You gain the Daylight and Fireball spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 6
        {
          name: 'Improved Warding Flare',
          description: 'When an enemy attacks an ally, you can use your Reaction to impose Disadvantage on the Attack Roll, possibly causing their attack to miss.'
        }
      ],
      [ // Level 7
        {
          name: 'Domain Spells',
          description: 'You gain the Guardian of Faith and Wall of Fire spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 8
        {
          name: 'Potent Spellcasting',
          description: 'Your god grants you even more intense power. You can add your Wisdom Modifier to the damage you deal with Cleric Cantrips.'
        }
      ],
      [ // Level 9
        {
          name: 'Domain Spells',
          description: 'You gain the Destructive Wave and Flame Strike spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 10

      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Nature Domain',
    icon: 'assets/img/classes/150px-Nature_Domain_Icon.png',
    description: 'You embody the vast viridian power of the natural world, an avatar of the subtle divinity of fruitfall, avian migration, woodland silence, and the landslide\'s roaring fury.',
    equipmentProficiencies: [
      EquipmentProficiency.HeavyArmor
    ] as EquipmentProficiency[],
    features: [
      [ // level 1
        {
          name: 'Acolyte of Nature',
          description: 'You learn a Druid cantrip, and become Proficient in Animal Handling, Nature, or Survival.'
          // todo: add druid cantrip selection
        },
        {
          name: 'Domain Spells',
          description: 'You gain the Speak with Animals and Animal Friendship spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // level 2
        {
          name: 'Charm Animals and Plants',
          description: 'Use a Channel Divinity Charge to channel fey magic to Charm nearby beasts and plants.'
        }
      ],
      [ // Level 3
        {
          name: 'Domain Spells',
          description: 'You gain the Spike Growth and Barkskin spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Domain Spells',
          description: 'You gain the Sleet Storm and Plant Growth spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 6

      ],
      [ // Level 7
        {
          name: 'Domain Spells',
          description: 'You gain the Dominate Beast and Grasping Vine spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 8
        {
          name: 'Divine Strike - Elemental Fury',
          description: 'Once per turn, deal 1d8 Cold, Fire, or Lightning damage in addition to your weapon\'s weapon damage when you make a melee attack.'
        }
      ],
      [ // Level 9
        {
          name: 'Domain Spells',
          description: 'You gain the Insect Plague and Wall of Stone spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 10

      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Tempest Domain',
    icon: 'assets/img/classes/150px-Tempest_Domain_Icon.png',
    description: 'Your faith has made you the very thunder that quakes the black firmament, the lightning coursing through the veins of a terrible storm.',
    equipmentProficiencies: [
      EquipmentProficiency.HeavyArmor,
      EquipmentProficiency.MartialWeapons
    ] as EquipmentProficiency[],
    features: [
      [ // level 1
        {
          name: 'Wrath of the Storm',
          description: 'Using a Reaction, strike back at an attacking creature, potentially dealing 2d8 Lightning damage if they fail a Dexterity Saving Throw or otherwise take half of 2d8 Thunder damage.'
        },
        {
          name: 'Domain Spells',
          description: 'You gain the Thunderwave and Fog Cloud spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // level 2
        {
          name: 'Destructive Wrath',
          description: 'When you roll Thunder or Lightning damage, you can use a Channel Divinity Charge to deal maximum damage instead.'
        }
      ],
      [ // Level 3
        {
          name: 'Domain Spells',
          description: 'You gain the Shatter and Gust of Wind spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Domain Spells',
          description: 'You gain the Call Lightning and Sleet Storm spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 6
        {
          name: 'Thunderbolt Strike',
          description: 'When you deal Thunder or Lightning damage to a creature that is Large or smaller, you can also push it up to 3 m / 10 ft.'
        }
      ],
      [ // Level 7
        {
          name: 'Domain Spells',
          description: 'You gain the Freedom of Movement and Ice Storm spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 8
        {
          name: 'Divine Strike - Tempest',
          description: 'Once per turn, deal 1d8 Lightning damage in addition to your weapon\'s weapon damage when you make a melee attack.'
        }
      ],
      [ // Level 9
        {
          name: 'Domain Spells',
          description: 'You gain the Destructive Wave and Insect Plague spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 10

      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Trickery Domain',
    icon: 'assets/img/classes/150px-Class_Cleric_Trickery_Badge_Icon.png',
    description: 'A domain shared by wicked, chaotic, and mischievous deities alike, those who channel Trickery specialise in deception and illusion magic.',
    features: [
      [ // level 1
        {
          name: 'Blessing of the Trickster',
          description: 'Grant another creature Advantage on Stealth Checks.'
        },
        {
          name: 'Domain Spells',
          description: 'You gain the Charm Person and Disguise Self spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // level 2
        {
          name: 'Invoke Duplicity',
          description: 'Use a Channel Divinity Charge to summon an illusion to distract your enemies. You and your allies receive Advantage on Attack Rolls against a creature if both the attacker and the illusion are within 3m / 10ft of that creature.'
        }
      ],
      [ // Level 3
        {
          name: 'Domain Spells',
          description: 'You gain the Mirror Image and Pass Without Trace spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Domain Spells',
          description: 'You gain the Bestow Curse and Fear spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 6

      ],
      [ // Level 7
        {
          name: 'Domain Spells',
          description: 'You gain the Dimension Door and Polymorph spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 8
        {
          name: 'Divine Strike - Nightshade Poison',
          description: 'Once per turn, deal 1d8 Poison damage in addition to your weapon\'s weapon damage when you make a melee attack.'
        }
      ],
      [ // Level 9
        {
          name: 'Domain Spells',
          description: 'You gain the Dominate Person and Seeming spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 10

      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'War Domain',
    icon: 'assets/img/classes/150px-War_Domain_Icon.png',
    description: 'Fortified by holy zeal, you brandish an arsenal of sacramental savagery to use against those you deem unrighteous.',
    equipmentProficiencies: [
      EquipmentProficiency.HeavyArmor,
      EquipmentProficiency.MartialWeapons
    ] as EquipmentProficiency[],
    features: [
      [ // level 1
        {
          name: 'War Priest',
          description: 'When you make an unarmed or weapon attack, you can spend a War Priest Charge to make an additional attack as a Bonus Action.'
        },
        {
          name: 'Domain Spells',
          description: 'You gain the Divine Favour and Shield of Faith spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // level 2
        {
          name: 'Guided Strike',
          description: 'Use a Channel Divinity Charge to gain a +10 bonus to your Attack Roll.'
        }
      ],
      [ // Level 3
        {
          name: 'Domain Spells',
          description: 'You gain the Magic Weapon and Spiritual Weapon spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Domain Spells',
          description: 'You gain the Spirit Guardians and Crusader\'s Mantle spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 6
        {
          name: 'War God\'s Blessing',
          description: 'Use a Channel Divinity Charge to endow a nearby ally with the glory of your god to grant them a +10 bonus to their Attack Roll.'
        }
      ],
      [ // Level 7
        {
          name: 'Domain Spells',
          description: 'You gain the Stoneskin and Freedom of Movement spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 8
        {
          name: 'Divine Strike - Warmaster',
          description: 'Once per turn, deal 1d8 damage in addition to your weapon\'s weapon damage when you make a melee attack.'
        }
      ],
      [ // Level 9
        {
          name: 'Domain Spells',
          description: 'You gain the Flame Strike and Hold Monster spells from your domain. They are always prepared'
          // todo: add spells
        }
      ],
      [ // Level 10

      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  }
] as Subclass[];

const DRUID_SUBCLASSES = [
  {
    name: 'Circle of the Land',
    icon: 'assets/img/classes/Circle_Of_The_Land_Icon.png',
    description: 'Druids of this Circle connect to powerful magic that flows through the earth and binds all living things together.',
    features: [
      [ // level 1

      ],
      [ // level 2
        {
          name: 'Natural Recovery',
          description: 'Once per day while out of combat, you can replenish expended Spell Slots. The recovered spell slots must have a combined level that is less than or equal to half your Druid level (rounded up).'
        }
      ],
      [ // Level 3

      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6
        {
          name: 'Land\'s Stride',
          description: 'Difficult Terrain no longer slows you down.'
        }
      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10
        {
          name: 'Nature\'s Ward',
          description: 'You can\'t be charmed or frightened by elementals and fey. Disease and poison no longer affect you.'
        }
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Circle of the Moon',
    icon: 'assets/img/classes/Circle_Of_The_Moon_Icon.png',
    description: 'Druids sworn to the moon draw on its mercurial nature to transform into massive creatures and primal elementals.',
    features: [
      [ // level 1

      ],
      [ // level 2
        {
          name: 'Lunar Mend',
          description: 'As a bonus action, expend spell slots to regain Hit Points while in Wild Shape. You regain 1d8 hit points per Spell Slot level.'
        },
        {
          name: 'Combat Wild Shape',
          description: 'Wild Shape now uses a Bonus Action instead of an Action. You can now transform into a Bear.'
        }
      ],
      [ // Level 3

      ],
      [ // Level 4
        {
          name: 'Improved Combat Wild Shape',
          description: 'You can now transform into a Dire Raven.'
        }
      ],
      [ // Level 5
        {
          name: 'Wild Strike',
          description: 'You can make an additional attack after making an unarmed strike while in wildshape.'
        }
      ],
      [ // Level 6
        {
          name: 'Primal Strike',
          description: 'While in beast form, your attacks count as magical for the purpose of overcoming resistance and immunity to non-magical damage.'
        }
      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10
        {
          name: 'Elemental Combat Wild Shape',
          description: 'You can now transform into a Air Myrmidon, Earth Myrmidon, Fire Myrmidon, and Water Myrmidon.'
        },
        {
          name: 'Improved Wild Strike',
          description: 'You can make 2 additional attacks after making an unarmed strike while in wildshape.'
        }
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Circle of the Spores',
    icon: 'assets/img/classes/150px-Circle_of_the_Spores_Icon.png',
    description: 'Viewing death, necrosis, fungal growth, and sportulation as just another part of life, you can manipulate such spores to augment yourself and harm your foes.',
    features: [
      [ // level 1

      ],
      [ // level 2
        {
          name: 'Halo of Spores',
          description: 'As a reaction, unleash a cloud of necrotic spores upon a target that deals 1d4 Necrotic damage. A Constitution Save negates the damage.'
        },
        {
          name: 'Symbiotic Entity',
          description: 'Use a Wild Shape Charge to gain 4 Temporary Hit Points per Druid Druid level and deal an additional 1d6 Necrotic damage while you have them. you can cast Halo of Spores with double damage.'
        }
      ],
      [ // Level 3

      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6
        {
          name: 'Fungal Infestation',
          description: 'Target a Beast or Humanoid corpse and raise a Zombie until next Long Rest. Counts as a Reaction.'
        }
      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10
        {
          name: 'Spreading Spores',
          description: 'Requires Symbiotic Entity to be active. As a Bonus Action, create a cloud within 9m that deals 2d8 Necrotic damage per turn to all enemies. Lasts 10 turns or until Symbiotic Entity deactivates. A Constitution Save negates the damage.'
        }
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  }
] as Subclass[];



const FIGHTER_SUBCLASSES = [
  {
    name: 'Battle Master',
    icon: 'assets/img/classes/Battle_Master_Icon.png',
    description: 'Battle Masters are paragons of tactical superiority, combining combat maneuvers and experience in the field to dominate every fight.',
    features: [
      [ // level 1

      ],
      [ // level 2

      ],
      [ // Level 3
        {
          name: 'Superiority Dice',
          description: 'Your Superiority Dice are d8s, and you have 4. They fuel your battle master manoeuvers and are expended upon use. You gain an additional dice at level 7. You regain expended Superiority Dice after a Short or Long Rest.'
        },
        {
          name: 'Battle Manoeuvers',
          description: 'Choose 3 Battle Manoeuvers. They are powerful attacks that use superiority dice.',
          featureSelection: BATTLE_MANOEUVERS,
          selectionCount: 3,
          selectSameAllowed: false
        }
      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6

      ],
      [ // Level 7
        {
          name: 'Battle Manoeuvers',
          description: 'Choose 2 additional Battle Manoeuvers.',
          featureSelection: BATTLE_MANOEUVERS,
          selectionCount: 2,
          selectSameAllowed: false
        }
      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10
        {
          name: 'Battle Manoeuvers',
          description: 'Choose 2 additional Battle Manoeuvers.',
          featureSelection: BATTLE_MANOEUVERS,
          selectionCount: 2,
          selectSameAllowed: false
        },
        {
          name: 'Improved Superiority Dice',
          description: 'The size of your Superiority Dice increases to 1d10.'
        }
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Champion',
    icon: 'assets/img/classes/150px-Champion_Pic.png',
    description: 'You approach the complex problems posed by combat with one distinctly effective solution - you hit those problems, really quite hard.',
    features: [
      [ // level 1

      ],
      [ // level 2

      ],
      [ // Level 3
        {
          name: 'Ímproved Critical Hit',
          description: 'The number you need to roll a Critical Hit while attacking is reduced by 1. This effect can stack.'
        }
      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6

      ],
      [ // Level 7
        {
          name: 'Remarkable Athlete (Jump)',
          description: 'You\'re a master of your own body, your athletic prowess extended beyond the usual. Your Jump distance is increased by 3m / 10ft.'
        },
        {
          name: 'Remarkable Athlete (Proficiency)',
          description: 'You\'re a master of your own body, your athletic prowess extended beyond the usual. You can add half of your Proficiency Bonus to any Strength, Dexterity, and Constitution Checks that you are not Proficient in.'
        }
      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10
        {
          name: 'Fighting Style',
          description: 'Choose 1 fighting style.',
          featureSelection: [
            {
              name: 'Archery',
              description: 'You gain a +2 bonus to Attack Rolls you make with ranged Weapons.'
            },
            {
              name: 'Defense',
              description: 'You gain a +1 bonus to Armour Class while wearing Armour.'
            },
            {
              name: 'Duelling',
              description: 'When you are wielding a melee weapon that is not Two-Handed or Versatile in one hand, and no weapon in the other, you deal an additional 2 damage with that weapon.'
            },
            {
              name: 'Great Weapon Fighting',
              description: 'When you roll a 1 or 2 on a damage die for an attack with a Two-Handed melee weapon, that die is rerolled once.'
            },
            {
              name: 'Protection',
              description: 'When you have a Shield, impose Disadvantage on an enemy who attacks one of your allies when you are within 1.5 m / 5 ft. You must be able to see the enemy. This is a reaction.'
            },
            {
              name: 'Two Weapon Fighting',
              description: 'When you make an offhand attack, you can add your Ability Score Modifier to the damage of the attack.'
            }
          ] as Feature[],
          selectionCount: 1,
          selectSameAllowed: false
        } as Feature
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Eldritch Knight',
    icon: 'assets/img/classes/Eldritch_Knight_Icon.png',
    description: 'Eldritch Knights study magic to supplement their weaponry, allowing them to overcome resistance from the toughest foes.',
    features: [
      [ // level 1

      ],
      [ // level 2

      ],
      [ // Level 3
        {
          name: 'Weapon Bond',
          description: 'Ritually bind the weapon in your main hand. The weapon can\'t be knocked out of your hand, and it automatically returns to you when thrown.',
        }
      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6

      ],
      [ // Level 7
        {
          name: 'War Magic',
          description: 'You have honed your body and magic for war. After you cast a Cantrip, you can make a weapon attack using a bonus action.'
        }
      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10
        {
          name: 'Eldritch Strike',
          description: 'When you hit a creature with a weapon attack, it has Disadvantage on its next Saving Throw against a spell you cast before the end of your next turn.'
        }
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  }
] as Subclass[];

const MONK_SUBCLASSES = [
  {
    name: 'Way of the Open Hand',
    icon: 'assets/img/classes/150px-Open_Hand.png',
    description: 'You specialise in unarmed combat, using your hands and control of ki to heal or inflict grievous hurt.',
    features: [
      [ // level 1

      ],
      [ // level 2

      ],
      [ // Level 3
        {
          name: 'Flurry of Blows (Topple)',
          description: 'Punch twice in quick succession and possibly knock the target Prone.'
        },
        {
          name: 'Flurry of Blows (Stagger)',
          description: 'Punch twice in quick succession and Stagger the target, making it unable to take reactions.'
        },
        {
          name: 'Flurry of Blows (Push)',
          description: 'Punch twice in quick succession and possibly push the target 5m / 17ft away.'
        }
      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6
        {
          name: 'Manifestation of Body',
          description: 'Passive (Toggle). Your hands sap the ki from your enemies\' bodies. Your unarmed attacks deal an additional 1d4 + Wisdom Modifier Necrotic damage. Can\'t be used at the same time as Manifestation of Mind or Manifestation of Soul.'
        },
        {
          name: 'Manifestation of Mind',
          description: 'Passive (Toggle). Your strikes interrupt the ki flow to your enemies\' minds. Your unarmed attacks deal an additional 1d4 + Wisdom Modifier Psychic damage. Can\'t be used at the same time as Manifestation of Body or Manifestation of Soul.'
        },
        {
          name: 'Manifestation of Soul',
          description: 'Passive (Toggle). Infuse your strikes with ki from outside your body. Your unarmed attacks deal an additional 1d4 + Wisdom Modifier Radiant damage. Can\'t be used at the same time as Manifestation of Body or Manifestation of Mind.'
        },
        {
          name: 'Wholeness of Body',
          description: 'Regain half your Monk Ki points and enter a temporary state of Wholeness. Restores 3 x Monk Level Hit Points, gain an extra Bonus Action, and recover 1 Ki Point every turn for 3 turns. Can be used once per Long Rest.'
        }
      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9
        {
          name: 'Ki Resonation (Punch)',
          description: 'Hit a creature with your bare fists, making the ki in its body Resonate with yours. You can then make the ki explode with Ki Resonation: Blast.'
        },
        {
          name: 'Ki Resonation (Blast)',
          description: 'Detonate the ki of a creature Resonating with you, hitting the creature and anyone within 5m / 17ft of it. The resulting explosion causes any nearby creatures Resonating with you to also detonate. On Dexterity Save targets still take half damage.'
        }
      ],
      [ // Level 10

      ],
      [ // Level 11
        {
          name: 'Tranquility',
          description: 'Long Rests surround you with an aura of peace, granting you Sanctuary.'
        }
      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Way of Shadow',
    icon: 'assets/img/classes/150px-Shadow_Icon.png',
    description: 'You value the arts of stealth and subterfuge, bending the shadows to your will to strike without warning.',
    features: [
      [ // level 1

      ],
      [ // level 2

      ],
      [ // Level 3
        {
          name: 'Shadow Arts (Hide)',
          description: 'Hide from enemies by succeeding Stealth Checks. Stick to the dark and avoid enemy sightlines. Attacking or casting a spell will reveal your location. Can be used as a Bonus Action.'
        },
        {
          name: 'Shadow Arts (Pass Without Trace)',
          description: 'Call forth a veil of shadows and silence that gives you and all nearby companions a +10 bonus to Stealth Checks. Can be used as an Action, and uses 2 Ki points.'
        },
        {
          name: 'Shadow Arts (Darkness)',
          description: 'Create a dark shroud that heavily Obscures and Blinds creatures within an 18m / 60ft radius. Creatures cannot make ranged attacks into or out of it. Lasts for 10 turns and requires Concentration. Can be used as an Action, and uses 2 Ki points.'
        },
        {
          name: 'Shadow Arts (Darkvision)',
          description: 'Grant a creature the ability to see into the dark out to a range of 12 m / 40 ft. Lasts until the next Long Rest. Can be used as an Action, and uses 2 Ki points.'
        },
        {
          name: 'Shadow Arts (Silence)',
          description: 'Create a sound-proof sphere of an 18m / 60ft radius for 100 turns. Requires concentration. All within are Silenced and Immune toDamage Types Thunder damage. Can be used as an Action, and uses 2 Ki points.'
        },
        {
          name: 'Cantrip (Minor Illusion)',
          description: 'Create an illusion that compels nearby creatures to investigate. You can remain hidden while casting this spell. This spell can be cast while you are Silenced.'
          // todo: add Minor Illusion spell
        }
      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Cloak of Shadows',
          description: 'Wrap yourself in shadows to become Invisible if you are obscured. Lasts for 10 turns. Invisibility ends early if you attack, cast another spell, take an action, or take damage.'
        }
      ],
      [ // Level 6
        {
          name: 'Shadow Step',
          description: 'Teleport from shadow to shadow, as a Bonus Action. Afterwards, you have Advantage on your next melee Attack Roll.'
        }
      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10

      ],
      [ // Level 11
        {
          name: 'Shadow Strike',
          description: 'Teleport to a foe from a hidden position, while invisible or sneaking. You strike them with the creeping, psychic ferocity of the shadows themselves, dealing an additional 3d8 Psychic damage. Can be used as an Action, and uses 3 Ki points.'
        }
      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Way of the Four Elements',
    icon: 'assets/img/classes/150px-Four_Elements_Icon.png',
    description: 'You focus your ki to bend the elements to your will, using them as an extension of your own body.',
    features: [
      [ // level 1

      ],
      [ // level 2

      ],
      [ // Level 3
        {
          name: 'Ki Spellcasting',
          description: 'Learn 3 spells that can be cast using Ki points. These spells also trigger an Unarmed Strike.',
          featureSelection: KI_SPELLS_LV3,
          selectionCount: 3,
          selectSameAllowed: false
        },
        {
          name: 'Harmony of Fire and Water',
          description: 'While not in combat, regain half your Ki points. Can be used once per Long Rest.'
        }
      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6
        {
          name: 'Ki Spellcasting',
          description: 'Learn 1 additional spell.',
          featureSelection: KI_SPELLS_LV3.concat(KI_SPELLS_LV6),
          selectionCount: 1,
          selectSameAllowed: false
        }
      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9
        {
          name: 'Improved Elemental Casting',
          description: 'Several of your Four Elemental Spells deal an additional dice of damage. Clench of the North Wind can hold an additional creature. Embrace of the Inferno fires an additional ray.'
        },
        {
          name: 'Ki Spellcasting',
          description: 'Learn 1 additional spell.',
          featureSelection: KI_SPELLS_LV3.concat(KI_SPELLS_LV6),
          selectionCount: 1,
          selectSameAllowed: false
        }
      ],
      [ // Level 10

      ],
      [ // Level 11
        {
          name: 'Ki Spellcasting',
          description: 'Learn 1 additional spell.',
          featureSelection: KI_SPELLS_LV3.concat(KI_SPELLS_LV6).concat(KI_SPELLS_LV11),
          selectionCount: 1,
          selectSameAllowed: false
        }
      ],
      [ // Level 12

      ]
    ] as Feature[][],
  }
] as Subclass[];

const PALADIN_SUBCLASSES = [
  {
    name: 'Oath of the Ancients',
    icon: 'assets/img/classes/150px-Paladin_Oath_of_the_Ancients_Icon.png',
    description: 'You fight on the side of light in the cosmic struggle against darkness to preserve the sanctity of life and the beauty of nature.',
    features: [
      [ // level 1
        {
          name: 'Healing Radiance',
          description: 'Call upon your Oath to let nature heal all nearby allies. Allies immediately gain Proficiency Bonus + Paladin Level + Charisma Modifier hit points, and will regain the same amount at the start of your next turn. Can be used as a Bonus Action, and uses 1 Channel Oath Charge.'
        }
      ],
      [ // level 2

      ],
      [ // Level 3
        {
          name: 'Nature\'s Wrath',
          description: 'Invoke primaeval forces to restrain an enemy. For 10 turns, the target cannot move, Attack Rools against it have Advantage, and it\'s Attack Rools and Dexterity Saving Throws have Disadvantage. Strength Saving Throw negates the effects. Can be used as an Action, and uses 1 Channel Oath Charge.'
        },
        {
          name: 'Turn the Faithless',
          description: 'Channel your Oath using ancient words to Turn nearby fey and fiends. Wisdom Saving Throw negates the effect. Can be used as an Action, and uses 1 Channel Oath Charge.'
        },
        {
          name: 'Oath Spells',
          description: 'You gain the Speak with Animals and Ensnaring Strike spells from your oath. They are Always Prepared.'
          // todo: add spells
        }
      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Oath Spells',
          description: 'You gain the Misty Step and  Moonbeam spells from your oath. They are Always Prepared.'
          // todo: add spells
        }
      ],
      [ // Level 6

      ],
      [ // Level 7
        {
          name: 'Aura of Warding',
          description: 'You and any nearby allies take only half the damage from spells. The aura disappears if you fall Unconscious.'
        }
      ],
      [ // Level 8

      ],
      [ // Level 9
        {
          name: 'Oath Spells',
          description: 'You gain the Protection from Energy and Plant Growth spells from your oath. They are Always Prepared.'
          // todo: add spells
        }
      ],
      [ // Level 10

      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Oath of Devotion',
    icon: 'assets/img/classes/150px-Paladin_Oath_of_Devotion_Icon.png',
    description: 'Following the ideal of the knight in shining armour, you act with honour and virtue to protect the weak and pursue the greater good.',
    features: [
      [ // level 1
        {
          name: 'Holy Rebuke',
          description: 'Call upon your Oath to grant a vengeful aura that lasts for 2 turns and deals 1d4 Radiant damage to anyone who hits them with a melee attack. Can be used as an Action, and uses 1 Channel Oath Charge.'
        }
      ],
      [ // level 2

      ],
      [ // Level 3
        {
          name: 'Sacred Weapon',
          description: 'Imbue the weapon in your main hand with positive energy to turn it into a Sacred Weapon for 10 turns. Can be used as an Action, and uses 1 Channel Oath Charge.'
        },
        {
          name: 'Turn the Unholy',
          description: 'Channel your Oath and speak a prayer to Turn nearby fiends and undead. Wisdom Saving Throw negates the effect. Can be used as an Action, and uses 1 Channel Oath Charge.'
        },
        {
          name: 'Oath Spells',
          description: 'You gain the Protection from Evil and Good, and Sanctuary spells from your oath. They are Always Prepared.'
          // todo: add spells
        }
      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Oath Spells',
          description: 'You gain the Lesser Restoration and Silence spells from your oath. They are Always Prepared.'
          // todo: add spells
        }
      ],
      [ // Level 6

      ],
      [ // Level 7
        {
          name: 'Aura of Devotion',
          description: 'You and any nearby allies can\'t be Charmed. The aura disappears if you fall Unconscious.'
        }
      ],
      [ // Level 8

      ],
      [ // Level 9
        {
          name: 'Oath Spells',
          description: 'You gain the Remove Curse and Beacon of Hope spells from your oath. They are Always Prepared.'
          // todo: add spells
        }
      ],
      [ // Level 10

      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Oath of Vengeance',
    icon: 'assets/img/classes/150px-Paladin_Oath_of_Vengeance.png',
    description: 'You have set aside even your own purity to right wrongs and deliver justice to those who have committed the most grievous sins.',
    features: [
      [ // level 1
        {
          name: 'Inquisitor\'s Might',
          description: 'You or an ally\'s weapon attacks deal an additional 3 Radiant damage and can Daze enemies for 1 turn. Can be used as a Bonus Action, and uses 1 Channel Oath Charge.'
        }
      ],
      [ // level 2

      ],
      [ // Level 3
        {
          name: 'Abjure Enemy',
          description: 'Frighten an enemy. They\'ll be easier to hit and cannot move. Fiends and undead have Disadvantage on this Saving Throw. Wisdom Saving Throw negates the effect. Can be used as an Action, and uses 1 Channel Oath Charge.'
        },
        {
          name: 'Vow of Enmity',
          description: 'Gain Advantage on Attack Rolls against an enemy for 10 turns. Can be used as an Action, and uses 1 Channel Oath Charge.'
        },
        {
          name: 'Oath Spells',
          description: 'You gain the Bane and Hunter\'s Mark spells from your oath. They are Always Prepared.'
          // todo: add spells
        }
      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Oath Spells',
          description: 'You gain the Misty Step and Hold Person spells from your oath. They are Always Prepared.'
          // todo: add spells
        }
      ],
      [ // Level 6

      ],
      [ // Level 7
        {
          name: 'Relentless Avenger',
          description: 'If you hit an enemy with an Opportunity Attack, gain 4.5m / 15ft movement next turn.'
        }
      ],
      [ // Level 8

      ],
      [ // Level 9
        {
          name: 'Oath Spells',
          description: 'You gain the Haste and Protection from Energy spells from your oath. They are Always Prepared.'
          // todo: add spells
        }
      ],
      [ // Level 10

      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Oathbreaker',
    icon: 'assets/img/classes/150px-Paladin_Oathbreaker_Icon.png',
    description: 'You have broken your sacred Oath in pursuit of power and ambition. Only darkness remains to fuel you now.',
    features: [
      [ // level 1
        {
          name: 'Spiteful Suffering',
          description: 'Steep an enemy in the darkness that churns within you. For 3 turns, the target takes 1d4 + Charisma Modifier Necrotic damage each turn and Attack Rolls against it have Advantage. Charisma Saving Throw negates the effects. Can be used as a Action, and uses 1 Channel Oath Charge.'
        }
      ],
      [ // level 2

      ],
      [ // Level 3
        {
          name: 'Control Undead',
          description: 'Use the power of your Oath to gain control over an undead creature. The undead will follow you around and attack your enemies. The target\'s level must be lower than yours. Wisdom Saving Throw negates the effects. Can be used as an Action, and uses 1 Channel Oath Charge.'
        },
        {
          name: 'Dreadful Aspect',
          description: 'Let your darkest emotions burst forth as a menacing pulse to Frighten enemies in a 9m / 30ft radius around you for 10 turns. Wisdom Saving Throw negates the effect. Can be used as an Action, and uses 1 Channel Oath Charge.'
        },
        {
          name: 'Oath Spells',
          description: 'You gain the Hellish Rebuke and Inflict Wounds spells from your oath. They are Always Prepared.'
          // todo: add spells
        }
      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Oath Spells',
          description: 'You gain the Crown of Madness and Darkness spells from your oath. They are Always Prepared.'
          // todo: add spells
        }
      ],
      [ // Level 6

      ],
      [ // Level 7
        {
          name: 'Aura of Hate',
          description: 'You and any nearby fiends and undead gain additional damage to melee weapon attacks equal to your Charisma Modifier. The aura disappears if you fall Unconscious.'
        }
      ],
      [ // Level 8

      ],
      [ // Level 9
        {
          name: 'Oath Spells',
          description: 'You gain the Bestow Curse and Animate Undead spells from your oath. They are Always Prepared.'
          // todo: add spells
        }
      ],
      [ // Level 10

      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  }
] as Subclass[];

const RANGER_SUBCLASSES = [
  {
    name: 'Hunter',
    icon: 'assets/img/classes/Hunter_Icon.png',
    description: 'Hunters seek the most dangerous prey in Faerûn, from ancient dragons to massive hordes of undead, and excel at slaying them all.',
    features: [
      [ // level 1

      ],
      [ // level 2

      ],
      [ // Level 3
        {
          name: 'Hunter\'s Prey',
          description: 'Choose 1 type of prey.',
          featureSelection: [
            {
              name: 'Colossus Slayer',
              description: 'Once per turn, your weapon attack deals an extra 1d8 damage if the target is below its Hit Point maximum.'
            },
            {
              name: 'Giant Killer',
              description: 'Once per turn, your weapon attack deals an extra 1d8 damage if the target is below its Hit Point maximum.'
            },
            {
              name: 'Horde Breaker',
              description: 'Target two creatures standing close to each other, attacking them in quick succession.'
            }
          ],
          selectionCount: 1,
          selectSameAllowed: false
        }
      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6

      ],
      [ // Level 7
        {
          name: 'Defensive Tactics',
          description: 'Choose 1 Defensive Tactic.',
          featureSelection: [
            {
              name: 'Escape the Horde',
              description: 'Opportunity Attacks against you have Disadvantage.'
            },
            {
              name: 'Steel Will',
              description: 'Advantage on Saving Throws against being Frightened.'
            },
            {
              name: 'Multiattack Defence',
              description: 'When a enemy attacks you, they suffer -4 penalty to additional Attack Rolls against you until start of their next turn.'
            }
          ],
          selectionCount: 1,
          selectSameAllowed: false
        }
      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10

      ],
      [ // Level 11
        {
          name: 'Volley',
          description: 'Fire a cascade of arrows upon nearby enemies.'
        },
        {
          name: 'Whirlwind Attack',
          description: 'Whirl your body around striking all nearby foes.'
        }
      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Beast Master',
    icon: 'assets/img/classes/Beast_Master_Icon.png',
    description: 'Beast Masters bind themselves to an animal companion, sharing an intelligent bond that flourishes in and out of combat.',
    features: [
      [ // level 1

      ],
      [ // level 2

      ],
      [ // Level 3
        {
          name: 'Summon Companion',
          description: 'Gain a beast companion that accompanies you on your adventures and fights alongside you. You can summon a Bear, Boar, Dire Raven, Wolf, or Wolf Spider. It\'s Hit Points and damage scale with your level.'
        }
      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Companion\'s Bond',
          description: 'The bond between you and your companion grows stronger. Your Proficiency Bonus is added to its Armour Class and damage rolls.'
        }
      ],
      [ // Level 6

      ],
      [ // Level 7
        {
          name: 'Exceptional Training',
          description: 'Your companion is now able to Dash, Disengage, and Help as a Bonus Action.'
        }
      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10

      ],
      [ // Level 11
        {
          name: 'Bestial Fury',
          description: 'The bond between you and your companion has deepened. Your Summon Companion gains an Extra Attack.'
        }
      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Gloom Stalker',
    icon: 'assets/img/classes/150px-Gloom_Stalker_Icon.png',
    description: 'Emerging like a horrible gift from the envelope of darkness and shadow, you ambush and put down your foes before they can even scream.',
    features: [
      [ // level 1

      ],
      [ // level 2

      ],
      [ // Level 3
        {
          name: 'Dread Ambusher',
          description: 'You specialise in taking out foes swiftly and ruthlessly. You gain a +3 bonus to Initiative. On the first turn of combat, your Movement Speed increases by 3 m / 10 ft, and you can make an extra attack that deals an additional 1d8 damage. As a Bonus Action, you can Hide from enemies by succeeding on Stealth Checks. Attacking or casting a spell will make you visible.'
        },
        {
          name: 'Umbral Shroud',
          description: 'Use an Action to wrap yourself in shadows to become Invisible for 10 turns if you are obscured. Invisibility ends early if you attack, cast another spell, take an action, or take damage. Can be used once per Short Rest.'
        },
        {
          name: 'Superior Darkvision',
          description: 'You can see in the dark up to 24m / 80ft.'
        },
        {
          name: 'Gloom Stalker Spells',
          description: 'You gain the Disguise Self spell. It is always prepared.'
          // todo: Add spell
        }
      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Gloom Stalker Spells',
          description: 'You gain the Misty Step spell. It is always prepared.'
          // todo: Add spell
        }
      ],
      [ // Level 6

      ],
      [ // Level 7
        {
          name: 'Iron Mind',
          description: 'You gain Proficiency in Wisdom and Intelligence Saving Throws.'
        }
      ],
      [ // Level 8

      ],
      [ // Level 9
        {
          name: 'Gloom Stalker Spells',
          description: 'You gain the Fear spell. It is always prepared.'
          // todo: Add spell
        }
      ],
      [ // Level 10

      ],
      [ // Level 11
        {
          name: 'Stalker\'s Flurry',
          description: 'When you miss with a weapon attack, you can make another one for free.'
        }
      ],
      [ // Level 12

      ]
    ] as Feature[][],
  }
] as Subclass[];

const ROGUE_SUBCLASSES = [
  {
    name: 'Arcane Trickster',
    icon: 'assets/img/classes/150px-Class_Rogue_Arcane_Trickster_Badge_Icon.png',
    description: 'Arcane Tricksters are rogues with a clever touch of magic, using illusions and enchantments to keep their opponents on the back foot.',
    features: [
      [ // level 1

      ],
      [ // level 2

      ],
      [ // Level 3
        {
          name: 'Mage Hand Legerdemain',
          description: 'You learn the Mage Hand Cantrip. When you cast it, the spectral hand is invisible and can carry out additional tasks. It also doesn\'t expire until destroyed',
          effect: {
            spells: [SPELL_MAGE_HAND] as Spell[]
          }
        }
      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6

      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10

      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Thief',
    icon: 'assets/img/classes/150px-Class_Rogue_Thief_Badge_Icon.png',
    description: 'Thieves use their skills in stealth and larceny to acquire whatever they wish, whether from a third story window or the depths of long-forgotten ruins.',
    features: [
      [ // level 1

      ],
      [ // level 2

      ],
      [ // Level 3
        {
          name: 'Fast Hands',
          description: 'Gain an additional Bonus Action.'
        },
        {
          name: 'Second-Story Work',
          description: 'You\'ve mastered the art of falling and gain Resistance to Falling damage.'
        }
      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6

      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9
        {
          name: 'Supreme Sneak',
          description: 'Blend into the environment so completely that you become Invisible for 10 turns. Can be used once per Short Rest.'
        }
      ],
      [ // Level 10

      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Assassin',
    icon: 'assets/img/classes/150px-Class_Rogue_Assassin_Badge_Icon.png',
    description: 'You prefer to deal sublime punishment to a single foe at a time - not in a duel, mind, because a duel implies chivalry, and you\'re too busy getting the job done for honour.',
    features: [
      [ // level 1

      ],
      [ // level 2

      ],
      [ // Level 3
        {
          name: 'Assassin\'s Alacrity',
          description: 'Quick as an alley cat in a rain-dark city, you immediately restore your Action and Bonus Action at the start of combat.'
        },
        {
          name: 'Assassinate',
          description: 'Any successful Attack Roll against a Surprised creature is a Critical Hit. Moreover, you have Advantage on Attack Rolls against creatures that haven\'t taken a turn yet.'
        }
      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6

      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9
        {
          name: 'Infiltration Expert',
          description: 'Adopt a new identity, changing your appearance.'
        }
      ],
      [ // Level 10

      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  }
] as Subclass[];

const SORCERER_SUBCLASSES = [
  {
    name: 'Draconic Bloodline',
    icon: 'assets/img/classes/Draconic_Bloodline_Subclass_Icon.png',
    description: 'Your veins carry draconic magic, the result of a powerful dragon ancestor.',
    features: [
      [ // level 1
        {
          name: 'Draconic Resilience',
          description: 'Your maximum Hit Points increase by 1 for each Sorcerer Level. Dragon-like scales cover parts of your skin. When you aren\'t wearing Armour, your Base Armour Class is 13 + Dexterity Modifier.'
        },
        {
          name: 'Draconic Ancestry',
          description: 'Choose 1 type of Draconic Ancestry.',
          featureSelection: [
            {
              name: 'Red (Fire)',
              description: 'Fire is the damage type associated with your dragon ancestor. Burning Hands is added to your spell list.',
              effect: {
                spells: [SPELL_BURNING_HANDS] as Spell[]
              }
            },
            {
              name: 'Black (Acid)',
              description: 'Acid is the damage type associated with your dragon ancestor. Grease is added to your spell list.',
              //todo: add Grease spell
            },
            {
              name: 'Blue (Lightning)',
              description: 'Lightning is the damage type associated with your dragon ancestor. Witch Bolt is added to your spell list.',
              //todo: add Witch Bolt spell
            },
            {
              name: 'White (Cold)',
              description: 'Cold is the damage type associated with your dragon ancestor. Armour of Agathys is added to your spell list.',
              //todo: add Armour of Agathys spell
            },
            {
              name: 'Green (Poison)',
              description: 'Poison is the damage type associated with your dragon ancestor. Ray of Sickness is added to your spell list.',
              //todo: add Ray of Sickness spell
            },
            {
              name: 'Gold (Fire)',
              description: 'Fire is the damage type associated with your dragon ancestor. Disguise Self is added to your spell list.',
              //todo: add Disguise Self spell
            },
            {
              name: 'Silver (Cold)',
              description: 'Cold is the damage type associated with your dragon ancestor. Feather Fall is added to your spell list.',
              //todo: add Feather Fall spell
            },
            {
              name: 'Bronze (Lightning)',
              description: 'Lightning is the damage type associated with your dragon ancestor. Fog Cloud is added to your spell list.',
              //todo: add Fog Cloud spell
            },
            {
              name: 'Copper (Acid)',
              description: 'Acid is the damage type associated with your dragon ancestor. Tasha\'s Hideous Laughter is added to your spell list.',
              //todo: add Tasha's Hideous Laughter spell
            },
            {
              name: 'Brass (Fire)',
              description: 'Fire is the damage type associated with your dragon ancestor. Sleep is added to your spell list.',
              //todo: add Sleep spell
            }
          ],
          selectionCount: 1,
          selectSameAllowed: false
        }
      ],
      [ // level 2

      ],
      [ // Level 3

      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6
        {
          name: 'Elemental Affinity',
          description: 'When you cast a spell that deals damage of the type associated with your draconic ancestry, you add your Charisma modifier to the damage. Moreover, you can spend 1 Sorcery Point to gain Resistance to that damage type.'
        }
      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10

      ],
      [ // Level 11
        {
          name: 'Flight',
          description: 'You can now fly at will using your movement speed.'
        }
      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Wild Magic',
    icon: 'assets/img/classes/Wild_Magic_Subclass_Icon.png',
    description: 'Your powers come from ancient forces of chaos. They churn within you -- waiting to burst free at any time.',
    features: [
      [ // level 1
        {
          name: 'Wild Magic',
          description: 'Wild Magic stems from the forces of chaos. It churns within the sorcerers that wield it, waiting to burst free. Each time you cast a Spell of level 1 or higher, your magic might surge and trigger a random magical effect.'
        },
        {
          name: 'Tides of Chaos',
          description: 'Activate to gain Advantage on your next Attack Roll, Ability Check, or Saving Throw. Increased chance of Wild Magic surge afterwards. Can be used once per Short Rest.'
        }
      ],
      [ // level 2

      ],
      [ // Level 3

      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6
        {
          name: 'Bend Luck',
          description: 'Spend 2 Sorcery Points as a free action to give a target a 1d4 bonus or a 1d4 penalty to all Ability Checks for two turns, or spend 2 Sorcery Points as a reaction to give a target a 1d4 bonus or a 1d4 penalty on a single Attack Roll or Saving Throw.'
        }
      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10

      ],
      [ // Level 11
        {
          name: 'Controlled Chaos',
          description: 'When an enemy casts a spell, spend your reaction to cause a Wild Magic Surge near the enemy . This Wild Magic Surge is more likely to be negative.'
        }
      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Storm Sorcery',
    icon: 'assets/img/classes/150px-Storm_Sorcery_Subclass_Icon.png',
    description: 'Whether crackling with the energy of ancient deluges or pierced by gales and hurricanes, your lineage is a strange tapestry scrawled by a tempest.',
    features: [
      [ // level 1
        {
          name: 'Tempestuous Magic',
          description: 'After you cast a Spell of level 1 or higher you can Fly as a Bonus Action until the end of your turn without receiving Opportunity Attacks. The distance you fly is up to 9m / 30ft.'
        }
      ],
      [ // level 2

      ],
      [ // Level 3

      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6
        {
          name: 'Heart of the Storm',
          description: 'When you cast a Spell of level 1 or higher that deals Lightning or Thunder damage, you cause a small, local storm. All enemies within 6m / 20ft take additional Lightning or Thunder damage equal to half your Sorcerer level. Moreover, you are Resistant to Lightning and Thunder damage.'
        }
      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10

      ],
      [ // Level 11
        {
          name: 'Storm\'s Fury',
          description: 'When you are hit by a melee attack, you deal Lightning equal to your Sorcerer level to the attacker. The attacker will also have to make a Strength Saving Throw. On a failed save, the attacker is pushed away from you 6m / 20ft in a straight line.'
        }
      ],
      [ // Level 12

      ]
    ] as Feature[][],
  }
] as Subclass[];

const WARLOCK_SUBCLASSES = [
  {
    name: 'The Archfey',
    icon: 'assets/img/classes/150px-The_Archfey_Subclass_Icon.png',
    description: 'Graced by a lady or lord of the fey, you are imbued with all the sumptuous and scary qualities of your patron\'s extraordinary realm.',
    features: [
      [ // level 1
        {
          name: 'Fey Presence',
          description: 'Charm or Frighten nearby foes with the feywild\'s beguiling, disturbing magics, for 2 turns. A successful Wisdom Save negates the effects. Can be used once per Short Rest.'
        },
        {
          name: 'Expanded Spell List',
          description: 'You add the 1st level spells Faerie Fire and Sleep to your spell list.'
          // todo: add Faerie Fire and Sleep spells
        }
      ],
      [ // level 2

      ],
      [ // Level 3
        {
          name: 'Expanded Spell List',
          description: 'You add the 2nd level spells Calm Emotions and Phantasmal Force to your spell list.'
          // todo: add Calm Emotions and Phantasmal Force spells
        }
      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Expanded Spell List',
          description: 'You add the 3rd level spells Blink and Plant Growth to your spell list.'
          // todo: add spells
        }
      ],
      [ // Level 6
        {
          name: 'Misty Escape',
          description: 'Upon taking damage, become Invisible. On your next turn, you can cast Misty Step, though this will break your invisibility.'
        }
      ],
      [ // Level 7
        {
          name: 'Expanded Spell List',
          description: 'You add the 4th level spells Dominate Beast and Greater Invisibility to your spell list.'
          // todo: add spells
        }
      ],
      [ // Level 8

      ],
      [ // Level 9
        {
          name: 'Expanded Spell List',
          description: 'You add the 5th level spells Dominate Person and Seeming to your spell list.'
          // todo: add spells
        }
      ],
      [ // Level 10
        {
          name: 'Beguiling Defenses',
          description: 'You are immune to being charmed.'
        }
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'The Fiend',
    icon: 'assets/img/classes/The_Fiend_Subclass_Icon.png',
    description: 'Warlocks in service to fiends work toward corrupting, destructive ends -- intentionally or otherwise -- and receive hellish blessings in turn.',
    features: [
      [ // level 1
        {
          name: 'Dark One\'s Blessing',
          description: 'Whenever you reduce a hostile creature to 0 Hit Points, this gift from your patron grants you Charisma Modifier + Warlock Level temporary hit points.'
        },
        {
          name: 'Expanded Spell List',
          description: 'You add the 1st level spells Burning Hands and Command to your spell list.'
          // todo: add spells
        }
      ],
      [ // level 2

      ],
      [ // Level 3
        {
          name: 'Expanded Spell List',
          description: 'You add the 2nd level spells Blindness and Scorching Ray to your spell list.'
          // todo: add spells
        }
      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Expanded Spell List',
          description: 'You add the 3rd level spells Fireball and Stinking Cloud to your spell list.'
          // todo: add spells
        }
      ],
      [ // Level 6
        {
          name: 'Dark One\'s Own Luck',
          description: 'Call on your patron to change your fate and add a 1d10 to an Ability Check once per short rest.'
        }
      ],
      [ // Level 7
        {
          name: 'Expanded Spell List',
          description: 'You add the 4th level spells Wall of Fire and Fire Shield to your spell list.'
          // todo: add spells
        }
      ],
      [ // Level 8

      ],
      [ // Level 9
        {
          name: 'Expanded Spell List',
          description: 'You add the 5th level spells Cone of Cold and Flame Strike to your spell list.'
          // todo: add spells
        }
      ],
      [ // Level 10
        {
          name: 'Fiendish Resilience',
          description: 'Choose a damage type and become Resistant to it. You can pick a new damage type each Short Rest.'
        }
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'The Great Old One',
    icon: 'assets/img/classes/The_Great_Old_One_Subclass_Icon.png',
    description: 'Warlocks bound to eldritch beings in the Far Realms work toward inscrutable goals, gaining strange powers over entropy and the mind.',
    features: [
      [ // level 1
        {
          name: 'Mortal Reminder',
          description: 'When you land a Critical Hit against a creature, that creature and any nearby enemies must succeed a Wisdom Saving Throw or become Frightened until the end of their next turn.'
        },
        {
          name: 'Expanded Spell List',
          description: 'You add the 1st level spells Dissonant Whispers and Tasha\'s Hideous Laughter to your spell list.'
          // todo: add spells
        }
      ],
      [ // level 2

      ],
      [ // Level 3
        {
          name: 'Expanded Spell List',
          description: 'You add the 2nd level spells Detect Thoughts and Phantasmal Force to your spell list.'
          // todo: add spells
        }
      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Expanded Spell List',
          description: 'You add the 3rd level spells Bestow Curse and Slow to your spell list.'
          // todo: add spells
        }
      ],
      [ // Level 6
        {
          name: 'Entropic Ward',
          description: 'Impose Disadvantage on an Attack Roll against you as a Reaction. If the attack misses, you gain Advantage on your next Attack Roll against the attacker for 1 turn.'
        }
      ],
      [ // Level 7
        {
          name: 'Expanded Spell List',
          description: 'You add the 4th level spells Dominate Beast and Evard\'s Black Tentacles to your spell list.'
          // todo: add spells
        }
      ],
      [ // Level 8

      ],
      [ // Level 9
        {
          name: 'Expanded Spell List',
          description: 'You add the 5th level spells Dominate Person and Telekinesis to your spell list.'
          // todo: add spells
        }
      ],
      [ // Level 10
        {
          name: 'Thought Shield',
          description: 'An elder being shields your brain. You gain Resistance to Psychic damage. When you take Psychic damage, your attacker takes the same damage.'
        }
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  }
] as Subclass[];

const WIZARD_SUBCLASSES = [
  {
    name: 'Abjuration School',
    icon: 'assets/img/classes/Abjuration_School_Subclass_Icon.png',
    description: 'Abjuration spells summon wards, banish enemies, and nullify magic, suitable for those who wish to defend themselves and others.',
    features: [
      [ // level 1

      ],
      [ // level 2
        {
          name: 'Abjuration Savant',
          description: 'Halves the cost to learn Abjuration spells from Scrolls. Learning these spells will only cost 25gp per spell level.'
        },
        {
          name: 'Arcane Ward',
          description: 'The residual magic of your spells forms a ward around you that protects you from harm. Each time you cast an Abjuration spell, the intensity of the ward increases by the amount of the spell\'s Level, up to a maximum of 2x your Wizard level. Each time you take damage, the ward blocks an amount of damage equal to its intensity, and its intensity decreases by 1. After each Long Rest, the ward\'s intensity resets, and becomes the same as your Wizard level.'
        }
      ],
      [ // Level 3

      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6
        {
          name: 'Projected Ward',
          description: 'As a reaction, when a nearby ally takes damage you can sacrifice your Arcane Ward to reduce the damage they take, and its intensity reduces by 1.'
        }
      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10
        {
          name: 'Improved Abjuration',
          description: 'Each time you take a Short Rest the intensity of your Arcane Ward increases by an amount equal to your Wizard level.'
        }
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Conjuration School',
    icon: 'assets/img/classes/150px-Wizard_conjuration.png',
    description: 'Compelled by shape and motion, your favoured arcane style involves summoning creatures and objects, as well as displacing them in space.',
    features: [
      [ // level 1

      ],
      [ // level 2
        {
          name: 'Conjuration Savant',
          description: 'Halves the cost to learn Conjuration spells from Scrolls. Learning these spells will only cost 25gp per spell level.'
        },
        {
          name: 'Minor Conjuration (Create Water)',
          description: 'Gain the ability to cast the Create Water spell without consuming a spell slot, once per Short Rest.'
        }
      ],
      [ // Level 3

      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6
        {
          name: 'Benign Transposition',
          description: 'Gain the ability to teleport yourself to an unoccupied space or swap with an ally (of size Medium or Small) within Range9m / 30ft. Once used, this action cannot be reused until after a Long Rest or casting a Conjuration Spell of Level 1 or higher.'
        }
      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10
        {
          name: 'Focused Conjuration',
          description: 'Damage taken while you are Concentrating on a Conjuration spell will not break your concentration.'
        }
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Divination School',
    icon: 'assets/img/classes/150px-Wizard_divination.png',
    description: 'You peer through time itself and can sculpt the future, like temporal clay, into a more favourable form.',
    features: [
      [ // level 1

      ],
      [ // level 2
        {
          name: 'Divination Savant',
          description: 'Halves the cost to learn Divination spells from Scrolls. Learning these spells will only cost 25gp per spell level.'
        },
        {
          name: 'Portent',
          description: 'Your dreams grant you glimpses that let you influence the future. After each Long Rest you gain two random Portent Dice. During the day, you can use your reaction to change the die of any Attack Roll or Saving Throw rolled within Range18m / 60ft of you to one of your Portent Dice. Each Portent Die can only be used once, and you lose your unused Portent Dice at the end of the day.'
        }
      ],
      [ // Level 3

      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6
        {
          name: 'Expert Divination',
          description: 'You may gain an additional Portent Die: when taking a Short Rest you receive a Prophecy. Complete it to regain a missing Portent Die.'
        }
      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10
        {
          name: 'Third Eye',
          description: 'Once per long rest, use an action to gain Superior Darkvision and cast See Invisibility without expending a Spell Slot.'
        }
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Enchantment School',
    icon: 'assets/img/classes/150px-Wizard_enchantment.png',
    description: 'Your magic influences, beguiles, and bends the will and conviction of those around you.',
    features: [
      [ // level 1

      ],
      [ // level 2
        {
          name: 'Enchantment Savant',
          description: 'Halves the cost to learn Enchantment spells from Scrolls. Learning these spells will only cost 25gp per spell level.'
        },
        {
          name: 'Hypnotic Gaze',
          description: 'Once per Long Rest, use an action to target a non-ally creature within Range1.5m / 5ft. The target must succeed on a Wisdom icon.png Wisdom Saving Throw or become charmed and incapacitated for 2 turns. As an action, while still within range, you can maintain the charm, extending the charm for another 2 turns. If the target takes damage, the effect ends.'
        }
      ],
      [ // Level 3

      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6
        {
          name: 'Instinctive Charm',
          description: 'As a Reaction, charm an enemy that attacks you. They must succeed on a Wisdom saving throw or attack a new target if possible. If a target succeeds on its saving throw, it cannot be affected by this ability until after a long rest.'
        }
      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10
        {
          name: 'Split Enchantment',
          description: 'You know your enchantments inside and out. You can target 2 creatures with Enchantment spells that would normally only target 1 creature.'
        }
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Evocation School',
    icon: 'assets/img/classes/Evocation_School_Subclass_Icon.png',
    description: 'Evocation spells focus elemental energy into powerful attacks and enchantments. Those who specialise in this school are known as evokers.',
    features: [
      [ // level 1

      ],
      [ // level 2
        {
          name: 'Evocation Savant',
          description: 'Halves the cost to learn Evocation spells from Scrolls. Learning these spells will only cost 25gp per spell level.'
        },
        {
          name: 'Sculpt Spells',
          description: 'Create pockets of safety within your Evocation spells. Allies automatically succeed their Saving Throws and take no damage from these spells.'
        }
      ],
      [ // Level 3

      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6
        {
          name: 'Potent Cantrip',
          description: 'Your Cantrips become harder to evade entirely. When a creature succeeds its Saving Throw against one of your cantrips, it still takes half the cantrip\'s damage, but suffers no additional effects.'
        }
      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10
        {
          name: 'Empowered Evocation',
          description: 'Your grasp of Evocation magic has tightened, and you can add your Intelligence Modifier to damage rolls with any Evocation spells.'
        }
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Illusion School',
    icon: 'assets/img/classes/150px-Wizard_illusion.png',
    description: 'You specialise in unravelling and restitching the fabric of reality to fool the unwary, the gullible, and the monstrous.',
    features: [
      [ // level 1

      ],
      [ // level 2
        {
          name: 'Illusion Savant',
          description: 'Halves the cost to learn Illusion spells from Scrolls. Learning these spells will only cost 25gp per spell level.'
        },
        {
          name: 'Improved Minor Illusion',
          description: 'You can cast Minor Illusion as a Bonus Action. You can remain hidden while casting this spell. This spell can be cast while you are Silenced.'
        }
      ],
      [ // Level 3

      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6
        {
          name: 'See Invisibility',
          description: 'Once per Short Rest, as an Action, you can cast the spell See Invisibility without consuming a Spell Slot.'
        }
      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10
        {
          name: 'Illusory Self',
          description: 'As a Reaction, you magically fashion an illusory duplicate of yourself when attacked, causing the attack to miss. After use, this ability cannot be used until after a short rest.'
        }
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Necromancy School',
    icon: 'assets/img/classes/150px-Wizard_necromancy.png',
    description: 'You walk through the valley of the shadow of death, and make lots of friends there.',
    features: [
      [ // level 1

      ],
      [ // level 2
        {
          name: 'Necromancy Savant',
          description: 'Halves the cost to learn Necromancy spells from Scrolls. Learning these spells will only cost 25gp per spell level.'
        },
        {
          name: 'Grim Harvest',
          description: 'Once per turn, if you kill a creature with a spell, you regain Hit Points equal to twice the Spell Slot level used - thrice if it\'s a Necromancy spell. Undead and Constructs are unaffected.'
        }
      ],
      [ // Level 3

      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6
        {
          name: 'Undead Thralls',
          description: 'You gain the Animate Dead spell, if you didn\'t have it already. When you use Animate Dead, you can raise an additional corpse. Creatures created with Animate Dead have additional hit points equal to your Wizard level, and your Proficiency Bonus is added to their damage.'
        }
      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10
        {
          name: 'Inured to Undeath',
          description: 'You have steeped youself so completely in death that you are Resistant to Necrotic damage, and your hit point maximum cannot be reduced.'
        }
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  },
  {
    name: 'Transmutation School',
    icon: 'assets/img/classes/150px-Wizard_transmutation.png',
    description: 'Your innate fascination with the structure of things has granted you an incredible power over crude matter and its manipulation.',
    features: [
      [ // level 1

      ],
      [ // level 2
        {
          name: 'Transmutation Savant',
          description: 'Halves the cost to learn Transmutation spells from Scrolls. Learning these spells will only cost 25gp per spell level.'
        },
        {
          name: 'Experimental Alchemy',
          description: 'You brew two Alchemical Solutions instead of one when combining extracts, if you succeed a Difficulty Class 15 Medicine Check.'
        }
      ],
      [ // Level 3

      ],
      [ // Level 4

      ],
      [ // Level 5

      ],
      [ // Level 6
        {
          name: 'Transmuter\'s Stone',
          description: 'You can create Transmuter\'s Stones, storing some of your transmutation magic. A creature carrying the stone gains a benefit of your choice: Darkvision, Proficiency in Constitution Saving Throws, Movement Speed increased by 3m / 10ft, or Resistance to Acid, Cold, Fire, Lightning, or Thunder. Once you have created a stone, you must cast a Transmutation spell of level 1 or above or finish a Long Rest before you can create another one. Only one stone can be active at a time.'
        }
      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10
        {
          name: 'Shapechanger',
          description: 'Transform into a blue jay, able to Fly. If the blue jay\'s Hit Points drop to 0, the target reverts to its original form with its original hit points.'
        }
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
  }
] as Subclass[];














export const CLASSES = [
  {
    name: 'Barbarian',
    icon: 'assets/img/classes/160px-Class_Barbarian_Badge_Icon.png',
    flavorText: 'The strong embrace the wild that hides inside - keen instincts, primal physicality, and most of all, an unbridled, unquenchable rage.',
    description: 'Barbarians use their martial prowess and primal rage to strengthen themselves and dominate enemies in combat. Barbarians\' key ability scores are Strength, Dexterity, and Constitution.',
    savingThrowProficiencies: [Ability.Strength, Ability.Dexterity],
    skillProficiencies: {
      name: 'Skill Proficiency',
      description: 'Choose two class skills in which to gain profiency.',
      skillSelection: BARBARIAN_SKILLS,
      selectionCount: 2,
      selectSameAllowed: false
    } as Feature,
    equipmentProficiencies: [
      EquipmentProficiency.SimpleWeapons,
      EquipmentProficiency.MartialWeapons,
      EquipmentProficiency.Shields
    ] as EquipmentProficiency[],
    initialEquipmentProficiencies: [
      EquipmentProficiency.LightArmor,
      EquipmentProficiency.MediumArmor,
    ] as EquipmentProficiency[],
    features: [
      [ // Level 1
        {
          name: 'Rage',
          description: 'Release your unbridled fury to become stronger and more robust. While Raging, you deal 2 extra damage with melee and improvised weapons, and when throwing objects. You also have Resistance to physical damage and Advantage on Strength Checks and Saving Throws. Rage ends early if you haven\'t attacked a creature or taken damage since your last turn. Activating Rage is a Bonus Action.'
        } as Feature,
        {
          name: 'Unarmoured Defence (Barbarian)',
          description: 'Your body is as resilient as any armour. You add your Constitution modifier to your Armour Class in addition to your Dexterity modifier when not wearing armour.'
        } as Feature
      ],
      [ // Level 2
        {
          name: 'Reckless Attack',
          description: 'Make a Reckless Attack, granting you Advantage on attacks but also granting enemies Advantage on attacks against you.'
        } as Feature,
        {
          name: 'Danger Sense',
          description: 'You have Advantage on Dexterity Saving Throws against traps, Spells and surfaces while not Blinded, Deafened, or Incapacitated.'
        } as Feature
      ],
      [ // Level 3

      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Extra Attack',
          description: 'You can make an additional free attack after making an unarmed or weapon attack.'
        } as Feature,
        {
          name: 'Fast Movement',
          description: 'Your Movement Speed increases by 1.5 m / 5 ft while not wearing Heavy Armour.'
        } as Feature
      ],
      [ // Level 6

      ],
      [ // Level 7
        {
          name: 'Feral Instinct',
          description: 'You have honed your instincts to the utmost degree. You gain a +3 to Initiative and can\'t be Surprised.'
        } as Feature
      ],
      [ // Level 8

      ],
      [ // Level 9
        {
          name: 'Brutal Critical',
          description: 'You\'ve trained to strike swift and true. When you land a Critical Hit, you roll an extra damage die as well as the normal additional critical die.'
        } as Feature
      ],
      [ // Level 10

      ],
      [ // Level 11
        {
          name: 'Relentless Rage',
          description: 'Once per Short Rest, if you drop to 0 hit point while Enraged, you regain 1 hit points instead of being Downed.'
        } as Feature
      ],
      [ // Level 12

      ]
    ] as Feature[][],
    featLevels: [4, 8, 12],
    subclassLevel: 3,
    subclasses: BARBARIAN_SUBCLASSES
  },
  {
    name: 'Bard',
    icon: 'assets/img/classes/160px-Bard_Class_Icon.png',
    flavorText: 'You know music is more than a fancy - it is power. Through study and adventure, you have mastered song, speech, and the magic within.',
    description: 'Bards are expert storytellers, artists, and performers. Bards channel their power through song, speech, or performance to cast Spells. Bards use Charisma as their primary ability score.',
    savingThrowProficiencies: [Ability.Dexterity, Ability.Charisma],
    skillProficiencies: {
      name: 'Skill Proficiency',
      description: 'Choose two class skills in which to gain profiency.',
      skillSelection: BARD_SKILLS,
      selectionCount: 2,
      selectSameAllowed: false
    } as Feature,
    equipmentProficiencies: [
      EquipmentProficiency.LightArmor
    ] as EquipmentProficiency[],
    initialEquipmentProficiencies: [
      EquipmentProficiency.SimpleWeapons,
      EquipmentProficiency.HandCrossbows,
      EquipmentProficiency.Rapiers,
      EquipmentProficiency.Longswords,
      EquipmentProficiency.Shortswords,
    ] as EquipmentProficiency[],
    features: [
      [ // Level 1
        {
          name: 'Bardic Inspiration',
          description: 'As a bonus action, you can inspire an ally to go beyond their capabilities with your performance. They can add a +1d6 bonus to their next Attack Roll, Ability Check, or Saving Throw.'
        } as Feature,
        {
          name: 'Bonus Skill Proficiency',
          description: 'Choose one additional class skill in which to gain profiency.',
          skillSelection: BARD_SKILLS,
          selectionCount: 1,
          selectSameAllowed: false
        } as Feature
      ],
      [ // Level 2
        {
          name: 'Song of Rest',
          description: 'Once per long rest, you can use your craft to soothe. You and your allies are revitalized as though you had taken a Short Rest.'
        } as Feature,
        {
          name: 'Jack of All Trades',
          description: 'Your vast experiences make you more likely to succeed in any undertaking. Add half of your Proficiency Bonus (rounded down) to Ability Checks that you are not Proficient in.'
        } as Feature
      ],
      [ // Level 3
        {
          name: 'Skill Expertise',
          description: 'Gain Expertise in 2 Skills you are proficient in.',
          skillExpertiseSelection: true,
          selectionCount: 2,
          selectSameAllowed: false
        } as Feature
      ],
      [ ], // Level 4
      [ // Level 5
        {
          name: 'Font of Inspiration',
          description: 'You regain all of your Bardic Inspiration Charges after a Long or Short Rest.'
        } as Feature,
        {
          name: 'Improved Bardic Inspiration',
          description: 'Your Bardic Inspiration bonus improves to 1d8.'
        } as Feature
      ],
      [ // Level 6
        {
          name: 'Countercharm',
          description: 'For 3 turns, you and any allies within 9 m / 30 ft have Advantage on Saving Throws against being Charmed or Frightened.'
        } as Feature
      ],
      [ ], // Level 7
      [ ], // Level 8
      [ ], // Level 9
      [ // Level 10
        {
          name: 'Skill Expertise',
          description: 'Gain Expertise in 2 Skills you are proficient in.',
          skillExpertiseSelection: true,
          selectionCount: 2,
          selectSameAllowed: false
        } as Feature,
        {
          name: 'Magical Secrets',
          description: 'Learn 2 non-bard spells from the Magical Secrets spell list.'
          // todo: spell selection
        } as Feature,
        {
          name: 'Improved Bardic Inspiration',
          description: 'Your Bardic Inspiration bonus improves to 1d10.'
        } as Feature
      ],
      [ ], // Level 11
      [ ] // Level 12
    ] as Feature[][],
    featLevels: [4, 8, 12],
    subclassLevel: 3,
    subclasses: BARD_SUBCLASSES
  },
  {
    name: 'Cleric',
    icon: 'assets/img/classes/150px-Cleric_Class_300px.png',
    flavorText: 'Clerics are representatives of the gods they worship, wielding potent divine magic for good or ill.',
    description: 'Clerics worship and enact their deities\' will to the world, for better or worse. They channel the power of their faith to cast Spells. Clerics use Wisdom as their primary ability score.',
    savingThrowProficiencies: [Ability.Wisdom, Ability.Charisma],
    skillProficiencies: {
      name: 'Skill Proficiency',
      description: 'Choose two class skills in which to gain profiency.',
      skillSelection: CLERIC_SKILLS,
      selectionCount: 2,
      selectSameAllowed: false
    } as Feature,
    equipmentProficiencies: [
      EquipmentProficiency.LightArmor,
      EquipmentProficiency.MediumArmor,
      EquipmentProficiency.Shields,
      EquipmentProficiency.Morningstars
    ] as EquipmentProficiency[],
    initialEquipmentProficiencies: [
      EquipmentProficiency.SimpleWeapons, EquipmentProficiency.Flails
    ] as EquipmentProficiency[],
    features: [
      [ // Level 1

      ],
      [ // Level 2
        {
          name: 'Turn Undead',
          description: 'As an action and using a Channel Divinity Charge, present your holy symbol and pray. Each undead that can see or hear you is forced to spend its turns fleeing from you, until it takes damage.'
        }
      ],
      [ // Level 3

      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Destroy Undead',
          description: 'Your Turn Undead ability deals 4d6 Radiant damage to Turned creatures.'
        },
        {
          name: 'Cantrip Damage Boost',
          description: 'Damage rolls for cantrips roll an additional die.'
        }
      ],
      [ // Level 6

      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10
        {
          name: 'Divine Intervention',
          description: 'Call upon your deity in your moment of greatest need. Once they interfere, your deity will never do so again in this manner. Sunder the Heretical deals 8d10 Radiant damage to all enemies in a 50 ft (15 m) radius. Targets can succeed on a Wisdom saving throw to take half damage. Opulent Revival resurrects fallen companions within an 18m / 60ft radius with half their hit points, and restores all nearby allies as if they had Long Rested. Arm Thy Servant creates the Devotee\'s Mace in the cleric\'s inventory and immediately equips it. Golden Generosity spawns a chest Bestowed Bounty on the ground next to the cleric, containing camp supplies, superior or supreme healing potions, and six to seven elixirs.'
        },
        {
          name: 'Cantrip Damage Boost',
          description: 'Damage rolls for cantrips roll an additional die.'
        }
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
    featLevels: [4, 8, 12],
    subclassLevel: 1,
    subclasses: CLERIC_SUBCLASSES
  },
  {
    name: 'Druid',
    icon: 'assets/img/classes/150px-Druid_Class_300px.png',
    flavorText: 'Druids channel the elemental forces of nature and share a deep kinship with animals. Mastery of Wild Shape allows them to transform into beasts from all over the Realms.',
    description: 'Druids are closely attuned with nature and the animals that live in it. They ultilize the power of nature to cast Spells and have the ability to transform into various creatures. Druids use Wisdom as their primary ability score.',
    savingThrowProficiencies: [Ability.Wisdom, Ability.Intelligence],
    skillProficiencies: {
      name: 'Skill Proficiency',
      description: 'Choose two class skills in which to gain profiency.',
      skillSelection: DRUID_SKILLS,
      selectionCount: 2,
      selectSameAllowed: false
    } as Feature,
    equipmentProficiencies: [
      EquipmentProficiency.LightArmor,
      EquipmentProficiency.MediumArmor,
      EquipmentProficiency.Shields
    ] as EquipmentProficiency[],
    initialEquipmentProficiencies: [
      EquipmentProficiency.Clubs, EquipmentProficiency.Daggers, EquipmentProficiency.Javelins,
      EquipmentProficiency.Maces, EquipmentProficiency.Quarterstaves, EquipmentProficiency.Scimitars,
      EquipmentProficiency.Sickles, EquipmentProficiency.Spears
    ] as EquipmentProficiency[],
    features: [
      [ // Level 1

      ],
      [ // Level 2
        {
          name: 'Wild Shape',
          description: 'Magically assume the shape of a beast. While in animal shape, you can\'t talk or cast Spells. You can take on the attributes your beast form - excluding your Intelligence, Wisdom, and Charisma scores. When your beast form drops to 0 Hit Points, you revert to your normal form. You may take the form of a Badger, Cat, Spider, and Wolf.'
        }
      ],
      [ // Level 3

      ],
      [ // Level 4
        {
          name: 'Wild Shape Improvement',
          description: 'You can now Wild Shape into a Deep Rothé.'
        }
      ],
      [ // Level 5

      ],
      [ // Level 6
        {
          name: 'Wild Shape Improvement',
          description: 'You can now Wild Shape into a Panther, and Owlbear.'
        }
      ],
      [ // Level 7

      ],
      [ // Level 8
        {
          name: 'Wild Shape Improvement',
          description: 'You can now Wild Shape into a Sabre-Toothed Tiger.'
        }
      ],
      [ // Level 9

      ],
      [ // Level 10
        {
          name: 'Wild Shape Improvement',
          description: 'You can now Wild Shape into a Dilophosaurus.'
        }
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
    featLevels: [4, 8, 12],
    subclassLevel: 2,
    subclasses: DRUID_SUBCLASSES
  },
  {
    name: 'Fighter',
    icon: 'assets/img/classes/Fighter_Class_Icon.png',
    flavorText: 'Fighters have mastered the art of combat, wielding weapons with unmatched skill and wearing armour like a second skin.',
    description: 'Fighters use their martial expertise to fight enemies and support their allies in combat. Fighters use Strength or Dexterity as their primary ability score.',
    savingThrowProficiencies: [Ability.Strength, Ability.Constitution],
    skillProficiencies: {
      name: 'Skill Proficiency',
      description: 'Choose two class skills in which to gain profiency.',
      skillSelection: FIGHTER_SKILLS,
      selectionCount: 2,
      selectSameAllowed: false
    } as Feature,
    equipmentProficiencies: [
      EquipmentProficiency.LightArmor, EquipmentProficiency.MediumArmor,
      EquipmentProficiency.Shields, EquipmentProficiency.SimpleWeapons,
      EquipmentProficiency.MartialWeapons
    ] as EquipmentProficiency[],
    initialEquipmentProficiencies: [
      EquipmentProficiency.HeavyArmor
    ] as EquipmentProficiency[],
    features: [
      [ // Level 1
        {
          name: 'Second Wind',
          description: 'Draw on your stamina to protect yourself. You regain 1d10 + Fighter Level Hit Points as a Bonus Action. Can be used once per short rest.'
        },
        {
          name: 'Fighting Style',
          description: 'Choose 1 fighting style.',
          featureSelection: [
            {
              name: 'Archery',
              description: 'You gain a +2 bonus to Attack Rolls you make with ranged Weapons.'
            },
            {
              name: 'Defense',
              description: 'You gain a +1 bonus to Armour Class while wearing Armour.'
            },
            {
              name: 'Duelling',
              description: 'When you are wielding a melee weapon that is not Two-Handed or Versatile in one hand, and no weapon in the other, you deal an additional 2 damage with that weapon.'
            },
            {
              name: 'Great Weapon Fighting',
              description: 'When you roll a 1 or 2 on a damage die for an attack with a Two-Handed melee weapon, that die is rerolled once.'
            },
            {
              name: 'Protection',
              description: 'When you have a Shield, impose Disadvantage on an enemy who attacks one of your allies when you are within 1.5 m / 5 ft. You must be able to see the enemy. This is a reaction.'
            },
            {
              name: 'Two Weapon Fighting',
              description: 'When you make an offhand attack, you can add your Ability Score Modifier to the damage of the attack.'
            }
          ] as Feature[],
          selectionCount: 1,
          selectSameAllowed: false
        } as Feature
      ],
      [ // Level 2
        {
          name: 'Action Surge',
          description: 'Immediately gain an extra action to use this turn. Can be used once per Short Rest.'
        }
      ],
      [ // Level 3

      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Extra Attack',
          description: 'You can make an additional free attack after making an unarmed or weapon attack. If you gain the Extra Attack feature from more than one class, they don\'t add together.'
        } as Feature
      ],
      [ // Level 6

      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9
        {
          name: 'Indomitable',
          description: 'You have become as durable as an iron golem. Whenever you fail a Saving Throw, you can roll again, using the new result instead. Can be used once per Long Rest.'
        }
      ],
      [ // Level 10

      ],
      [ // Level 11
        {
          name: 'Improved Extra Attack',
          description: 'You can make two additional attacks after attacking with your main-hand weapon.'
        }
      ],
      [ // Level 12

      ]
    ] as Feature[][],
    featLevels: [4, 6, 8, 12],
    subclassLevel: 3,
    subclasses: FIGHTER_SUBCLASSES
  },
  {
    name: 'Monk',
    icon: 'assets/img/classes/160px-Monk_Class_Icon.png',
    flavorText: 'Channel your cosmic enlightenment by deftly dodging and efficiently disassembling your foes through stunning strikes and a whirlwind of martial art attacks.',
    description: 'Monks are unarmed combatants capable of spending Ki Points to perform special abilities. The Monk\'s key ability scores are Wisdom, Dexterity, and Constitution.',
    savingThrowProficiencies: [Ability.Strength, Ability.Dexterity],
    skillProficiencies: {
      name: 'Skill Proficiency',
      description: 'Choose two class skills in which to gain profiency.',
      skillSelection: MONK_SKILLS,
      selectionCount: 2,
      selectSameAllowed: false
    } as Feature,
    equipmentProficiencies: [
      EquipmentProficiency.SimpleWeapons, EquipmentProficiency.Shortswords
    ] as EquipmentProficiency[],
    initialEquipmentProficiencies: [

    ] as EquipmentProficiency[],
    features: [
      [ // Level 1
        {
          name: 'Unarmoured Defence (Monk)',
          description: 'Your reflexes are as effective as any armour. While not wearing armour, you add your Wisdom Modifier to your Armour Class.'
        },
        {
          name: 'Flurry of Blows',
          description: 'Punch twice in quick succession, as a Bonus Action. Consumes a Ki Point.'
        },
        {
          name: 'Dextrous Attacks',
          description: 'Attacks with Monk Weapons and unarmed attacks scale with your Dexterity instead of your Strength if your Dexterity is higher.'
        },
        {
          name: 'Deft Strikes',
          description: 'Attacks with Monk Weapons and unarmed attacks deal 1d4 Bludgeoning damage, unless their normal damage is higher.'
        },
        {
          name: 'Bonus Unarmed Attack',
          description: 'After making an attack with a Monk Weapon or while unarmed, you can make another unarmed attack as a Bonus Action.'
        }
      ],
      [ // Level 2
        {
          name: 'Patient Defence',
          description: 'Attack Rolls against you have Disadvantage, and you have Advantage on Dexterity Saving Throws.'
        },
        {
          name: 'Step of the Wind (Dash)',
          description: 'Double your movement speed, as a Bonus Action and using a Ki Point. Jump no longer requires a Bonus Action.'
        },
        {
          name: 'Step of the Wind (Disengage)',
          description: 'Move without triggering Opportunity Attacks, as a Bonus Action and using a Ki Point. Jump no longer requires a Bonus Action.'
        },
        {
          name: 'Unarmoured Movement',
          description: 'Your movement speed increases by 3m / 10 ft while you are not wearing armor or using a shield.'
        }
      ],
      [ // Level 3
        {
          name: 'Deflect Missiles',
          description: 'Use your reaction to reduce the damage from a ranged weapon attack by 1d10 + your Dexterity Modifier + your monk level. If the damage is reduced to 0, you use a Ki Point to Deflect the Missile.'
        },
        {
          name: 'Improved Deft Strikes',
          description: 'Attacks with Monk Weapons and unarmed attacks deal 1d6 Bludgeoning damage, unless their normal damage is higher.'
        }
      ],
      [ // Level 4
        {
          name: 'Slow Fall',
          description: 'When you fall, you can use your reaction to gain Resistance to Falling damage.'
        }
      ],
      [ // Level 5
        {
          name: 'Extra Attack',
          description: 'You can make an additional free attack after making an unarmed or weapon attack. If you gain the Extra Attack feature from more than one class, they don\'t add together.'
        },
        {
          name: 'Stunning Strike',
          description: 'Use a Ki Point to attempt to Stun an opponent while performing an unarmed or Monk Weapon attack. Stun effect can be negated with a Constitution Save.'
        }
      ],
      [ // Level 6
        {
          name: 'Ki-Empowered Strikes',
          description: 'Your unarmed attacks count as magical for the purpose of overcoming enemies\' Resistances and Immunity to non-magical damage.'
        },
        {
          name: 'Improved Unarmoured Movement',
          description: 'Your movement speed increases by 4.5 m / 15 ft while you are not wearing armour or using a shield.'
        }
      ],
      [ // Level 7
        {
          name: 'Evasion',
          description: 'Your agility lets you dodge out of the way of certain spells. When a spell or effect would deal half damage on a successful Dexterity Saving Throw, it deals no damage if you succeed, and only half damage if you fail.'
        },
        {
          name: 'Stillness of Mind',
          description: 'If you are Charmed or Frightened, you automatically cast Stillness of Mind to remove the condition.'
        }
      ],
      [ // Level 8

      ],
      [ // Level 9
        {
          name: 'Advanced Unarmoured Movement',
          description: 'Difficult Terrain doesn\'t slow you down and you can jump an additional 6 m / 20 ft while you are not wearing armour or using a shield.'
        },
        {
          name: 'Improved Deft Strikes',
          description: 'Attacks with Monk Weapons and unarmed attacks deal 1d8 Bludgeoning damage, unless their normal damage is higher.'
        }
      ],
      [ // Level 10
        {
          name: 'Purity of Body',
          description: 'You are Immune to Poison damage and can\'t be Poisoned or affected by disease thanks to the purifying ki flowing through your meridians.'
        },
        {
          name: 'Improved Unarmoured Movement',
          description: 'Your movement speed increases by 6 m / 20 ft while you are not wearing armour or using a shield.'
        }
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
    featLevels: [4, 8, 12],
    subclassLevel: 3,
    subclasses: MONK_SUBCLASSES
  },
  {
    name: 'Paladin',
    icon: 'assets/img/classes/150px-Paladin_Class_Icon.png',
    flavorText: 'Fuelled by the Oath you swore to uphold justice and righteousness, you are a beacon of hope in dark times.',
    description: 'Paladins are masters of a variety of weapons and armour. They channel the power of their sacred oath to heal the sick and injured and to smite their foes. Important Ability Scores for Paladins include Strength (or Dexterity), Charisma, and ',
    savingThrowProficiencies: [Ability.Wisdom, Ability.Charisma],
    skillProficiencies: {
      name: 'Skill Proficiency',
      description: 'Choose two class skills in which to gain profiency.',
      skillSelection: PALADIN_SKILLS,
      selectionCount: 2,
      selectSameAllowed: false
    } as Feature,
    equipmentProficiencies: [
      EquipmentProficiency.LightArmor, EquipmentProficiency.MediumArmor,
      EquipmentProficiency.Shields, EquipmentProficiency.SimpleWeapons,
      EquipmentProficiency.MartialWeapons
    ] as EquipmentProficiency[],
    initialEquipmentProficiencies: [
      EquipmentProficiency.HeavyArmor
    ] as EquipmentProficiency[],
    features: [
      [ // Level 1
        {
          name: 'Divine Sense',
          description: 'As a Bonus Action, tap into your spiritual awareness to gain Advantage on Attack Rolls against celestials, fiends, and undead.'
        },
        {
          name: 'Lay on Hands',
          description: 'As an action and using a Lay on Hands Charge, use your blessed touch to heal a creature or cure it of all diseases and poisons.'
        }
      ],
      [ // Level 2
        {
          name: 'Divine Smite',
          description: 'Your weapon emanates divine might as you strike, dealing an additional 2d8 Radiant damage. Deals an additional 1d8 Radiant damage to Fiends and Undead. Uses a Spell Slot, and deals an additional 1d8 Radiant damage for each Spell Slot Level above 1st.'
        },
        {
          name: 'Fighting Style',
          description: 'Choose 1 fighting style.',
          featureSelection: [
            {
              name: 'Defense',
              description: 'You gain a +1 bonus to Armour Class while wearing Armour.'
            },
            {
              name: 'Duelling',
              description: 'When you are wielding a melee weapon that is not Two-Handed or Versatile in one hand, and no weapon in the other, you deal an additional 2 damage with that weapon.'
            },
            {
              name: 'Great Weapon Fighting',
              description: 'When you roll a 1 or 2 on a damage die for an attack with a Two-Handed melee weapon, that die is rerolled once.'
            },
            {
              name: 'Protection',
              description: 'When you have a Shield, impose Disadvantage on an enemy who attacks one of your allies when you are within 1.5 m / 5 ft. You must be able to see the enemy. This is a reaction.'
            }
          ] as Feature[],
          selectionCount: 1,
          selectSameAllowed: false
        } as Feature
      ],
      [ // Level 3
        {
          name: 'Divine Health',
          description: 'The divine magic flowing within you prevents disease from affecting you.'
        }
      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Extra Attack',
          description: 'You can make an additional free attack after making an unarmed or weapon attack. If you gain the Extra Attack feature from more than one class, they don\'t add together.'
        }
      ],
      [ // Level 6
        {
          name: 'Aura of Protection',
          description: 'You and nearby allies gain a bonus to Saving Throws equal to your Charisma modifier.'
        }
      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10
        {
          name: 'Aura of Courage',
          description: 'You and any nearby allies can\'t be Frightened. The aura disappears if you fall Unconscious.'
        }
      ],
      [ // Level 11
        {
          name: 'Improved Divine Smite',
          description: 'Whenever you hit a creature with a melee weapon, the creature takes an extra 1d8 Radiant damage.'
        }
      ],
      [ // Level 12

      ]
    ] as Feature[][],
    featLevels: [4, 8, 12],
    subclassLevel: 1,
    subclasses: PALADIN_SUBCLASSES
  },
  {
    name: 'Ranger',
    icon: 'assets/img/classes/Ranger_Class_Icon.png',
    flavorText: 'Rangers are unrivalled scouts and trackers, honing a deep connection with nature in order to hunt their favoured prey.',
    description: 'In tune with nature and their surroundings, Rangers utilize both their knowledge of the wilds and spellcasting to pursue their prey. Important Ability Scores for Rangers include Dexterity (or Strength), Constitution, and Wisdom.',
    savingThrowProficiencies: [Ability.Strength, Ability.Dexterity],
    skillProficiencies: {
      name: 'Skill Proficiency',
      description: 'Choose two class skills in which to gain profiency.',
      skillSelection: RANGER_SKILLS,
      selectionCount: 2,
      selectSameAllowed: false
    } as Feature,
    equipmentProficiencies: [
      EquipmentProficiency.LightArmor, EquipmentProficiency.MediumArmor,
      EquipmentProficiency.Shields, EquipmentProficiency.SimpleWeapons,
      EquipmentProficiency.MartialWeapons
    ] as EquipmentProficiency[],
    initialEquipmentProficiencies: [

    ] as EquipmentProficiency[],
    features: [
      [ // Level 1
        {
          name: 'Bonus Skill Proficiency',
          description: 'Choose one additional class skill in which to gain profiency.',
          skillSelection: RANGER_SKILLS,
          selectionCount: 1,
          selectSameAllowed: false
        },
        FAVOURED_ENEMY,
        NATURAL_EXPLORER
      ],
      [ // Level 2
        {
          name: 'Fighting Style',
          description: 'Choose 1 fighting style.',
          featureSelection: [
            {
              name: 'Archery',
              description: 'You gain a +2 bonus to Attack Rolls you make with ranged Weapons.'
            },
            {
              name: 'Defense',
              description: 'You gain a +1 bonus to Armour Class while wearing Armour.'
            },
            {
              name: 'Duelling',
              description: 'When you are wielding a melee weapon that is not Two-Handed or Versatile in one hand, and no weapon in the other, you deal an additional 2 damage with that weapon.'
            },
            {
              name: 'Two Weapon Fighting',
              description: 'When you make an offhand attack, you can add your Ability Score Modifier to the damage of the attack.'
            }
          ] as Feature[],
          selectionCount: 1,
          selectSameAllowed: false
        }
      ],
      [ // Level 3

      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Extra Attack',
          description: 'You can make an additional free attack after making an unarmed or weapon attack. If you gain the Extra Attack feature from more than one class, they don\'t add together.'
        }
      ],
      [ // Level 6
        FAVOURED_ENEMY, NATURAL_EXPLORER
      ],
      [ // Level 7

      ],
      [ // Level 8
        {
          name: 'Land\'s Stride',
          description: 'Difficult Terrain no longer slows you down.'
        }
      ],
      [ // Level 9

      ],
      [ // Level 10
        {
          name: 'Hide in Plain Sight',
          description: 'Become Invisible and gain +10 bonus to Stealth Checks as long as you stand still.'
        },
        FAVOURED_ENEMY, NATURAL_EXPLORER
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
    featLevels: [4, 8, 12],
    subclassLevel: 3,
    subclasses: RANGER_SUBCLASSES
  },
  {
    name: 'Rogue',
    icon: 'assets/img/classes/Rogue_Class_Icon.png',
    flavorText: 'With stealth, skill, and uncanny reflexes, a rogue\'s versatility lets them get the upper hand in almost any situation.',
    description: 'Rogues are well versed in the art of stealth and rely on their resourcefulness to be in control of any challenging situation. Rogues use Dexterity as their primary ability score.',
    savingThrowProficiencies: [Ability.Dexterity, Ability.Intelligence],
    skillProficiencies: {
      name: 'Skill Proficiency',
      description: 'Choose two class skills in which to gain profiency.',
      skillSelection: ROGUE_SKILLS,
      selectionCount: 2,
      selectSameAllowed: false
    } as Feature,
    equipmentProficiencies: [
      EquipmentProficiency.LightArmor
    ] as EquipmentProficiency[],
    initialEquipmentProficiencies: [
      EquipmentProficiency.SimpleWeapons, EquipmentProficiency.HandCrossbows,
      EquipmentProficiency.Longswords, EquipmentProficiency.Rapiers,
      EquipmentProficiency.Shortswords
    ] as EquipmentProficiency[],
    features: [
      [ // Level 1
        {
          name: 'Bonus Skill Proficiency',
          description: 'Choose one additional class skill in which to gain profiency.',
          skillSelection: ROGUE_SKILLS,
          selectionCount: 2,
          selectSameAllowed: false
        },
        {
          name: 'Skill Expertise',
          description: 'Gain Expertise in 2 Skills you are proficient in.',
          skillExpertiseSelection: true,
          selectionCount: 2,
          selectSameAllowed: false
        },
        {
          name: 'Sneak Attack',
          description: 'Deal an extra 1d6 damage to a foe you have Advantage against. You can also use Sneak Attack if you have an ally within 1.5m / 5ft of the target and you don\'t have Disadvantage.'
        }
      ],
      [ // Level 2
        {
          name: 'Cunning Action',
          description: 'You can use Dash, Disengage, and Hide as a Bonus Action.'
        }
      ],
      [ // Level 3

      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Uncanny Dodge',
          description: 'Use your lightning-quick reflexes to protect yourself. When an attack hits you, you only take half the usual damage.'
        }
      ],
      [ // Level 6
        {
          name: 'Skill Expertise',
          description: 'Gain Expertise in 2 Skills you are proficient in.',
          skillExpertiseSelection: true,
          selectionCount: 2,
          selectSameAllowed: false
        }
      ],
      [ // Level 7
        {
          name: 'Evasion',
          description: 'Your agility lets you dodge out of the way of certain spells. When a spell or effect would deal half damage on a successful Dexterity Saving Throw, it deals no damage if you succeed, and only half damage if you fail.'
        },
      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10

      ],
      [ // Level 11
        {
          name: 'Reliable Talent',
          description: 'When you make an Ability Check with a Skill you are Proficient with, the lowest result you can roll on the die is 10.'
        }
      ],
      [ // Level 12
        {

        }
      ]
    ] as Feature[][],
    featLevels: [4, 8, 10, 12],
    subclassLevel: 3,
    subclasses: ROGUE_SUBCLASSES
  },
  {
    name: 'Sorcerer',
    icon: 'assets/img/classes/150px-Sorcerer_Class_Icon.png',
    flavorText: 'Sorcerers are natural spellcasters, drawing on inherent magic from a gift or bloodline.',
    description: 'Sorcerers use their innate magical powers to fight enemies and aid allies in combat. Sorcerers use Charisma as their primary ability score.',
    savingThrowProficiencies: [Ability.Charisma, Ability.Constitution],
    skillProficiencies: {
      name: 'Skill Proficiency',
      description: 'Choose two class skills in which to gain profiency.',
      skillSelection: SORCERER_SKILLS,
      selectionCount: 2,
      selectSameAllowed: false
    } as Feature,
    equipmentProficiencies: [

    ] as EquipmentProficiency[],
    initialEquipmentProficiencies: [
      EquipmentProficiency.Daggers, EquipmentProficiency.Quarterstaves,
      EquipmentProficiency.LightCrossbows
    ] as EquipmentProficiency[],
    features: [
      [ // Level 1

      ],
      [ // Level 2
        {
          name: 'Metamagic',
          description: 'Choose 2 Metamagic abilities.',
          featureSelection: [
            {
              name: 'Careful Spell',
              description: 'Allies automatically succeed Saving Throws against spells that required them. Costs 1 Sorcery Point per spell.'
            },
            {
              name: 'Distant Spell',
              description: 'Increases the range of the spell by 50%. Spells with a range of 1.5m / 5ft are increased to 9m / 30ft. Costs 1 Sorcery Point per spell.'
            },
            {
              name: 'Extended Spell',
              description: 'Doubles the duration of Conditions, summons, and surfaces caused by spells. Costs 1 Sorcery Point per spell.'
            },
            {
              name: 'Twinned Spell',
              description: 'Spells that only target 1 creature can target an additional creature. Costs 1 Sorcery Point per Spell Slot level used. Cantrips also cost 1 Sorcery Point.'
            }
          ] as Feature[],
          selectionCount: 2,
          selectSameAllowed: false
        },
        {
          name: 'Create Spell Slot',
          description: 'Spend Sorcery Points to unlock a Spell Slot, as a Bonus Action.'
        },
        {
          name: 'Create Sorcery Points',
          description: 'Spend Spell Slots to gain Sorcery Points equal to the slot\'s level, as a Bonus Action.'
        }
      ],
      [ // Level 3
        {
          name: 'Metamagic',
          description: 'Choose 1 additional Metamagic ability.',
          featureSelection: [
            {
              name: 'Careful Spell',
              description: 'Allies automatically succeed Saving Throws against spells that required them. Costs 1 Sorcery Point per spell.'
            },
            {
              name: 'Distant Spell',
              description: 'Increases the range of the spell by 50%. Spells with a range of 1.5m / 5ft are increased to 9m / 30ft. Costs 1 Sorcery Point per spell.'
            },
            {
              name: 'Extended Spell',
              description: 'Doubles the duration of Conditions, summons, and surfaces caused by spells. Costs 1 Sorcery Point per spell.'
            },
            {
              name: 'Twinned Spell',
              description: 'Spells that only target 1 creature can target an additional creature. Costs 1 Sorcery Point per Spell Slot level used. Cantrips also cost 1 Sorcery Point.'
            },
            {
              name: 'Heightened Spell',
              description: 'Targets of spells that require Saving Throws have Disadvantage on their first Saving Throw. Costs 3 Sorcery Point per spell.'
            },
            {
              name: 'Quickend Spell',
              description: 'Spells that take an Action take a Bonus Action instead. Costs 3 Sorcery Point per spell.'
            },
            {
              name: 'Sutble Spell',
              description: 'You can cast spells while Silenced. Costs 1 Sorcery Point per spell.'
            },
          ] as Feature[],
          selectionCount: 1,
          selectSameAllowed: false
        }
      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Cantrip Damage Boost',
          description: 'Damage rolls for cantrips roll an additional die.'
        }
      ],
      [ // Level 6

      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10
        {
          name: 'Metamagic',
          description: 'Choose 1 additional Metamagic ability.',
          featureSelection: [
            {
              name: 'Careful Spell',
              description: 'Allies automatically succeed Saving Throws against spells that required them. Costs 1 Sorcery Point per spell.'
            },
            {
              name: 'Distant Spell',
              description: 'Increases the range of the spell by 50%. Spells with a range of 1.5m / 5ft are increased to 9m / 30ft. Costs 1 Sorcery Point per spell.'
            },
            {
              name: 'Extended Spell',
              description: 'Doubles the duration of Conditions, summons, and surfaces caused by spells. Costs 1 Sorcery Point per spell.'
            },
            {
              name: 'Twinned Spell',
              description: 'Spells that only target 1 creature can target an additional creature. Costs 1 Sorcery Point per Spell Slot level used. Cantrips also cost 1 Sorcery Point.'
            },
            {
              name: 'Heightened Spell',
              description: 'Targets of spells that require Saving Throws have Disadvantage on their first Saving Throw. Costs 3 Sorcery Point per spell.'
            },
            {
              name: 'Quickend Spell',
              description: 'Spells that take an Action take a Bonus Action instead. Costs 3 Sorcery Point per spell.'
            },
            {
              name: 'Sutble Spell',
              description: 'You can cast spells while Silenced. Costs 1 Sorcery Point per spell.'
            },
          ] as Feature[],
          selectionCount: 1,
          selectSameAllowed: false
        },
        {
          name: 'Cantrip Damage Boost',
          description: 'Damage rolls for cantrips roll an additional die.'
        }
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
    featLevels: [4, 8, 12],
    subclassLevel: 1,
    subclasses: SORCERER_SUBCLASSES
  },
  {
    name: 'Warlock',
    icon: 'assets/img/classes/Warlock_Class_Icon.png',
    flavorText: 'Bound by a pact to an all-powerful patron, Warlocks trade their loyalty for supernatural abilities and unique magic.',
    description: 'Warlocks channel their patron\'s pact magic to fight enemies and aid allies in combat. Warlocks use Charisma as their primary ability score.',
    savingThrowProficiencies: [Ability.Charisma, Ability.Wisdom],
    skillProficiencies: {
      name: 'Skill Proficiency',
      description: 'Choose two class skills in which to gain profiency.',
      skillSelection: WARLOCK_SKILLS,
      selectionCount: 2,
      selectSameAllowed: false
    } as Feature,
    equipmentProficiencies: [
      EquipmentProficiency.LightArmor, EquipmentProficiency.SimpleWeapons
    ] as EquipmentProficiency[],
    initialEquipmentProficiencies: [

    ] as EquipmentProficiency[],
    features: [
      [ // Level 1
        {
          name: 'Pact Magic',
          description: 'Warlock spell slots are equal to the highest level of Spells the Warlock has access to. Casting a lower level Spell will always "upcast" it to the highest level available. Warlocks have a very limited number of spell slots, but also regain those slots by taking a Short Rest or Long Rest.'
        }
      ],
      [ // Level 2
        {
          name: 'Eldritch Invocation',
          description: 'Choose 2 eldritch invocations.',
          featureSelection: ELDRITCH_INVOCATIONS_LV2,
          selectionCount: 2,
          selectSameAllowed: false
        }
      ],
      [ // Level 3
        {
          name: 'Pact Boon',
          description: 'Choose 1 Warlock pact boon.',
          featureSelection: [
            {
              name: 'Pact of the Chain',
              description: 'Gain the service of a familiar, a fey spirit that takes a form you choose. This can be an animal, imp, or quasit.'
              // todo: Add Find Familiar spells
            },
            {
              name: 'Pact of the Blade',
              description: 'You can Summon a pact weapon, or Bind the one you are wielding, making it magical. Pact weapons use the wielder\'s Spellcasting Ability Modifier instead of Strength or Dexterity.'
            },
            {
              name: 'Pact of the Tome',
              description: 'Your patron grants you a grimoire called \'The Book of Shadows\', which allows you to cast Guidance, Vicious Mockery, and Thorn Whip.'
              // todo: Add Guidance, Vicious Mockery, and Thorn Whip spells.
            }
          ],
          selectionCount: 1,
          selectSameAllowed: false
        }
      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Eldritch Invocation',
          description: 'Choose 1 eldritch invocation.',
          featureSelection: ELDRITCH_INVOCATIONS_LV2.concat(ELDRITCH_INVOCATIONS_LV5),
          selectionCount: 1,
          selectSameAllowed: false
        },
        {
          name: 'Deepened Pact',
          description: 'Pleased with your service, your otherworldly patron added new boons to the terms of your pact. Pact of the Blade: Gain an Extra Attack with your pact weapon. Pact of the Chain: Your familiar gains an Extra Attack. Pact of the Tome: You can cast Animate Dead, Haste, and Call Lightning once per Long Rest.',
        },
        {
          name: 'Cantrip Damage Boost',
          description: 'Damage rolls for cantrips roll an additional die. Unlike other cantrips, Eldritch Blast instead fires an additional beam, allowing you to attack two targets once or the same target twice.'
        }
      ],
      [ // Level 6

      ],
      [ // Level 7
        {
          name: 'Eldritch Invocation',
          description: 'Choose 1 eldritch invocation.',
          featureSelection: ELDRITCH_INVOCATIONS_LV2.concat(ELDRITCH_INVOCATIONS_LV5).concat(ELDRITCH_INVOCATIONS_LV7),
          selectionCount: 1,
          selectSameAllowed: false
        }
      ],
      [ // Level 8

      ],
      [ // Level 9
        {
          name: 'Eldritch Invocation',
          description: 'Choose 1 eldritch invocation.',
          featureSelection: ELDRITCH_INVOCATIONS_LV2.concat(ELDRITCH_INVOCATIONS_LV5).concat(ELDRITCH_INVOCATIONS_LV7).concat(ELDRITCH_INVOCATIONS_LV9),
          selectionCount: 1,
          selectSameAllowed: false
        }
      ],
      [ // Level 10
        {
          name: 'Cantrip Damage Boost',
          description: 'Damage rolls for cantrips roll an additional die. Unlike other cantrips, Eldritch Blast instead fires an additional beam, allowing you to attack two targets once or the same target twice.'
        }
      ],
      [ // Level 11
        {
          name: 'Mystic Arcanum',
          description: 'Your patron bestows upon you a magical secret called an arcanum. Choose one 6th Level spell from the Warlock Spell list. You can cast your arcanum spell once without expending a spell slot. You must finish a long rest before you can do so again.'
          // todo: Add spell choices
        }
      ],
      [ // Level 12
        {
          name: 'Eldritch Invocation',
          description: 'Choose 1 eldritch invocation.',
          featureSelection: ELDRITCH_INVOCATIONS_LV2.concat(ELDRITCH_INVOCATIONS_LV5).concat(ELDRITCH_INVOCATIONS_LV7).concat(ELDRITCH_INVOCATIONS_LV9).concat(ELDRITCH_INVOCATIONS_LV12),
          selectionCount: 1,
          selectSameAllowed: false
        }
      ]
    ] as Feature[][],
    featLevels: [4, 8, 12],
    subclassLevel: 1,
    subclasses: WARLOCK_SUBCLASSES
  },
  {
    name: 'Wizard',
    icon: 'assets/img/classes/Wizard_Class_Icon.png',
    flavorText: 'Wizards master the arcane by specialising in individual schools of magic, combining ancient spells with modern research.',
    description: 'Wizards channel magic through their extensive knowledge of the arcane to fight enemies and aid allies in combat. Wizards use Intelligence as their primary ability score.',
    savingThrowProficiencies: [Ability.Intelligence, Ability.Wisdom],
    skillProficiencies: {
      name: 'Skill Proficiency',
      description: 'Choose two class skills in which to gain profiency.',
      skillSelection: WIZARD_SKILLS,
      selectionCount: 2,
      selectSameAllowed: false
    } as Feature,
    equipmentProficiencies: [

    ] as EquipmentProficiency[],
    initialEquipmentProficiencies: [
      EquipmentProficiency.Daggers, EquipmentProficiency.Quarterstaves,
      EquipmentProficiency.LightCrossbows
    ] as EquipmentProficiency[],
    features: [
      [ // Level 1
        {
          name: 'Arcane Recovery',
          description: 'Once per day out of combat, you can replenish expended Spell Slots.'
        }
      ],
      [ // Level 2

      ],
      [ // Level 3

      ],
      [ // Level 4

      ],
      [ // Level 5
        {
          name: 'Cantrip Damage Boost',
          description: 'Damage rolls for cantrips roll an additional die.'
        }
      ],
      [ // Level 6

      ],
      [ // Level 7

      ],
      [ // Level 8

      ],
      [ // Level 9

      ],
      [ // Level 10
        {
          name: 'Cantrip Damage Boost',
          description: 'Damage rolls for cantrips roll an additional die.'
        }
      ],
      [ // Level 11

      ],
      [ // Level 12

      ]
    ] as Feature[][],
    featLevels: [4, 8, 12],
    subclassLevel: 2,
    subclasses: WIZARD_SUBCLASSES
  },
];
