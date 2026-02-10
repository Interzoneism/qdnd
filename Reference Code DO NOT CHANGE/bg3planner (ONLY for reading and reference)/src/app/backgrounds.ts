import { Skill } from './skills'

export interface Background {
  name: string;
  icon: string;
  description: string;
  proficiencies: Skill[];
  requirements?: string;
}

export const BACKGROUNDS = [
  {
    name: 'Acolyte',
    icon: 'assets/img/backgrounds/150px-Background_Acolyte_Icon.png',
    description: 'You have spent your life in service to a temple, learning sacred rites and providing sacrifices to the god or gods you worship. Serving the gods and discovering their sacred works will guide you to greatness.',
    proficiencies: [Skill.Insight, Skill.Religion]
  },
  {
    name: 'Charlatan',
    icon: 'assets/img/backgrounds/150px-Background_Charlatan_Icon.png',
    description: 'You’re an expert in manipulation, prone to exaggeration and more than happy to profit from it. Bending the truth and turning allies against each other will lead to greater success down the road.',
    proficiencies: [Skill.Deception, Skill.SleightOfHand]
  },
  {
    name: 'Criminal',
    icon: 'assets/img/backgrounds/150px-Background_Criminal_Icon.png',
    description: 'You have a history of breaking the law and survive by leveraging less-than-legal connections. Profiting from criminal enterprise will lead to greater opportunities in the future.',
    proficiencies: [Skill.Deception, Skill.Stealth]
  },
  {
    name: 'Entertainer',
    icon: 'assets/img/backgrounds/150px-Background_Entertainer_Icon.png',
    description: 'You live to sway and subvert your audience, engaging common crowds and high society alike. Preserving art and bringing joy to the hapless and downtrodden heightens your charismatic aura.',
    proficiencies: [Skill.Acrobatics, Skill.Performance]
  },
  {
    name: 'Folk Hero',
    icon: 'assets/img/backgrounds/150px-Background_Folk_Hero_Icon.png',
    description: 'You’re the champion of the common people, challenging tyrants and monsters to protect the helpless. Saving innocents in imminent danger will make your legend grow.',
    proficiencies: [Skill.AnimalHandling, Skill.Survival]
  },
  {
    name: 'Guild Artisan',
    icon: 'assets/img/backgrounds/150px-Background_Guild_Artisan_Icon.png',
    description: 'Your skill in a particular craft has earned you membership in a mercantile guild, offering privileges and protection while engaging in your art. Repairing and discovering rare crafts will bring new inspiration.',
    proficiencies: [Skill.Insight, Skill.Persuasion]
  },
  {
    name: 'Noble',
    icon: 'assets/img/backgrounds/150px-Background_Noble_Icon.png',
    description: 'You were raised in a family among the social elite, accustomed to power and privilege. Accumulating renown, power, and loyalty will raise your status.',
    proficiencies: [Skill.History, Skill.Persuasion]
  },
  {
    name: 'Outlander',
    icon: 'assets/img/backgrounds/150px-Background_Outlander_Icon.png',
    description: 'You grew up in the wilds, learning to survive far from the comforts of civilisation. Surviving unusual hazards of the wild will enhance your prowess and understanding.',
    proficiencies: [Skill.Athletics, Skill.Survival]
  },
  {
    name: 'Sage',
    icon: 'assets/img/backgrounds/150px-Background_Sage_Icon.png',
    description: 'You’re curious and well-read, with an unending thirst for knowledge. Learning about rare lore of the world will inspire you to put this knowledge to greater purpose.',
    proficiencies: [Skill.Arcana, Skill.History]
  },
  {
    name: 'Soldier',
    icon: 'assets/img/backgrounds/150px-Background_Soldier_Icon.png',
    description: 'You are trained in battlefield tactics and combat, having served in a militia, mercenary company, or officer corps. Show smart tactics and bravery on the battlefield to enhance your prowess.',
    proficiencies: [Skill.Athletics, Skill.Intimidation]
  },
  {
    name: 'Urchin',
    icon: 'assets/img/backgrounds/150px-Background_Urchin_Icon.png',
    description: 'After surviving a poor and bleak childhood, you know how to make the most out of very little. Using your street smarts bolsters your spirit for the journey ahead.',
    proficiencies: [Skill.SleightOfHand, Skill.Stealth]
  },
  {
    name: 'Haunted One',
    icon: 'assets/img/backgrounds/150px-Background_HauntedOne_Icon.png',
    description: 'A wicked moment, person, or thing that cannot be slain by sword or spell haunts your mind and flickers in your peripheral vision. You carry it wherever your adventure takes you - or perhaps it carries you.',
    proficiencies: [Skill.Medicine, Skill.Intimidation],
    requirements: 'Only available to \'The Dark Urge\' origin characters.'
  }
];
