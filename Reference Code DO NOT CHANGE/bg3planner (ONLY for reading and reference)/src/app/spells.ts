export interface Spell {
  name: string;
  icon: string;
  school: SpellSchool;
  resources: SpellResource[];
  range: number;
  radius: number;
  concentration: boolean;
  roll: boolean;
  save?: string;
  duration: number;
  description: string;
}

export enum SpellSchool {
  Abjuration = 'Abjuration',
  Conjuration = 'Conjuration',
  Divination = 'Divination',
  Enchantment = 'Enchantment',
  Evocation = 'Evocation',
  Illusion = 'Illusion',
  Necromancy = 'Necromancy',
  Transmutation = 'Transmutation'
}

export enum SpellResource {
  Action = 'Action', BonusAction = 'Bonus Action', Reaction = 'Reaction'
}

export const DURATION_LONG_REST = -2;
export const DURATION_SHORT_REST = -1;

export const SPELL_ACID_SPLASH = {
  name: 'Acid Splash',
  icon: 'assets/img/spells/300px-Acid_Splash_Icon.png',
  school: SpellSchool.Conjuration,
  resources: [SpellResource.Action],
  range: 18,
  radius: 2,
  concentration: false,
  roll: false,
  duration: 0,
  description: 'Hurl a bubble of acid that deals 1d6 Acid damage to each creature it hits in a small area.'
} as Spell;

export const SPELL_BLADE_WARD = {
  name: 'Blade Ward',
  icon: 'assets/img/spells/300px-Blade_Ward_Icon.png',
  school: SpellSchool.Abjuration,
  resources: [SpellResource.Action],
  range: 0,
  radius: 0,
  concentration: false,
  roll: false,
  duration: 2,
  description: 'The caster gains resistance against Bludgeoning, Piercing, and Slashing damage dealt by weapon attacks for two turns. Damage of these types is halved.'
} as Spell;

export const SPELL_BONE_CHILL = {
  name: 'Bone Chill',
  icon: 'assets/img/spells/300px-Bone_Chill_Icon.png',
  school: SpellSchool.Necromancy,
  resources: [SpellResource.Action],
  range: 18,
  radius: 0,
  concentration: false,
  roll: true,
  duration: 1,
  description: 'Assail a creature with the chill of the grave. It takes 1d8 Necrotic damage and cannot regain Hit Points. Undead creatures also get Disadvantage on Attack Rolls.'
} as Spell;

export const SPELL_DANCING_LIGHTS = {
  name: 'Dancing Lights',
  icon: 'assets/img/spells/300px-Dancing_Lights_Icon.png',
  school: SpellSchool.Evocation,
  resources: [SpellResource.Action],
  range: 18,
  radius: 0,
  concentration: true,
  roll: false,
  duration: 10,
  description: 'Create a wisp of light that illuminates a 12 m / 40 ft radius.'
} as Spell;

export const SPELL_FIRE_BOLT = {
  name: 'Fire Bolt',
  icon: 'assets/img/spells/300px-Fire_Bolt_Icon.png',
  school: SpellSchool.Evocation,
  resources: [SpellResource.Action],
  range: 18,
  radius: 0,
  concentration: false,
  roll: true,
  duration: 0,
  description: 'Hurl a bolt of fire at an enemy as a ranged attack, dealing 1d10 Fire damage if it hits.'
} as Spell;

export const SPELL_FRIENDS = {
  name: 'Friends',
  icon: 'assets/img/spells/300px-Friends_Icon.png',
  school: SpellSchool.Enchantment,
  resources: [SpellResource.Action],
  range: 18,
  radius: 0,
  concentration: true,
  roll: false,
  duration: 10,
  description: 'Gain Advantage on Charisma Checks against a non-hostile creature. In higher difficulty modes, the target might accuse you of enchanting them.'
} as Spell;

export const SPELL_LIGHT = {
  name: 'Light',
  icon: 'assets/img/spells/300px-Light_Cantrip_Icon.png',
  school: SpellSchool.Evocation,
  resources: [SpellResource.Action],
  range: 1.5,
  radius: 0,
  concentration: false,
  roll: false,
  duration: DURATION_LONG_REST,
  description: 'Infuse an object with an aura of light. Only affects one target at a time.'
} as Spell;

export const SPELL_MAGE_HAND = {
  name: 'Mage Hand',
  icon: 'assets/img/spells/300px-Mage_Hand_Icon.png',
  school: SpellSchool.Conjuration,
  resources: [SpellResource.Action],
  range: 18,
  radius: 0,
  concentration: true,
  roll: false,
  duration: 10,
  description: 'Create a spectral hand that can manipulate and interact with objects.'
} as Spell;

export const SPELL_MINOR_ILLUSION = {
  name: 'Minor Illusion',
  icon: 'assets/img/spells/300px-Minor_Illusion_Icon.png',
  school: SpellSchool.Illusion,
  resources: [SpellResource.Action],
  range: 18,
  radius: 0,
  concentration: true,
  roll: false,
  duration: 10,
  description: 'Create an illusory image that distracts nearby creatures, compelling them to investigate.'
} as Spell;

export const SPELL_POISON_SPRAY = {
  name: 'Poison Spray',
  icon: 'assets/img/spells/300px-Poison_Spray_Icon.png',
  school: SpellSchool.Conjuration,
  resources: [SpellResource.Action],
  range: 3,
  radius: 0,
  concentration: false,
  roll: false,
  duration: 0,
  save: 'CON',
  description: 'Project a puff of noxious gas that deals 1d12 Poison damage.'
} as Spell;

export const SPELL_RAY_OF_FROST = {
  name: 'Ray of Frost',
  icon: 'assets/img/spells/300px-Ray_of_Frost_Icon.png',
  school: SpellSchool.Evocation,
  resources: [SpellResource.Action],
  range: 18,
  radius: 0,
  concentration: false,
  roll: true,
  duration: 0,
  description: 'Call forth a frigid beam of blue light. Deals 1d8 Cold damage and reduces the target\'s Movement Speed by 3 m / 10 ft.'
} as Spell;

export const SPELL_SHOCKING_GRASP = {
  name: 'Shocking Grasp',
  icon: 'assets/img/spells/300px-Shocking_Grasp_Icon.png',
  school: SpellSchool.Evocation,
  resources: [SpellResource.Action],
  range: 1.5,
  radius: 0,
  concentration: false,
  roll: true,
  duration: 0,
  description: 'Lightning springs from your hand. It deals 1d8 Lightning damage and prevents the target from taking Reactions. You have Advantage when using this spell on creatures wearing metal Armour.'
} as Spell;

export const SPELL_TRUE_STRIKE = {
  name: 'True Strike',
  icon: 'assets/img/spells/300px-True_Strike_Icon.png',
  school: SpellSchool.Divination,
  resources: [SpellResource.Action],
  range: 18,
  radius: 0,
  concentration: true,
  roll: false,
  duration: 2,
  description: 'Divine a character\'s defences to give you Advantage on your next Attack Roll against it.'
} as Spell;

export const SPELL_FAERIE_FIRE = {
  name: 'Faerie Fire',
  icon: 'assets/img/spells/300px-Faerie_Fire_Icon.png',
  school: SpellSchool.Evocation,
  resources: [SpellResource.Action],
  range: 18,
  radius: 0,
  concentration: true,
  roll: false,
  duration: 10,
  description: 'Encase multiple targets in colorful light. The targets turn visible, and Attack Rolls against the target have Advantage.'
} as Spell;

export const SPELL_DARKNESS = {
  name: 'Darkness',
  icon: 'assets/img/spells/300px-Darkness_Icon.png',
  school: SpellSchool.Evocation,
  resources: [SpellResource.Action],
  range: 18,
  radius: 5,
  concentration: true,
  roll: false,
  duration: -2,
  description: 'Create a cloud of magical darkness to Heavily Obscure and Blind creatures within. Creatures cannot make ranged attacks into or out of the darkness.'
} as Spell;

export const SPELL_ENLARGE= {
  name: 'Enlarge',
  icon: 'assets/img/spells/300px-Enlarge_Icon.png',
  school: SpellSchool.Transmutation,
  resources: [SpellResource.Action],
  range: 9,
  radius: 0,
  concentration: true,
  roll: false,
  duration: 10,
  save: 'CON',
  description: 'Make a creature larger. Its weapons deal an extra 1d4 damage. It has Advantage on Strength Checks and Saving Throws.'
} as Spell;

export const SPELL_INVISIBILITY = {
  name: 'Invisibility',
  icon: 'assets/img/spells/300px-Invisibility_Icon.png',
  school: SpellSchool.Illusion,
  resources: [SpellResource.Action],
  range: 1.5,
  radius: 0,
  concentration: true,
  roll: false,
  duration: 10,
  description: 'Touch a creature to turn it Invisible. Attacks against it have Disadvantage. It attacks with Advantage. The spell ends early if the creature attacks or casts a spell.'
} as Spell;

export const SPELL_SPEAK_WITH_ANIMALS = {
  name: 'Speak with Animals',
  icon: 'assets/img/spells/300px-Speak_With_Animals_Icon.png',
  school: SpellSchool.Divination,
  resources: [SpellResource.Action],
  range: 0,
  radius: 0,
  concentration: false,
  roll: false,
  duration: -2,
  description: 'Gain the ability to comprehend and verbally communicate with beasts.'
} as Spell;

export const SPELL_PRODUCE_FLAME = {
  name: 'Produce Flame',
  icon: 'assets/img/spells/300px-Produce_Flame_Icon.png',
  school: SpellSchool.Conjuration,
  resources: [SpellResource.Action],
  range: 0,
  radius: 0,
  concentration: false,
  roll: false,
  duration: -2,
  description: 'A flickering flame appears in your hand. It sheds bright light in a 9 m / 30 ft radius and deals 1d8 Fire damage when thrown.'
} as Spell;

export const SPELL_HELLISH_REBUKE = {
  name: 'Hellish Rebuke',
  icon: 'assets/img/spells/300px-Hellish_Rebuke_Icon.png',
  school: SpellSchool.Evocation,
  resources: [SpellResource.Reaction],
  range: 18,
  radius: 0,
  concentration: false,
  roll: false,
  duration: 0,
  save: 'DEX',
  description: 'The next time you take damage, you use your Reaction to surround your attacker in hellish flames that deal 2d10 Fire damage. On a successful save, the target still takes half damage.'
} as Spell;

export const SPELL_FLAME_BLADE = {
  name: 'Flame Blade',
  icon: 'assets/img/spells/300px-Flame_Blade_Icon.png',
  school: SpellSchool.Evocation,
  resources: [SpellResource.Reaction],
  range: 0,
  radius: 0,
  concentration: true,
  roll: false,
  duration: 10,
  description: 'Conjure a flaming scimitar in your hand that deals 3d6 Fire damage. It sheds a bright light in a 3m / 10ft radius and a dim light in a 6m / 20ft radius.'
} as Spell;

export const SPELL_THAUMATURGY = {
  name: 'Thaumaturgy',
  icon: 'assets/img/spells/300px-Thaumaturgy_Icon.png',
  school: SpellSchool.Transmutation,
  resources: [SpellResource.Action],
  range: 0,
  radius: 0,
  concentration: false,
  roll: false,
  duration: 10,
  description: 'Manifest a sign of supernatural power that grants you Advantage on Intimidation and Performance Skill Checks.'
} as Spell;

export const SPELL_SEARING_SMITE = {
  name: 'Searing Smite',
  icon: 'assets/img/spells/300px-Searing_Smite_Icon.png',
  school: SpellSchool.Evocation,
  resources: [SpellResource.Action, SpellResource.BonusAction],
  range: 0,
  radius: 0,
  concentration: true,
  roll: false,
  duration: 10,
  description: 'Your weapon flares with white-hot intensity. It deals an extra 1d6 Fire damage and marks the target with Searing Smite. A target with Searing Smite takes 1d6 Fire damage every turn, until it succeeds on a Constitution Saving Throw.'
} as Spell;

export const SPELL_BRANDING_SMITE = {
  name: 'Branding Smite',
  icon: 'assets/img/spells/300px-Branding_Smite_(Melee)_Icon.png',
  school: SpellSchool.Evocation,
  resources: [SpellResource.Action, SpellResource.BonusAction],
  range: 0,
  radius: 0,
  concentration: true,
  roll: false,
  duration: 10,
  description: 'Your weapon gleams with astral radiance as you strike and possibly marks your target with light, preventing it from turning invisible. It deals an extra 2d6 Radiant damage.'
} as Spell;

export const SPELL_BURNING_HANDS = {
  name: 'Burning Hands',
  icon: 'assets/img/spells/300px-Burning_Hands_Icon.png',
  school: SpellSchool.Evocation,
  resources: [SpellResource.Action],
  range: 0,
  radius: 0,
  concentration: false,
  roll: false,
  duration: 0,
  save: 'DEX',
  description: 'Fire shoots from your outstretched fingers. It ignites anything flammable and deals 3d6 Fire damage. On a successful save, targets still take half damage.'
} as Spell;

export const SPELL_ENHANCE_LEAP = {
  name: 'Enhance Leap',
  icon: 'assets/img/spells/300px-Jump_Spell_Icon.png',
  school: SpellSchool.Transmutation,
  resources: [SpellResource.Action],
  range: 1.5,
  radius: 0,
  concentration: false,
  roll: false,
  duration: 10,
  description: 'Triple a creature\'s Jumping distance.'
} as Spell;

export const SPELL_MISTY_STEP = {
  name: 'Misty Step',
  icon: 'assets/img/spells/Misty_Step_Icon.png',
  school: SpellSchool.Conjuration,
  resources: [SpellResource.BonusAction],
  range: 0,
  radius: 0,
  concentration: false,
  roll: false,
  duration: 0,
  description: 'Surrounded by silver mists, you teleport to an unoccupied space you can see within 18m / 60ft.'
} as Spell;


export const WIZARD_SPELLS = [
  [
    SPELL_ACID_SPLASH, SPELL_BLADE_WARD, SPELL_BONE_CHILL, SPELL_DANCING_LIGHTS,
    SPELL_FIRE_BOLT, SPELL_FRIENDS, SPELL_LIGHT, SPELL_MAGE_HAND, SPELL_MINOR_ILLUSION,
    SPELL_POISON_SPRAY, SPELL_RAY_OF_FROST, SPELL_SHOCKING_GRASP, SPELL_TRUE_STRIKE
  ]
] as Spell[][];
