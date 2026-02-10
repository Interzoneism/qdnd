export enum Ability {
  Strength = 'Strength',
  Dexterity = 'Dexterity',
  Constitution = 'Constitution',
  Intelligence = 'Intelligence',
  Wisdom = 'Wisdom',
  Charisma = 'Charisma'
}

export const MIN_ABILITY_SCORE = 8;
export const MAX_ABILITY_SCORE = 15;

export const ABILITIES = [Ability.Strength, Ability.Dexterity, Ability.Constitution, Ability.Intelligence, Ability.Wisdom, Ability.Charisma];

export interface IAbility {
  name: string;
  abbr: string;
  icon: string;
  description: string;
}

export const ABILITY_INFO: Map<Ability, IAbility> = new Map([
  [
    Ability.Strength,
    {
      name: 'Strength',
      abbr: 'STR',
      icon: 'assets/img/abilities/150px-Strength_Ability_Icon.png',
      description: 'Strength affects your Athletics skill and Attack Rolls so you become better at melee combat. It also determines how far you can jump, how much you can carry and how far you can shove (possibly based on Athletics).'
    } as IAbility
  ],
  [
    Ability.Dexterity,
    {
      name: 'Dexterity',
      abbr: 'DEX',
      icon: 'assets/img/abilities/150px-Dexterity_Ability_Icon.png',
      description: 'Dexterity affects your Acrobatics, Sleight of Hand, and Stealth skills, as well as your Attack Rolls for ranged weapons and Finesse weapons. It also affects your Initiative and Armour Class.'
    } as IAbility
  ],
  [
    Ability.Constitution,
    {
      name: 'Constitution',
      abbr: 'CON',
      icon: 'assets/img/abilities/150px-Constitution_Ability_Icon.png',
      description: 'Your Constitution modifier is added to the number of Hit Points gained per level (including negative values), and is applied retroactively.'
    } as IAbility
  ],
  [
    Ability.Intelligence,
    {
      name: 'Intelligence',
      abbr: 'INT',
      icon: 'assets/img/abilities/150px-Intelligence_Ability_Icon.png',
      description: 'Intelligence affects your Arcana, History, Investigation, Nature, and Religion skills and improves spellcasting for Wizards, Eldritch Knights and Arcane Tricksters (better chance to hit enemies with Spell Attack Rolls; increases the Difficulty Class of spells with Saving Throws).'
    } as IAbility
  ],
  [
    Ability.Wisdom,
    {
      name: 'Wisdom',
      abbr: 'WIS',
      icon: 'assets/img/abilities/150px-Wisdom_Ability_Icon.png',
      description: 'Wisdom affects your Animal Handling, Insight, Medicine, Perception, and Survival skills and improves spellcasting for Clerics, Druids and Rangers (better chance to hit enemies with Spell Attack Rolls; increases the Difficulty Class of spells with Saving Throws).'
    } as IAbility
  ],
  [
    Ability.Charisma,
    {
      name: 'Charisma',
      abbr: 'CHA',
      icon: 'assets/img/abilities/150px-Charisma_Ability_Icon.png',
      description: 'Charisma affects its related skills (Deception, Intimidation, Performance, Persuasion) and improves spellcasting for Bards, Paladins, Warlocks and Sorcerers (better chance to hit enemies with Spell Attack Rolls; increases the Difficulty Class of spells with Saving Throws).'
    } as IAbility
  ],
])
