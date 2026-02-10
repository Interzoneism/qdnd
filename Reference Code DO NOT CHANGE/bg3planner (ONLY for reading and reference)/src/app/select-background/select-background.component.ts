import { Component, Input, HostListener } from '@angular/core';
import { Background, BACKGROUNDS } from '../backgrounds'
import { CharacterService } from '../character.service'

@Component({
  selector: 'app-select-background',
  templateUrl: './select-background.component.html',
  styleUrls: ['./select-background.component.css']
})
export class SelectBackgroundComponent {
  @Input() goBack!: () => void;
  readonly backgrounds = BACKGROUNDS;
  selectedBackground : Background;
  windowHeight: number;

  constructor(private characterService: CharacterService) {
    this.selectedBackground = characterService.getBackground();
    this.windowHeight = 0;
  }

  ngOnInit() {
    this.windowHeight = window.innerHeight;
  }

  @HostListener('window:resize', ['$event'])
  onResize(event: any) {
    this.windowHeight = window.innerHeight;
  }

  setSelectedBackground(background: Background) {
    this.selectedBackground = background;
  }

  changeBackground(background : Background) {
    this.characterService.setBackground(background);
    this.goBack();
  }

  getCharacterService() {
    return this.characterService;
  }
}
