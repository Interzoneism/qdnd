import { Race } from './races'
import { Background } from './backgrounds'
import { Skill } from './skills'
import { Class } from './classes'
import { Feature } from './features'
import { Ability } from './abilities'
import { Feat } from './feats'

export const MAX_LEVEL = 12;

export interface Character {
  name: string;
  race: Race;
  background: Background;
  abilityScores: Map<Ability, number>;
  majorAbility: Ability;
  minorAbility: Ability;
  classes: Class[];
  selectedClassFeatureOptions: Map<Class, Array<Map<Feature, Array<number>>>>;
  selectedRaceFeatureOptions: Map<Race, Map<Feature, Array<number>>>;
  selectedSubclasses: Map<Class, number>;
  selectedFeats: Map<Class, Map<number, Feat>>;
}
