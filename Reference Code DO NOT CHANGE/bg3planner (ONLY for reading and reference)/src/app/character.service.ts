import { Injectable } from '@angular/core';
import { Character, MAX_LEVEL } from './character';
import { RACES, Race } from './races';
import { CLASSES } from './classes';
import { Background, BACKGROUNDS } from './backgrounds';
import { EquipmentProficiency, SIMPLE_WEAPONS, MARTIAL_WEAPONS } from './equipment';
import { Skill } from './skills'
import { Feature } from './features'
import { ParamMap, Params } from '@angular/router';
import { Class, Subclass } from './classes'
import { Ability, ABILITIES, MIN_ABILITY_SCORE, MAX_ABILITY_SCORE } from './abilities'
import { Feat, FEATS } from './feats'

@Injectable({
  providedIn: 'root'
})
export class CharacterService {

  private character: Character;

  constructor() {
    this.character = this.generateInitialCharacter();
  }

  public getRace() : Race {
    return this.character.race;
  }

  public setRace(race: Race) {
    this.character.race = race;
  }

  public getBackground() : Background {
    return this.character.background;
  }

  public setBackground(background: Background) {
    this.character.background = background;
  }

  public getMajorAbility() : Ability {
    return this.character.majorAbility;
  }

  public getMinorAbility() : Ability {
    return this.character.minorAbility;
  }

  public setMajorAbility(majorAbility : Ability) {
    this.character.majorAbility = majorAbility;
  }

  public setMinorAbility(minorAbility : Ability) {
    this.character.minorAbility = minorAbility;
  }

  public setClass(level: number, cls: Class) {
    this.character.classes[level - 1] = cls;
  }

  public getClass(level: number): Class {
    return this.character.classes[level - 1];
  }

  public getClasses() : Class[] {
    return this.character.classes;
  }

  public setSelectedSubclass(cls: Class, subclass: number) {
    this.character.selectedSubclasses.set(cls, subclass);
  }

  public getSelectedSubclass(cls: Class) : Subclass | undefined {
    let selectedSubclass = this.character.selectedSubclasses.get(cls);
    if (selectedSubclass && selectedSubclass > 0) {
      return cls.subclasses[selectedSubclass - 1]
    } else {
      return undefined;
    }
  }

  public getFeat(level: number) : Feat | undefined {
    if (this.isFeatLevel(level)) {
      let cls = this.getClass(level);
      let clsLvl = this.getClassLevel(level);
      let clsFeats = this.character.selectedFeats.get(cls);
      if (!clsFeats) {
        return undefined;
      } else {
        return clsFeats.get(clsLvl);
      }
    } else {
      return undefined;
    }
  }

  public setFeat(feat: Feat, level: number) {
    if (this.isFeatLevel(level)) {
      let cls = this.getClass(level);
      let clsLvl = this.getClassLevel(level);
      let clsFeats = this.character.selectedFeats.get(cls);
      if (!clsFeats) {
        clsFeats = new Map<number, Feat>();
        clsFeats.set(clsLvl, feat);
        this.character.selectedFeats.set(cls, clsFeats);
      } else {
        clsFeats.set(clsLvl, feat);
      }
    }
  }

  public removeFeat(level: number) {
    if (this.isFeatLevel(level)) {
      let cls = this.getClass(level);
      let clsLvl = this.getClassLevel(level);
      let clsFeats = this.character.selectedFeats.get(cls);
      if (clsFeats) {
        clsFeats.delete(clsLvl);
      }
    }
  }

  public hasFeat(feat: Feat, maxLevel: number) {
    for (let lvl = 1; lvl <= maxLevel; lvl++) {
      if (this.isFeatLevel(lvl)) {
        let f = this.getFeat(lvl);
        if (f && f.name == feat.name) {
          return true;
        }
      }
    }
    return false;
  }

  public satisfiesFeatRequirements(feat: Feat, level: number): boolean {
    let reqs = feat.requirements;
    if (reqs) {
      let equipReqs = reqs.equipmentProficiencies;
      if (equipReqs) {
        let currentFeat = this.getFeat(level);
        let equipmentProficiencies = this.getEquipmentProficiencies(level, currentFeat);
        for (let equipReq of equipReqs) {
          if (!equipmentProficiencies.has(equipReq)) {
            return false;
          }
        }
      }
    }
    return true;
  }

  public getSkillProficiencyFeature() {
    return this.character.classes[0].skillProficiencies;
  }


  public loadFromParamMap(map: ParamMap) {
    let charCode = map.get('char');
    if (charCode) {
      let bytes = this.hexStringToByteArray(charCode);
      let i = 0;
      let version = bytes[i++]; // ignored for now
      // load race
      let raceIndex = bytes[i++];
      if (raceIndex >= 0 && raceIndex < RACES.length) {
        this.character.race = RACES[raceIndex];
      }

      // load racial feature selections
      for (let feature of this.getRacialFeatures()) {
        if (this.isSelectionFeature(feature) && feature.selectionCount) {
          for (let j = 0; j < feature.selectionCount; j++) {
            let selectedOption = bytes[i++];
            if (selectedOption > 0 && selectedOption <= this.getFeatureOptionCount(feature)) {
              this.setFeatureSelectedOption(0, feature, j, selectedOption);
            }
          }
        }
      }

      // load background
      let backgroundIndex = bytes[i++];
      if (backgroundIndex >= 0 && backgroundIndex < BACKGROUNDS.length) {
        this.character.background = BACKGROUNDS[backgroundIndex];
      }

      // load ability scores
      for (let ability of ABILITIES) {
        this.character.abilityScores.set(ability, bytes[i++]);
      }
      let majorAbility = bytes[i++];
      let minorAbility = bytes[i++];
      if (0 <= majorAbility && majorAbility < ABILITIES.length && 0 <= minorAbility && minorAbility < ABILITIES.length && majorAbility != minorAbility) {
        this.setMajorAbility(ABILITIES[majorAbility]);
        this.setMinorAbility(ABILITIES[minorAbility]);
      }

      // load character levels
      let totalLevel = bytes[i++];
      for (let level = 1; level <= totalLevel; level++) {
        this.character.classes[level - 1] = CLASSES[bytes[i++]];

        // load level 1 skill proficiencies
        if (level == 1) {
          let skillProficiencyFeature = this.getSkillProficiencyFeature();
          if (skillProficiencyFeature.skillSelection && skillProficiencyFeature.selectionCount) {
            for (let j = 0; j < skillProficiencyFeature.selectionCount; j++) {
              this.setFeatureSelectedOption(level, skillProficiencyFeature, j, bytes[i++]);
            }
          }
        }

        // load subclass
        if (this.isSubclassLevel(level)) {
          this.character.selectedSubclasses.set(this.character.classes[level - 1], bytes[i++]);
        }

        // load feats
        if (this.isFeatLevel(level)) {
          let featId = bytes[i++];
          if (featId > 0) {
            let feat = FEATS[featId - 1];
            this.setFeat(feat, level);

            if (this.isSelectionFeature(feat) && feat.selectionCount) {
              for (let j = 0; j < feat.selectionCount; j++) {
                this.setFeatureSelectedOption(level, feat, j, bytes[i++]);
              }
            }
          }
        }

        // load class selection features per level
        for (let feature of this.getClassFeatures(level)) {
          if (this.isSelectionFeature(feature) && feature.selectionCount) {
            for (let j = 0; j < feature.selectionCount; j++) {
              this.setFeatureSelectedOption(level, feature, j, bytes[i++]);
            }
          }
        }
      }
    }
  }

  private hexStringToByteArray(hex: string) {
    let bytes = Array<number>(hex.length / 2);
    for (let i = 0; i < hex.length; i += 2) {
      bytes[i/2] = Number('0x' + hex.substring(i, i + 2));
    }
    return bytes;
  }


  public createParamMap() : Params {
    let queryParams = {} as Params;

    // add version 1 identifier
    let code = this.byteToHex(1);

    // create race parameters
    code += this.byteToHex(RACES.indexOf(this.character.race));
    for (let feature of this.getRacialFeatures()) {
      if (this.isSelectionFeature(feature) && feature.selectionCount) {
        for (let j = 0; j < feature.selectionCount; j++) {
          let selectedOption = this.getFeatureSelectedOption(0, feature, j);
          if (selectedOption) {
            code += this.byteToHex(selectedOption);
          } else {
            code += this.byteToHex(0);
          }
        }
      }
    }

    // create background parameters
    code += this.byteToHex(BACKGROUNDS.indexOf(this.character.background));

    // create ability score parameters
    for (let ability of ABILITIES) {
      code += this.byteToHex(this.getAbilityScore(ability));
    }
    code += this.byteToHex(ABILITIES.indexOf(this.getMajorAbility()));
    code += this.byteToHex(ABILITIES.indexOf(this.getMinorAbility()));

    // create class parameters
    code += this.byteToHex(this.getCharacterLevel());
    for (let level = 1; level <= this.getCharacterLevel(); level++) {
      let cls = this.getClass(level);
      code += this.byteToHex(CLASSES.indexOf(cls));

      // add level 1 skill proficiencies
      if (level == 1) {
        let skillProficiencyFeature = this.getSkillProficiencyFeature();
        if (skillProficiencyFeature.skillSelection && skillProficiencyFeature.selectionCount) {
          for (let i = 0; i < skillProficiencyFeature.selectionCount; i++) {
            let selectedOption = this.getFeatureSelectedOption(1, skillProficiencyFeature, i);
            if (selectedOption) {
              code += this.byteToHex(selectedOption);
            } else {
              code += this.byteToHex(0);
            }
          }
        }
      }

      // add subclass
      if (this.isSubclassLevel(level)) {
        let subclassId = this.character.selectedSubclasses.get(cls);
        if (subclassId) {
          code += this.byteToHex(subclassId);
        } else {
          code += this.byteToHex(0);
        }
      }

      // add feats
      if (this.isFeatLevel(level)) {
        let selectedFeat = this.getFeat(level);
        if (selectedFeat) {
          code += this.byteToHex(FEATS.indexOf(selectedFeat) + 1);
          if (this.isSelectionFeature(selectedFeat) && selectedFeat.selectionCount) {
            for (let j = 0; j < selectedFeat.selectionCount; j++) {
              let selectedOption = this.getFeatureSelectedOption(level, selectedFeat, j);
              if (selectedOption) {
                code += this.byteToHex(selectedOption);
              } else {
                code += this.byteToHex(0);
              }
            }
          }
        } else {
          code += this.byteToHex(0);
        }
      }

      // add class selection features per level
      for (let feature of this.getClassFeatures(level)) {
        if (this.isSelectionFeature(feature) && feature.selectionCount) {
          for (let j = 0; j < feature.selectionCount; j++) {
            let selectedOption = this.getFeatureSelectedOption(level, feature, j);
            if (selectedOption) {
              code += this.byteToHex(selectedOption);
            } else {
              code += this.byteToHex(0);
            }
          }
        }
      }
    }

    queryParams['char'] = code;

    return queryParams;
  }


  private byteToHex(byte: number) {
    if (byte < 0 || byte > 255) throw new Error("Invalid byte value");

    let hex = byte.toString(16);
    while (hex.length < 2) {
      hex = '0' + hex;
    }

    return hex;
  }

  public getFeatureSelectedOption(level: number, feature: Feature, index: number): number {
    let featureSelectedOptions = this.getFeatureSelectedOptions(level, feature);
    if (featureSelectedOptions) {
      return featureSelectedOptions[index];
    } else {
      return 0;
    }
  }

  public getFeatureSelectedOptions(level: number, feature: Feature) : number[] {
    if (this.isSelectionFeature(feature) && feature.selectionCount) {
      if (level == 0) { // racial features
        let raceSelectedOptions = this.character.selectedRaceFeatureOptions.get(this.character.race);
        if (raceSelectedOptions) {
          let raceFeatureSelectedOptions = raceSelectedOptions.get(feature);
          if (raceFeatureSelectedOptions) {
            return raceFeatureSelectedOptions;
          } else {
            return [];
          }
        } else {
          return [];
        }
      } else { // class features
        let cls = this.getClass(level);
        let clsLvl = this.getClassLevel(level);
        let clsSelectedOptions =  this.character.selectedClassFeatureOptions.get(cls);
        if (clsSelectedOptions && clsSelectedOptions[clsLvl]) {
          let selectedClsLvlOptions = clsSelectedOptions[clsLvl].get(feature);
          if (selectedClsLvlOptions) {
            return selectedClsLvlOptions;
          } else {
            return [];
          }
        } else {
          return [];
        }
      }
    } else {
      return [];
    }
  }

  public getFeatureAllSelectedOptions(maxLevel: number, feature: Feature) : Set<string> {
    let selectedOptions = new Set<string>();

    for (let f of this.getRacialFeatures()) {
      if (f.name == feature.name) {
        for (let selectedOption of this.getFeatureSelectedOptions(0, f)) {
          if (selectedOption != 0 && f.featureSelection) {
            selectedOptions.add(f.featureSelection[selectedOption - 1].name);
          }
        }
      }
    }

    for (let level = 1; level <= maxLevel && level <= this.getCharacterLevel(); level++) {
      for (let f of this.getClassFeatures(level)) {
        if (f.name == feature.name) {
          for (let selectedOption of this.getFeatureSelectedOptions(level, f)) {
            if (selectedOption != 0 && f.featureSelection) {
              selectedOptions.add(f.featureSelection[selectedOption - 1].name);
            }
          }
        }
      }
    }
    return selectedOptions;
  }

  public setFeatureSelectedOption(level: number, feature: Feature, index: number, option: number) {
    if (this.isSelectionFeature(feature) && feature.selectionCount && index >= 0 && index < feature.selectionCount) {
      if (level  == 0) { // racial features
        let raceSelectedOptions = this.character.selectedRaceFeatureOptions.get(this.character.race);
        if (raceSelectedOptions) {
          let raceFeatureSelectedOptions = raceSelectedOptions.get(feature);
          if (raceFeatureSelectedOptions) {
            raceFeatureSelectedOptions[index] = option;
          } else {
            raceFeatureSelectedOptions = new Array<number>();
            raceFeatureSelectedOptions[index] = option;
            raceSelectedOptions.set(feature, raceFeatureSelectedOptions);
          }
        } else {
          raceSelectedOptions = new Map<Feature, Array<number>>();
          let raceFeatureSelectedOptions = new Array<number>();
          raceFeatureSelectedOptions[index] = option;
          raceSelectedOptions.set(feature, raceFeatureSelectedOptions);
          this.character.selectedRaceFeatureOptions.set(this.character.race, raceSelectedOptions);
        }
      } else { // class features
        let cls = this.getClass(level);
        let clsLvl = this.getClassLevel(level);
        let clsSelectedOptions =  this.character.selectedClassFeatureOptions.get(cls);
        if (clsSelectedOptions) {
          if (clsSelectedOptions[clsLvl]) {
            let featureSelectedOptions = clsSelectedOptions[clsLvl].get(feature);
            if (featureSelectedOptions) {
              featureSelectedOptions[index] = option;
            } else {
              featureSelectedOptions = new Array<number>();
              featureSelectedOptions[index] = option;
              clsSelectedOptions[clsLvl].set(feature, featureSelectedOptions);
            }
          } else {
            let featureSelectedOptions = new Array<number>();
            featureSelectedOptions[index] = option;
            clsSelectedOptions[clsLvl] = new Map<Feature, Array<number>>();
            clsSelectedOptions[clsLvl].set(feature, featureSelectedOptions);
          }
        } else {
          clsSelectedOptions = new Array();
          let featureSelectedOptions = new Array<number>();
          featureSelectedOptions[index] = option;
          clsSelectedOptions[clsLvl] = new Map<Feature, Array<number>>();
          clsSelectedOptions[clsLvl].set(feature, featureSelectedOptions);
          this.character.selectedClassFeatureOptions.set(cls, clsSelectedOptions);
        }
      }
    }
  }

  private getFeatureOptionCount(feature : Feature) {
    let count = 0;
    if (feature.skillSelection && count < feature.skillSelection.length) {
      count = feature.skillSelection.length;
    }
    if (feature.spellSelection && count < feature.spellSelection.length) {
      count = feature.spellSelection.length;
    }
    if (feature.featureSelection && count < feature.featureSelection.length) {
      count = feature.featureSelection.length;
    }
    if (feature.skillExpertiseSelection && count < Object.values(Skill).length) {
      count = Object.values(Skill).length;
    }
    if (feature.abilitySelection && count < feature.abilitySelection.length) {
      count = feature.abilitySelection.length;
    }
    return count;
  }

  public isSelectionFeature(feature : Feature) {
    return this.getFeatureOptionCount(feature) > 0;
  }

  private generateInitialCharacter(): Character {
    return {
      name: 'Tav',
      race: RACES[0],
      background: BACKGROUNDS[0],
      abilityScores: new Map<Ability, number>([
        [Ability.Strength, MIN_ABILITY_SCORE],
        [Ability.Dexterity, MIN_ABILITY_SCORE],
        [Ability.Constitution, MIN_ABILITY_SCORE],
        [Ability.Intelligence, MIN_ABILITY_SCORE],
        [Ability.Wisdom, MIN_ABILITY_SCORE],
        [Ability.Charisma, MIN_ABILITY_SCORE],
      ]),
      majorAbility: Ability.Strength,
      minorAbility: Ability.Dexterity,
      classes: [CLASSES[0]],
      selectedClassFeatureOptions: new Map<Class, Array<Map<Feature, Array<number>>>>(),
      selectedRaceFeatureOptions: new Map<Race, Map<Feature, Array<number>>>(),
      selectedSubclasses: new Map<Class, number>(),
      selectedFeats: new Map<Class, Map<number, Feat>>()
    };
  }

  public resetCharacter() {
    this.character = this.generateInitialCharacter();
  }

  public getRacialFeatures() : Feature[] {
    let features = new Array<Feature>();

    if (this.character.race.parentRace) {
      for (let feature of this.character.race.parentRace.features) {
        features.push(feature);
      }
    }

    for (let feature of this.character.race.features) {
      features.push(feature);
    }

    return features;
  }

  public getClassFeatures(level: number) : Feature[] {
    let classLevel = this.getClassLevel(level);
    let featureList = new Array<Feature>();

    for (let feature of this.getClass(level).features[classLevel - 1]) {
      featureList.push(feature);
    }

    let subclass = this.getSelectedSubclass(this.getClass(level));
    if (subclass) {
      for (let feature of subclass.features[classLevel - 1]) {
        featureList.push(feature);
      }
    }

    return featureList;
  }

  public isFeatLevel(level: number) : boolean {
    return this.character.classes[level - 1].featLevels.includes(this.getClassLevel(level));
  }

  public isSubclassLevel(level: number) : boolean {
    return this.character.classes[level - 1].subclassLevel == this.getClassLevel(level);
  }

  public getAbilityScore(ability: Ability) : number {
    let abilityScore = this.character.abilityScores.get(ability);
    if (abilityScore) {
      return abilityScore;
    } else {
      return MIN_ABILITY_SCORE;
    }
  }

  public getFinalAbilityScore(ability: Ability) : number {
    if (this.character) {
      let abilityScore = this.getAbilityScore(ability);
      if (ability == this.character.majorAbility)
        abilityScore += 2;
      if (ability == this.character.minorAbility)
        abilityScore += 1;
      return abilityScore
    } else {
      return 0;
    }
  }

  public getAbilityModifier(ability: Ability) {
    if (this.character) {
      let mod = Math.floor((this.getFinalAbilityScore(ability) - 10) / 2);
      return (mod < 0) ? '' + mod : '+' + mod;
    } else {
      return 0;
    }
  }

  private getAbilityPointCost(abilityScore: number) {
    if (abilityScore <= 13 && abilityScore >= 8) {
      return abilityScore - 8;
    } else if (abilityScore == 14) {
      return 7;
    } else if (abilityScore == 15) {
      return 9;
    } else {
      return NaN;
    }
  }

  public getAvailableAbilityPoints() {
    let points = 27;
    if (this.character) {
      for (let abilityScore of this.character.abilityScores.values()) {
        points -= this.getAbilityPointCost(abilityScore);
      }
    }
    return points;
  }

  public increaseAbilityScore(ability: Ability) {
    if (this.character) {
      let availablePoints = this.getAvailableAbilityPoints();
      let currentScore = this.getAbilityScore(ability);
      let neededPoints = (currentScore <= 12) ? 1 : 2;

      if (neededPoints <= availablePoints && currentScore < MAX_ABILITY_SCORE) {
        this.character.abilityScores.set(ability, currentScore + 1);
      }
    }
  }

  public decreaseAbilityScore(ability: Ability) {
    if (this.character) {
      let currentScore = this.getAbilityScore(ability);
      if (currentScore > MIN_ABILITY_SCORE) {
        this.character.abilityScores.set(ability, currentScore - 1);
      }
    }
  }

  /*
   * Returns all equipment proficiencies up to a certain level
   */
  public getEquipmentProficiencies(maxLevel : number, excludedFeat?: Feat): Set<EquipmentProficiency> {
    let proficiencies = new Set<EquipmentProficiency>();

    // add racial proficiencies
    for (let feature of this.character.race.features) {
      for (let proficiency of this.getFeatureEquipmentProficiencies(feature, 0)) {
        proficiencies.add(proficiency);
      }
    }
    if (this.character.race.parentRace) {
      for (let feature of this.character.race.parentRace.features) {
        for (let proficiency of this.getFeatureEquipmentProficiencies(feature, 0)) {
          proficiencies.add(proficiency);
        }
      }
    }

    // add all class proficiencies up to the given level
    for (let l = 1; l <= maxLevel; l++) {
      // add class equipment proficiencies at class level 1
      for (let proficiency of this.getClassEquipmentProficiencies(l)) {
        proficiencies.add(proficiency);
      }

      // add feature effect equipment proficiencies
      for (let feature of this.getClassFeatures(l)) {
        for (let proficiency of this.getFeatureEquipmentProficiencies(feature, l)) {
          proficiencies.add(proficiency);
        }
      }

      // add feat equipment proficiencies
      if (this.isFeatLevel(l)) {
        let feat = this.getFeat(l);
        if (feat && feat != excludedFeat) {
          for (let proficiency of this.getFeatureEquipmentProficiencies(feat, l)) {
            proficiencies.add(proficiency);
          }
        }
      }
    }

    return proficiencies;
  }

  private getFeatureEquipmentProficiencies(feature: Feature, level: number): Set<EquipmentProficiency> {
    let proficiencies = new Set<EquipmentProficiency>();
    if (feature.effect && feature.effect.equipmentProficiencies) {
      for (let proficiency of feature.effect.equipmentProficiencies) {
        proficiencies.add(proficiency);
      }
    }

    if (feature.featureSelection && feature.selectionCount) {
      for (let i = 0; i < feature.selectionCount; i++) {
        let selectedOption = this.getFeatureSelectedOption(level, feature, i);
        if (selectedOption && selectedOption != 0) {
          let subFeature = feature.featureSelection[selectedOption - 1];
          if (subFeature.effect && subFeature.effect.equipmentProficiencies) {
            for (let proficiency of subFeature.effect.equipmentProficiencies) {
              proficiencies.add(proficiency);
            }
          }
        }
      }
    }

    return proficiencies;
  }

  public hasEquipmentProficiency(level: number, proficiency: EquipmentProficiency, excludedFeat?: Feat) {
    let proficiencies = this.getEquipmentProficiencies(level);
    let hasMartialWeaponProficiency = proficiencies.has(EquipmentProficiency.MartialWeapons);
    let hasSimpleWeaponProficiency = proficiencies.has(EquipmentProficiency.SimpleWeapons);

    return proficiencies.has(proficiency) || (hasMartialWeaponProficiency && MARTIAL_WEAPONS.has(proficiency)) || (hasSimpleWeaponProficiency && SIMPLE_WEAPONS.has(proficiency));
  }


  public getClassEquipmentProficiencies(level: number) : EquipmentProficiency[] {
    let proficiencies = new Array<EquipmentProficiency>();

    if (level == 1) {
      proficiencies = proficiencies.concat(this.character.classes[level - 1].initialEquipmentProficiencies);
    }

    if (this.getClassLevel(level) == 1) {
      proficiencies = proficiencies.concat(this.character.classes[level - 1].equipmentProficiencies);
    }

    if (this.isSubclassLevel(level)) {
      let subclass = this.getSelectedSubclass(this.getClass(level));
      if (subclass && subclass.equipmentProficiencies) {
        proficiencies = proficiencies.concat(subclass.equipmentProficiencies);
      }
    }

    return proficiencies;
  }

  /*
   * Returns a set of skill proficiencies up to a given level, and excluding
   * an optional feature argument.
   */
  public getSkillProficiencies(maxLevel: number, excludedFeature?: Feature): Set<Skill> {
    let proficiencies = new Set<Skill>();

    // add racial proficiencies
    for (let feature of this.character.race.features) {
      if (feature === excludedFeature) {
        continue;
      }
      for (let skill of this.getFeatureSkillProficiencies(feature, 0)) {
        proficiencies.add(skill);
      }
    }

    if (this.character.race.parentRace) {
      for (let feature of this.character.race.parentRace.features) {
        if (feature === excludedFeature) {
          continue;
        }
        for (let skill of this.getFeatureSkillProficiencies(feature, 0)) {
          proficiencies.add(skill);
        }
      }
    }

    // add background proficiencies
    for (let proficiency of this.character.background.proficiencies) {
      proficiencies.add(proficiency);
    }

    // add skill proficiencies from character level 1
    if (maxLevel >= 1 && excludedFeature != this.getSkillProficiencyFeature()) {
      let skillProficiencyFeature = this.getSkillProficiencyFeature();
      if (skillProficiencyFeature.skillSelection && skillProficiencyFeature.selectionCount) {
        for (let i = 0; i < skillProficiencyFeature.selectionCount; i++) {
          let selectedOption = this.getFeatureSelectedOption(1, skillProficiencyFeature, i);
          if (selectedOption && selectedOption != 0) {
            proficiencies.add(skillProficiencyFeature.skillSelection[selectedOption - 1]);
          }
        }
      }
    }

    // add class proficiencies up to given level
    for (let level = 1; level <= maxLevel; level++) {

      // add proficiencies from features
      for (let feature of this.getClassFeatures(level)) {
        if (feature === excludedFeature) {
          continue;
        }

        for (let skill of this.getFeatureSkillProficiencies(feature, level)) {
          proficiencies.add(skill);
        }
      }

      // add proficiencies from feats
      if (this.isFeatLevel(level)) {
        let feat = this.getFeat(level);
        if (feat && feat != excludedFeature) {
          for (let skill of this.getFeatureSkillProficiencies(feat, level)) {
            proficiencies.add(skill);
          }
        }
      }
    }

    return proficiencies;
  }

  private getFeatureSkillProficiencies(feature: Feature, level: number): Set<Skill> {
    let proficiencies = new Set<Skill>();

    if (feature.effect && feature.effect.skillProficiencies) {
      for (let proficiency of feature.effect.skillProficiencies) {
        proficiencies.add(proficiency);

      }
    }
    if (feature.skillSelection && feature.selectionCount) {
      for (let i = 0; i < feature.selectionCount; i++) {
        let selectedOption = this.getFeatureSelectedOption(level, feature, i);
        if (selectedOption != 0) {
          proficiencies.add(feature.skillSelection[selectedOption - 1]);
        }
      }
    }

    if (feature.featureSelection && feature.selectionCount) {
      for (let i = 0; i < feature.selectionCount; i++) {
        let selectedOption = this.getFeatureSelectedOption(level, feature, i);
        if (selectedOption && selectedOption != 0) {
          let subFeature = feature.featureSelection[selectedOption - 1];
          if (subFeature) {
            for (let skill of this.getFeatureSkillProficiencies(subFeature, level)) {
              proficiencies.add(skill);
            }
          }
        }
      }
    }

    return proficiencies;
  }

  public getSkillExpertises(maxLevel: number, excludedFeature?: Feature): Set<Skill> {
    let expertises = new Set<Skill>();
    let skills = Object.values(Skill);

    // add racial expertise
    for (let feature of this.character.race.features) {
      if (feature === excludedFeature) {
        continue;
      }
      if (feature.skillExpertiseSelection && feature.selectionCount) {
        for (let i = 0; i < feature.selectionCount; i++) {
          let selectedOption = this.getFeatureSelectedOption(0, feature, i);
          if (selectedOption != 0) {
            expertises.add(skills[selectedOption - 1]);
          }
        }
      }
    }

    if (this.character.race.parentRace) {
      for (let feature of this.character.race.parentRace.features) {
        if (feature === excludedFeature) {
          continue;
        }
        if (feature.skillExpertiseSelection && feature.selectionCount) {
          for (let i = 0; i < feature.selectionCount; i++) {
            let selectedOption = this.getFeatureSelectedOption(0, feature, i);
            if (selectedOption != 0) {
              expertises.add(skills[selectedOption - 1]);
            }
          }
        }
      }
    }

    // add class expertises up to given level
    for (let level = 1; level <= maxLevel; level++) {

      // add expertises from features
      for (let feature of this.getClassFeatures(level)) {
        if (feature === excludedFeature) {
          continue;
        }

        if (feature.skillExpertiseSelection && feature.selectionCount) {
          for (let i = 0; i < feature.selectionCount; i++) {
            let selectedOption = this.getFeatureSelectedOption(level, feature, i);

            if (selectedOption != 0) {
              expertises.add(skills[selectedOption - 1]);
            }
          }
        }
      }
    }

    return expertises;
  }

  public addLevel() {
    if (this.character.classes.length < MAX_LEVEL) {
      this.character.classes.push(this.character.classes[this.character.classes.length - 1]);
    }
  }

  public removeLevel() {
    if (this.character.classes.length > 1) {
      this.character.classes.pop();
    }
  }

  public getCharacterLevel() {
    return this.character.classes.length;
  }

  public getClassLevel(level: number) {
    if (level > this.getCharacterLevel()) {
      return 0;
    }
    let classLevel = 0;
    for (let i = 0; i < level && i < this.getCharacterLevel(); i++) {
      if (this.character.classes[i] == this.character.classes[level - 1]) {
        classLevel++;
      }
    }
    return classLevel;
  }
}
