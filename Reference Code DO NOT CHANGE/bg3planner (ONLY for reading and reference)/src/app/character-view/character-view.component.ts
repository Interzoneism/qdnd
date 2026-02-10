import { Component, Input } from '@angular/core';
import { Character } from '../character'
import { ABILITIES, ABILITY_INFO, Ability } from '../abilities'
import { CharacterService } from '../character.service';
import { Feature } from '../features';
import { Skill, SKILL_INFO } from '../skills'
import { Spell } from '../spells'
import { Class } from '../classes'
import { Feat } from '../feats'

@Component({
  selector: 'app-character-view',
  templateUrl: './character-view.component.html',
  styleUrls: ['./character-view.component.css']
})
export class CharacterViewComponent {
  @Input() selectRace!: () => void;
  @Input() selectBackground!: () => void;
  @Input() selectFeatureOption!: (feature: Feature, featureLevel: number) => void;
  @Input() selectClass!: (level: number) => void;
  @Input() selectSubclass!: (cls: Class) => void;
  @Input() selectFeat!: (level: number) => void;
  @Input() updateQueryParams!: () => void;
  readonly abilities = ABILITIES;
  readonly abilityInfo = ABILITY_INFO;

  constructor(private characterService: CharacterService) { }

  getCharacterService() {
    return this.characterService;
  }

  getFeatureString(level: number, feature: Feature) {
    let options = this.getFeatureSelectedOptionsString(level, feature);
    if (options) {
      return feature.name + ': ' + options
    } else {
      return feature.name
    }
  }

  getFeatString(level: number, feat: Feat | undefined) {
    if (feat) {
      let options = this.getFeatureSelectedOptionsString(level, feat);
      if (options) {
        return 'Feat: ' + feat.name + ' (' + options + ')';
      } else {
        return 'Feat: ' + feat.name
      }
    } else {
      return undefined;
    }
  }

  getFeatureSelectedOptionsString(level: number, feature: Feature) {
    let str = undefined;

    if (feature.skillSelection && feature.selectionCount) {
      str = ''
      let firstOption = true;
      for (let i = 0; i < feature.selectionCount; i++) {
        let selectedOption = this.getCharacterService().getFeatureSelectedOption(level, feature, i);
        if (selectedOption && selectedOption > 0 && selectedOption <= feature.skillSelection.length) {
          if (firstOption) {
            firstOption = false;
          } else {
            str += ', ';
          }
          str += SKILL_INFO.get(feature.skillSelection[selectedOption - 1])?.name;
        }
      }
    }

    if (feature.spellSelection && feature.selectionCount) {
      str = ''
      let firstOption = true;
      for (let i = 0; i < feature.selectionCount; i++) {
        let selectedOption = this.getCharacterService().getFeatureSelectedOption(level, feature, i);
        if (selectedOption && selectedOption > 0 && selectedOption <= feature.spellSelection.length) {
          if (firstOption) {
            firstOption = false;
          } else {
            str += ', ';
          }
          str += feature.spellSelection[selectedOption - 1].name;
        }
      }
    }

    if (feature.featureSelection && feature.selectionCount) {
      str = ''
      let firstOption = true;
      for (let i = 0; i < feature.selectionCount; i++) {
        let selectedOption = this.getCharacterService().getFeatureSelectedOption(level, feature, i);
        if (selectedOption && selectedOption > 0 && selectedOption <= feature.featureSelection.length) {
          if (firstOption) {
            firstOption = false;
          } else {
            str += ', ';
          }
          str += feature.featureSelection[selectedOption - 1].name;
        }
      }
    }

    if (feature.skillExpertiseSelection && feature.selectionCount) {
      str = ''
      let firstOption = true;
      let skills = Object.values(Skill);
      for (let i = 0; i < feature.selectionCount; i++) {
        let selectedOption = this.getCharacterService().getFeatureSelectedOption(level, feature, i);
        if (selectedOption && selectedOption > 0 && selectedOption <= skills.length) {
          if (firstOption) {
            firstOption = false;
          } else {
            str += ', ';
          }
          str += skills[selectedOption - 1];
        }
      }
    }

    if (feature.abilitySelection && feature.selectionCount) {
      str = ''
      let selectedAbilities = new Map<Ability, number>();
      for (let i = 0; i < feature.selectionCount; i++) {
        let selectedOption = this.getCharacterService().getFeatureSelectedOption(level, feature, i);
        if (selectedOption && selectedOption > 0 && selectedOption <= feature.abilitySelection.length) {
          let ability = feature.abilitySelection[selectedOption - 1];
          let value = selectedAbilities.get(ability)
          if (value) {
            selectedAbilities.set(ability, value + 1);
          } else {
            selectedAbilities.set(ability, 1);
          }
        }
      }

      let firstOption = true;
      for (let ability of selectedAbilities.keys()) {
        let abilityInfo = ABILITY_INFO.get(ability);
        if (abilityInfo) {
          if (firstOption) {
            firstOption = false;
          } else {
            str += ", "
          }
          str += abilityInfo.abbr + " +" + selectedAbilities.get(ability);
        }
      }
    }

    return str;
  }

  getSubclassString(cls: Class) {
    let subclass = this.characterService.getSelectedSubclass(cls);
    if (subclass) {
      return "Subclass: " + subclass.name;
    } else {
      return "Choose a Subclass";
    }
  }

  getSavingThrowProficiencyString(cls: Class) {
    let savingThrows = cls.savingThrowProficiencies;
    let savingThrowsString = '';
    let first = true;
    for (let ability of savingThrows) {
    let abilityInfo = ABILITY_INFO.get(ability);
      if (abilityInfo) {
        if (first) {
          first = false;
        } else {
          savingThrowsString += ', ';
        }
        savingThrowsString += abilityInfo.abbr;
      }
    }
    return savingThrowsString;
  }
}
