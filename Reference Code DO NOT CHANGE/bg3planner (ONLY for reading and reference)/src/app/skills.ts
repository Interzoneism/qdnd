export interface ISkill {
  name: string;
  ability: string;
  description: string;
}

export enum Skill {
  Athletics = "Athletics",
  Acrobatics = "Acrobatics",
  SleightOfHand = "Sleight Of Hand",
  Stealth = "Stealth",
  Arcana = "Arcana",
  History = "History",
  Investigation = "Investigation",
  Nature = "Nature",
  Religion = "Religion",
  AnimalHandling = "Animal Handling",
  Insight = "Insight",
  Medicine = "Medicine",
  Perception = "Perception",
  Survival = "Survival",
  Deception = "Deception",
  Intimidation = "Intimidation",
  Performance = "Performance",
  Persuasion = "Persuasion"
}

export const SKILL_INFO: Map<Skill, ISkill> = new Map([
  [
    Skill.Athletics,
    {
      name: 'Athletics',
      ability: 'STR',
      description: 'Stay fit. Perform physical stunts. It helps you shove and resist against being shoved.'
    }
  ],
  [
    Skill.Acrobatics,
    {
      name: 'Acrobatics',
      ability: 'DEX',
      description: 'Stay fit. Perform physical stunts. It helps you shove and resist against being shoved.'
    }
  ],
  [
    Skill.SleightOfHand,
    {
      name: 'Sleight Of Hand',
      ability: 'DEX',
      description: 'Wield nimble fingers. Steal stuff. It helps you pick locks, pick pockets, and disarm traps.'
    }
  ],
  [
    Skill.Stealth,
    {
      name: 'Stealth',
      ability: 'DEX',
      description: 'Stay out of sight. Melt into the shadows. It helps you with staying hidden from enemies or other NPCs.'
    }
  ],
  [
    Skill.Arcana,
    {
      name: 'Arcana',
      ability: 'INT',
      description: 'Recognise magic. Interact with enchanted items.'
    }
  ],
  [
    Skill.History,
    {
      name: 'History',
      ability: 'INT',
      description: 'Remember the past -- of the world and its people.'
    }
  ],
  [
    Skill.Investigation,
    {
      name: 'Investigation',
      ability: 'INT',
      description: 'Analyse clues. Solve mysteries.',
      effects: 'It helps you shove and resist against being shoved.'
    }
  ],
  [
    Skill.Nature,
    {
      name: 'Nature',
      ability: 'INT',
      description: 'Recognise plants and animals. Hug trees.'
    }
  ],
  [
    Skill.Religion,
    {
      name: 'Religion',
      ability: 'INT',
      description: 'Recognise deties. Understand holy rites.'
    }
  ],
  [
    Skill.AnimalHandling,
    {
      name: 'Animal Handling',
      ability: 'WIS',
      description: 'Influence animals. Pet all of the dogs.'
    }
  ],
  [
    Skill.Insight,
    {
      name: 'Insight',
      ability: 'WIS',
      description: 'Read people and situations. Detect lies.'
    }
  ],
  [
    Skill.Medicine,
    {
      name: 'Medicine',
      ability: 'WIS',
      description: 'Recognise symptoms. Diagnose diseases.'
    }
  ],
  [
    Skill.Perception,
    {
      name: 'Perception',
      ability: 'WIS',
      description: 'Observe your environment. Spot hidden details.'
    }
  ],
  [
    Skill.Survival,
    {
      name: 'Survival',
      ability: 'WIS',
      description: 'Stay fit. Perform physical stunts.',
      effects: 'It helps you shove and resist against being shoved.'
    }
  ],
  [
    Skill.Deception,
    {
      name: 'Deception',
      ability: 'CHA',
      description: 'Lie and cheat. Manipulate the truth.'
    }
  ],
  [
    Skill.Intimidation,
    {
      name: 'Intimidation',
      ability: 'CHA',
      description: 'Be a bully. Threaten and induce fear.'
    }
  ],
  [
    Skill.Performance,
    {
      name: 'Performance',
      ability: 'CHA',
      description: 'Entertain audiences. Command the stage.'
    }
  ],
  [
    Skill.Persuasion,
    {
      name: 'Persuasion',
      ability: 'CHA',
      description: 'Turn on the charm. Coax and cajole.'
    }
  ]
])
