import { Component, Input, HostListener } from '@angular/core';
import { Class, Subclass } from '../classes'
import { CharacterService } from '../character.service'

@Component({
  selector: 'app-select-subclass',
  templateUrl: './select-subclass.component.html',
  styleUrls: ['./select-subclass.component.css']
})
export class SelectSubclassComponent {
  @Input() goBack!: () => void;
  @Input() cls!: Class;
  selectedSubclass : Subclass | undefined;
  windowHeight: number;

  constructor(private characterService: CharacterService) {
    this.selectedSubclass = characterService.getSelectedSubclass(this.cls);
    this.windowHeight = 0;
  }

  ngOnInit() {
    this.windowHeight = window.innerHeight;
  }

  @HostListener('window:resize', ['$event'])
  onResize(event: any) {
    this.windowHeight = window.innerHeight;
  }

  setSelectedSubclass(subclass: Subclass) {
    this.selectedSubclass = subclass;
  }

  changeSubclass(subclassId: number) {
    this.characterService.setSelectedSubclass(this.cls, subclassId);
    this.goBack();
  }

  getCharacterService() {
    return this.characterService;
  }
}
