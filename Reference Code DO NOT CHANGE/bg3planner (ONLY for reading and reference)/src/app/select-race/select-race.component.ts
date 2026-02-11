import { Component, Input, HostListener } from '@angular/core';
import { RACES, Race } from '../races'
import { CharacterService } from '../character.service'

@Component({
  selector: 'app-select-race',
  templateUrl: './select-race.component.html',
  styleUrls: ['./select-race.component.css']
})
export class SelectRaceComponent {
  @Input() goBack!: () => void;

  raceGroups: Map<Race, Array<Race>>;
  selectedRace : Race;
  windowHeight: number;

  constructor(private characterService: CharacterService) {
    this.selectedRace = characterService.getRace();
    this.windowHeight = 0;
    this.raceGroups = new Map<Race, Array<Race>>();

    for (let race of RACES) {
      if (race.parentRace) {
        let subraces = this.raceGroups.get(race.parentRace);
        if (subraces) {
          subraces.push(race);
        } else {
          this.raceGroups.set(race.parentRace, [race]);
        }
      } else {
        this.raceGroups.set(race, [race]);
      }
    }

  }

  ngOnInit() {
    this.windowHeight = window.innerHeight;
  }

  @HostListener('window:resize', ['$event'])
  onResize(event: any) {
    this.windowHeight = window.innerHeight;
  }

  setSelectedRace(race: Race) {
    this.selectedRace = race;
  }

  changeRace(race: Race) {
    this.characterService.setRace(race);
    this.goBack();
  }

  meterToFeet(meter: number) {
    return meter * 3.281;
  }

  getCharacterService() {
    return this.characterService;
  }
}
