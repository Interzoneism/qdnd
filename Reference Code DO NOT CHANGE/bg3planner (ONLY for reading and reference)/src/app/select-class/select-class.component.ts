import { Component, Input, HostListener } from '@angular/core';
import { Class, CLASSES } from '../classes'
import { CharacterService } from '../character.service'
import { ABILITY_INFO } from '../abilities'

@Component({
  selector: 'app-select-class',
  templateUrl: './select-class.component.html',
  styleUrls: ['./select-class.component.css']
})
export class SelectClassComponent {
  @Input() goBack!: () => void;
  @Input() level!: number;
  readonly classes = CLASSES;
  selectedClass : Class;
  windowHeight: number;

  constructor(private characterService: CharacterService) {
    this.selectedClass = characterService.getClass(this.level);
    this.windowHeight = 0;
  }

  ngOnInit() {
    this.windowHeight = window.innerHeight;
  }

  @HostListener('window:resize', ['$event'])
  onResize(event: any) {
    this.windowHeight = window.innerHeight;
  }


  setSelectedClass(cls: Class) {
    this.selectedClass = cls;
  }

  changeClass(cls: Class) {
    this.characterService.setClass(this.level, cls);
    this.goBack();
  }

  getCharacterService() {
    return this.characterService;
  }

  getSubclassListString() {
    let subclassString = "";
    let subclasses = this.selectedClass.subclasses;
    if (subclasses) {
      let first = true;
      for (let subclass of subclasses) {
        if (first) {
          first = false;
        } else {
          subclassString += ", "
        }
        subclassString += subclass.name;
      }
    }

    return subclassString;
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
        savingThrowsString += abilityInfo.name;
      }
    }
    return savingThrowsString;
  }
}
