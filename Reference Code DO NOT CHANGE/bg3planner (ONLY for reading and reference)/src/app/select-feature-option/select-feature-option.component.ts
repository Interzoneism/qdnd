import { Component, Input, HostListener } from '@angular/core';
import { Feature } from '../features';
import { Skill, ISkill, SKILL_INFO } from '../skills';
import { CharacterService } from '../character.service'
import { Spell } from '../spells'
import { Ability, ABILITY_INFO} from '../abilities'

@Component({
  selector: 'app-select-feature-option',
  templateUrl: './select-feature-option.component.html',
  styleUrls: ['./select-feature-option.component.css']
})
export class SelectFeatureOptionComponent {
  @Input() goBack!: (success: boolean) => void;
  @Input() selectedFeature!: Feature;
  @Input() optionIndex!: number;
  @Input() featureLevel!: number;

  readonly skillInfo = SKILL_INFO;

  selectedSkill: Skill | undefined;
  selectedSkillInfo: ISkill | undefined;
  selectedSpell: Spell | undefined;
  selectedFeatureFeature: Feature | undefined;
  selectedAbility: Ability | undefined;
  previousSelections: number[];
  windowHeight: number;

  constructor(private characterService: CharacterService) {
    this.previousSelections = new Array<number>();this.windowHeight = 0;
  }

  ngOnInit() {
    this.windowHeight = window.innerHeight;
  }

  @HostListener('window:resize', ['$event'])
  onResize(event: any) {
    this.windowHeight = window.innerHeight;
  }

  setSelectedSkill(skill: Skill) {
    this.selectedSkill = skill;
    this.selectedSkillInfo = SKILL_INFO.get(skill);
  }

  setSelectedSpell(spell: Spell) {
    this.selectedSpell = spell;
  }

  setSelectedFeature(feature: Feature) {
    this.selectedFeatureFeature = feature;
  }

  selectFeatureOption(option: number) {
    this.previousSelections[this.optionIndex] = this.characterService.getFeatureSelectedOption(this.featureLevel, this.selectedFeature, this.optionIndex);
    this.characterService.setFeatureSelectedOption(this.featureLevel, this.selectedFeature, this.optionIndex++, option + 1);

    if (this.optionIndex == this.selectedFeature.selectionCount) {
      this.goBack(true);
    }
  }

  getCharacterService() {
    return this.characterService;
  }

  canSelectSpell(spell: Spell) {
    if (!this.selectedFeature.selectSameAllowed && this.selectedFeature.spellSelection) {
      for (let i = 0; i < this.optionIndex; i++) {
        if (spell == this.selectedFeature.spellSelection[this.characterService.getFeatureSelectedOption(this.featureLevel, this.selectedFeature, i) - 1]) {
          return false;
        }
      }
    }
    return true;
  }

  canSelectFeature(feature: Feature) {
    if (!this.selectedFeature.selectSameAllowed && this.selectedFeature.featureSelection) {
      // check if the feature had been selected before
      let allSelectedOptions = this.getCharacterService().getFeatureAllSelectedOptions(this.featureLevel - 1, this.selectedFeature);
      if (allSelectedOptions.has(feature.name)) {
        return false;
      }

      // check if it's been selected as part of this feat
      for (let i = 0; i < this.optionIndex; i++) {
        if (feature == this.selectedFeature.featureSelection[this.characterService.getFeatureSelectedOption(this.featureLevel, this.selectedFeature, i) - 1]) {
          return false;
        }
      }
    }

    return true;
  }

  canSelectAbility(ability: Ability) {
    if (!this.selectedFeature.selectSameAllowed && this.selectedFeature.abilitySelection) {
      for (let i = 0; i < this.optionIndex; i++) {
        let selectedOption = this.characterService.getFeatureSelectedOption(this.featureLevel, this.selectedFeature, i);
        if (selectedOption > 0 && this.selectedFeature.abilitySelection[selectedOption - 1] == ability) {
          return false;
        }
      }
    }
    return true;
  }

  hasSkillProficiency(skill: Skill): boolean {
    return this.getCharacterService().getSkillProficiencies(this.featureLevel, this.selectedFeature).has(skill);
  }

  hasSkillExpertise(skill: Skill): boolean {
    return this.getCharacterService().getSkillExpertises(this.featureLevel, this.selectedFeature).has(skill);
  }

  hasSelectedOption(index : number) {
    for (let i = 0; i < this.optionIndex; i++) {
      if (this.characterService.getFeatureSelectedOption(this.featureLevel, this.selectedFeature, i) - 1 == index) {
        return true;
      }
    }
    return false;
  }

  setSelectedAbility(ability: Ability) {
    this.selectedAbility = ability;
  }

  getAbilityInfo(ability: Ability) {
    return ABILITY_INFO.get(ability);
  }

  localGoBack() {
    if (this.optionIndex > 0) {
      this.optionIndex -= 1;
      this.characterService.setFeatureSelectedOption(this.featureLevel, this.selectedFeature, this.optionIndex, this.previousSelections[this.optionIndex]);
    } else {
      this.goBack(false);
    }
  }

  removeSelectedOptions() {
    if (this.selectedFeature.selectionCount) {
      for (let i = 0; i < this.selectedFeature.selectionCount; i++) {
        this.characterService.setFeatureSelectedOption(this.featureLevel, this.selectedFeature, i, 0);
      }
    }
    this.goBack(false);
  }

  getSkillList(): Skill[] {
    return Object.values(Skill);
  }
}
