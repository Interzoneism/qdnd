import { Feature } from './features'
import { Ability, ABILITIES } from './abilities'
import { Skill } from './skills'
import { EquipmentProficiency } from './equipment'
import { BATTLE_MANOEUVERS } from './classes'

export interface Feat extends Feature {
  icon: string;
  canReselect: boolean;
  requirements?: Requirement
}

export interface Requirement {
  equipmentProficiencies?: EquipmentProficiency[];
}


export const FEATS = [
  {
    name: 'Ability Improvement',
    icon: 'assets/img/feats/40px-Passive_Feature_Generic_Icon.png',
    description: 'Increase one Ability Score by 2, or two Ability Scores by 1. You can\'t increase an Ability Score above 20 using this feature.',
    abilitySelection: ABILITIES,
    selectionCount: 2,
    selectSameAllowed: true,
    canReselect: true
  },
  {
    name: 'Actor',
    icon: 'assets/img/feats/40px-Passive_Feature_Generic_Icon.png',
    description: 'Your Charisma increases by 1, to a maximum of 20. Your gain Proficiency in Deception and Performance and your proficiency bonus is doubled for these skills.',
    effect: {
      skillProficiencies: [Skill.Performance, Skill.Deception],
      abilityScoreIncrease: new Map<Ability, number>([[Ability.Charisma, 1]])
    },
    canReselect: false
  },
  {
    name: 'Alert',
    icon: 'assets/img/feats/40px-Alert_Icon.png',
    description: 'You gain a +5 bonus to Initiative and can\'t be Surprised.',
    canReselect: false
  },
  {
    name: 'Athlete',
    icon: 'assets/img/feats/40px-Athlete_Icon.png',
    description: 'Your Strength or Dexterity increases by 1, to a maximum of 20. When you are Prone, standing up uses significantly less movement. Your Jump distance also increases by 50%.',
    abilitySelection: [Ability.Strength, Ability.Dexterity],
    selectionCount: 1,
    selectSameAllowed: true,
    canReselect: false
  },
  {
    name: 'Charger',
    icon: 'assets/img/feats/40px-Charger_Icon.png',
    description: 'You gain the Charge weapon attach, which uses both an action and bonus action to charge up to 9m and make a melee attack that deals +5 damage without provoking an Attack of Opportunity. You also gain the Shove attack, allowing you to charge forward and shove the first enemy in your way.',
    canReselect: false
  },
  {
    name: 'Crossbow Expert',
    icon: 'assets/img/feats/40px-PassiveFeature_CrossbowExpert_BonusShot.png',
    description: 'When you make crossbow attacks within melee range, the Attack Rolls do not have Disadvantage. Your Piercing Shot also inflicts Gaping Wounds (Condition) for twice as long.',
    canReselect: false
  },
  {
    name: 'Defensive Duelist',
    icon: 'assets/img/feats/40px-Defensive_Duellist_Icon.png',
    description: 'When you are attacked with a melee attack while wielding a Finesse Weapon you are Proficient in, you can use a Reaction to increase your Armour Class by your Proficiency Bonus, possibly causing the attack to miss.',
    canReselect: false
  },
  {
    name: 'Duel Wielder',
    icon: 'assets/img/feats/40px-Dual_Wielder_Icon.png',
    description: 'You can use Two-Weapon Fighting even if your weapons aren\'t Light, and you gain a +1 bonus to Armour Class while wielding a melee weapon in each hand. You cannot dual-wield Heavy weapons.',
    canReselect: false
  },
  {
    name: 'Dungeon Delver',
    icon: 'assets/img/feats/40px-PassiveFeature_DungeonDelver_Perception.png',
    description: 'You gain Advantage on Perception checks made to detect hidden objects and on Saving Throws made to avoid or resist traps. You gain Resistance to the damage dealt by traps.',
    canReselect: false
  },
  {
    name: 'Durable',
    icon: 'assets/img/feats/40px-PassiveFeature_Durable.png',
    description: 'Constitution increases by 1, to a maximum of 20. Regain full Hit Points every time you take a Short Rest.',
    effect: {
      abilityScoreIncrease: new Map<Ability, number>([[Ability.Constitution, 1]])
    },
    canReselect: false
  },
  {
    name: 'Elemental Adept',
    icon: 'assets/img/feats/40px-PassiveFeature_ElementalAdept_Fire.png',
    description: 'Spells you cast ignore Resistance to a damage type of your choice. In addition, when you deal that type of damage with a Spell, you cannot roll a 1.',
    featureSelection: [
      {
        name: 'Acid Damage',
        description: 'Your spells ignore Acid Resistance.'
      },
      {
        name: 'Cold Damage',
        description: 'Your spells ignore Cold Resistance.'
      },
      {
        name: 'Lightning Damage',
        description: 'Your spells ignore Lightning Resistance.'
      },
      {
        name: 'Fire Damage',
        description: 'Your spells ignore Fire Resistance.'
      },
      {
        name: 'Thunder Damage',
        description: 'Your spells ignore Thunder Resistance.'
      }
    ],
    canReselect: true,
    selectSameAllowed: false,
    selectionCount: 1
  },
  {
    name: 'Great Weapon Master',
    icon: 'assets/img/feats/40px-Great_Weapon_Master_Icon.png',
    description: 'When an attack with a melee weapon lands a Critical Hit or kills a creature, you can make another melee weapon attack as a Bonus Action that turn. When attacking with a Two-Handed or Versatile melee weapon (in both hands) that you are Proficient with, Attack Rolls take a -5 penalty, but their damage increases by 10.',
    canReselect: false
  },
  {
    name: 'Heavily Armoured',
    icon: 'assets/img/feats/40px-Heavily_Armoured_Icon.png',
    description: 'Increase your Strength by 1, to a maximum of 20. Gain Proficiency with Heavy Armour.',
    effect: {
      equipmentProficiencies: [EquipmentProficiency.HeavyArmor],
      abilityScoreIncrease: new Map<Ability, number>([[Ability.Strength, 1]])
    },
    requirements: {
      equipmentProficiencies: [EquipmentProficiency.MediumArmor]
    },
    canReselect: false
  },
  {
    name: 'Heavy Armour Master',
    icon: 'assets/img/feats/40px-PassiveFeature_HeavyArmorMaster.png',
    description: 'Increase your Strength by 1, to a maximum of 20. Incoming damage from non-magical attacks also decreases by 3 while you\'re wearing heavy armour.',
    effect: {
      abilityScoreIncrease: new Map<Ability, number>([[Ability.Strength, 1]])
    },
    requirements: {
      equipmentProficiencies: [EquipmentProficiency.HeavyArmor]
    },
    canReselect: false
  },
  {
    name: 'Lightly Armoured',
    icon: 'assets/img/feats/40px-Lightly_Armoured_Icon.png',
    description: 'Increase your Strength or Dexterity by 1, to a maximum of 20. You gain Proficiency with Light Armour.',
    abilitySelection: [Ability.Strength, Ability.Dexterity],
    effect: {
      equipmentProficiencies: [EquipmentProficiency.LightArmor]
    },
    selectionCount: 1,
    selectSameAllowed: true,
    canReselect: false
  },
  {
    name: 'Lucky',
    icon: 'assets/img/feats/40px-Lucky_Icon.png',
    description: 'You gain 3 Luck Points. These can be used to gain Advantage on Attack Rolls, Ability Checks, Saving Throws, or to make an enemy reroll their Attack Roll.',
    canReselect: false
  },
  {
    name: 'Mage Slayer',
    icon: 'assets/img/feats/40px-Mage_Slayer_Icon.png',
    description: 'When a creature casts a Spell within melee range of you, you have Advantage on any Saving Throws against it, and you can use a Reaction to immediately make an attack against the caster. Enemies you hit have Disadvantage on Concentration Saving Throws.',
    canReselect: false
  },
  {
    name: 'Magic Initiate (Bard)',
    icon: 'assets/img/feats/40px-Magic_Initiate_Bard_Icon.png',
    description: 'Learn 2 Cantrips and a 1st-level Spell from the Bard spell list. You can cast the 1st-level Spell once per Long Rest. Your Spellcasting Ability for all three spells is Charisma.',
    canReselect: true
    // todo: add cantrip and spell choices.
  },
  {
    name: 'Magic Initiate (Cleric)',
    icon: 'assets/img/feats/40px-Magic_Initiate_Cleric_Icon.png',
    description: 'Learn 2 Cantrips and a 1st-level Spell from the Cleric spell list. You can cast the 1st-level Spell once per Long Rest. Your Spellcasting Ability for all three spells is Wisdom.',
    canReselect: true
    // todo: add cantrip and spell choices.
  },
  {
    name: 'Magic Initiate (Druid)',
    icon: 'assets/img/feats/40px-Magic_Initiate_Druid_Icon.png',
    description: 'Learn 2 Cantrips and a 1st-level Spell from the Druid spell list. You can cast the 1st-level Spell once per Long Rest. Your Spellcasting Ability for all three spells is Wisdom.',
    canReselect: true
    // todo: add cantrip and spell choices.
  },
  {
    name: 'Magic Initiate (Sorcerer)',
    icon: 'assets/img/feats/40px-Magic_Initiate_Sorcerer_Icon.png',
    description: 'Learn 2 Cantrips and a 1st-level Spell from the Sorcerer spell list. You can cast the 1st-level Spell once per Long Rest. Your Spellcasting Ability for all three spells is Charisma.',
    canReselect: true
    // todo: add cantrip and spell choices.
  },
  {
    name: 'Magic Initiate (Warlock)',
    icon: 'assets/img/feats/40px-Magic_Initiate_Warlock_Icon.png',
    description: 'Learn 2 Cantrips and a 1st-level Spell from the Warlock spell list. You can cast the 1st-level Spell once per Long Rest. Your Spellcasting Ability for all three spells is Charisma.',
    canReselect: true
    // todo: add cantrip and spell choices.
  },
  {
    name: 'Magic Initiate (Wizard)',
    icon: 'assets/img/feats/40px-Magic_Initiate_Wizard_Icon.png',
    description: 'Learn 2 Cantrips and a 1st-level Spell from the Wizard spell list. You can cast the 1st-level Spell once per Long Rest. Your Spellcasting Ability for all three spells is Intelligence.',
    canReselect: true
    // todo: add cantrip and spell choices.
  },
  {
    name: 'Martial Adept',
    icon: 'assets/img/feats/40px-Martial_Adept_Icon.png',
    description: 'Learn two manoeuvres from the Battle Master archetype and receive 1 (additional) Superiority Die to fuel them. You regain expended Superiority Dice after a Short or Long Rest.',
    featureSelection: BATTLE_MANOEUVERS,
    selectionCount: 2,
    selectSameAllowed: false
    // todo: now you can select the same manoeuvers also as part of fighter class
  },
  {
    name: 'Medium Armour Master',
    icon: 'assets/img/feats/40px-PassiveFeature_MediumArmorMaster.png',
    description: 'When you wear medium armour, it doesn\'t impose Disadvantage on Stealth checks. The bonus to Armour Class you can gain from your Dexterity modifier also becomes +3 instead of +2.',
    requirements: {
      equipmentProficiencies: [EquipmentProficiency.MediumArmor]
    },
    canReselect: false
  },
  {
    name: 'Mobile',
    icon: 'assets/img/feats/40px-Mobile_Feat_Icon.png',
    description: 'Your Movement Speed increases by Range Icon.png 3 m / 10 ft. When you use the Dash action, difficult terrain doesn\'t slow you down. If you move after making a melee attack, you don\'t provoke an Opportunity Attack from that target.',
    canReselect: false
  },
  {
    name: 'Moderately Armoured',
    icon: 'assets/img/feats/40px-Moderately_Armoured_Icon.png',
    description: 'Increase your Strength or Dexterity by 1, to a maximum of 20. Gain Proficiency with Medium Armour and Shields.',
    requirements: {
      equipmentProficiencies: [EquipmentProficiency.LightArmor]
    },
    abilitySelection: [Ability.Strength, Ability.Dexterity],
    effect: {
      equipmentProficiencies: [EquipmentProficiency.MediumArmor, EquipmentProficiency.Shields]
    },
    selectionCount: 1,
    selectSameAllowed: true,
    canReselect: false
  },
  {
    name: 'Performer',
    icon: 'assets/img/feats/40px-Passive_Feature_Generic_Icon.png',
    description: 'Increase your Charisma by 1, to a maximum of 20. Gain Musical Instrument Proficiency.',
    effect: {
      abilityScoreIncrease: new Map<Ability, number>([[Ability.Charisma, 1]])
    },
    canReselect: false
  },
  {
    name: 'Polearm Master',
    icon: 'assets/img/feats/40px-PassiveFeature_PolearmMaster_BonusAttack.png',
    description: 'When attacking with a Glaive, Halberd, Quarterstaff, or Spear, you can use a Bonus Action to attack with the butt of your weapon. You can also make an Opportunity Attack when a target comes within range.',
    canReselect: false
  },
  {
    name: 'Resilient',
    icon: 'assets/img/feats/40px-PassiveFeature_Resilient.png',
    description: 'You increase an Ability by 1, to a maximum of 20. Gain Proficiency in that ability\'s Saving Throws.',
    abilitySelection: ABILITIES,
    selectionCount: 1,
    selectSameAllowed: true,
    canReselect: true
    // todo should not be able to select the same score twice.
  },
  {
    name: 'Ritual Caster',
    icon: 'assets/img/feats/40px-PassiveFeature_RitualCaster_MemorizeSpells.png',
    description: 'You learn two Ritual Spells of your choice.',
    canReselect: false
    //TODO: add ritual spells
  },
  {
    name: 'Savage Attacker',
    icon: 'assets/img/feats/40px-PassiveFeature_SavageAttacker.png',
    description: 'When making melee weapon attacks, you roll your damage dice twice and use the highest result.',
    canReselect: false
  },
  {
    name: 'Sentinel',
    icon: 'assets/img/feats/40px-Sentinel_Icon.png',
    description: 'When an enemy in melee range attacks an ally, you can use a Reaction to make a weapon attack against that enemy. Target ally must not have the Sentinel feat. You gain Advantage on Opportunity Attacks, and when you hit a creature with an opportunity attack, it can no longer move for the rest of its turn.',
    canReselect: false
  },
  {
    name: 'Sharpshooter',
    icon: 'assets/img/feats/40px-Sharpshooter_Icon.png',
    description: 'Your ranged weapon attacks are not penalized for High Ground Rules. Ranged weapon attacks with weapons you are Proficient with have a -5 penalty to their Attack Roll, but deal an additional 10 damage.',
    canReselect: false
  },
  {
    name: 'Shield Master',
    icon: 'assets/img/feats/40px-Shield_Master_Icon.png',
    description: 'Gain a +2 bonus to Dexterity Saving Throws when wielding a Shield. If a Spell forces you to make a Dexterity Saving Throw, you can use your Reaction to shield yourself and diminish the effect\'s damage. On a failed save, you only take half damage.',
    requirements: {
      equipmentProficiencies: [EquipmentProficiency.Shields]
    },
    canReselect: false
  },
  {
    name: 'Skilled',
    icon: 'assets/img/feats/40px-PassiveFeature_Skilled.png',
    description: 'You gain Proficiency in 3 Skills of your choice.',
    skillSelection: Object.values(Skill),
    selectionCount: 3,
    selectSameAllowed: false,
    canReselect: true
  },
  {
    name: 'Spell Sniper',
    icon: 'assets/img/feats/40px-PassiveFeature_SpellSniper_Snipe.png',
    description: 'You learn a cantrip, and the number you need to roll a Critical Hit while attacking with a Spell is reduced by 1. This effect can stack. Applies to targeted projectile spells with a range greater than 1.5 m / 5 ft and no area of effect.',
    canReselect: false
    // TODO add cantrip spell choice
  },
  {
    name: 'Tavern Brawler',
    icon: 'assets/img/feats/40px-PassiveFeature_TavernBrawler.png',
    description: 'Increase your Strength or Constitution by 1, to a maximum of 20. When you make an unarmed attack, use an improvised weapon, or throw something, your strength modifier is added twice to the damage and Attack Rolls.',
    abilitySelection: [Ability.Strength, Ability.Constitution],
    selectionCount: 1,
    selectSameAllowed: true,
    canReselect: false
  },
  {
    name: 'Tough',
    icon: 'assets/img/feats/40px-Tough_Icon.png',
    description: 'Hit Point maximum increased by 2 for each level.',
    canReselect: false
  },
  {
    name: 'War Caster',
    icon: 'assets/img/feats/40px-War_Caster_Icon.png',
    description: 'You gain Advantage on Saving Throws to maintain Concentration Concentration on a Spell. You can also use a Reaction to cast Shocking Grasp at a target moving out of melee range.',
    canReselect: false
  },
  {
    name: 'Weapon Master',
    icon: 'assets/img/feats/40px-PassiveFeature_WeaponMaster.png',
    description: 'Increase your Strength or Dexterity by 1, to a maximum of 20. Gain Proficiency with four Weapon types of your choice.',
    abilitySelection: [Ability.Strength, Ability.Dexterity],
    selectionCount: 1,
    canReselect: true
    // todo: allow choosing weapon proficiencies as well...
  }
] as Feat[];
