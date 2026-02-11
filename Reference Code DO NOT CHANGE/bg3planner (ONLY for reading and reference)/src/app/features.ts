import { Skill } from './skills'
import { EquipmentProficiency } from './equipment'
import { Spell } from './spells'
import { Ability } from './abilities'

export interface Feature {
  name: string;
  description: string;
  effect?: Effect;
  skillSelection?: Skill[];
  spellSelection?: Spell[];
  featureSelection?: Feature[];
  abilitySelection?: Ability[];
  skillExpertiseSelection?: boolean;
  selectionCount?: number;
  selectSameAllowed?: boolean;
}

export interface Effect {
  skillProficiencies?: Skill[];
  equipmentProficiencies?: EquipmentProficiency[];
  spells?: Spell[];
  abilityScoreIncrease: Map<Ability, number>;
}
