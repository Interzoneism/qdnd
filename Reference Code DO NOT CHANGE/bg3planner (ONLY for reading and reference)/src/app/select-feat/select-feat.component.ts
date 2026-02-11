import { Component, Input, HostListener } from '@angular/core';
import { Feat, FEATS } from '../feats';
import { CharacterService } from '../character.service'

@Component({
  selector: 'app-select-feat',
  templateUrl: './select-feat.component.html',
  styleUrls: ['./select-feat.component.css']
})
export class SelectFeatComponent {
    @Input() goBack!: () => void;
    @Input() featLevel!: number;
    readonly feats = FEATS;
    selectedFeat : Feat | undefined;
    windowHeight: number;
    showSelectFeatOptions: boolean;
    pickedFeat: Feat | undefined;

    constructor(private characterService: CharacterService) {
      this.selectedFeat = undefined;
      this.windowHeight = 0;
      this.showSelectFeatOptions = false;
      this.pickedFeat = undefined;
    }

    ngOnInit() {
      this.windowHeight = window.innerHeight;
    }

    @HostListener('window:resize', ['$event'])
    onResize(event: any) {
      this.windowHeight = window.innerHeight;
    }

    setSelectedFeat(feat: Feat) {
      this.selectedFeat = feat;
    }

    removeSelectedFeat() {
      this.getCharacterService().removeFeat(this.featLevel);
      this.goBack();
    }

    pickFeat(feat : Feat) {
      if (!this.getCharacterService().isSelectionFeature(feat)) {
        this.characterService.setFeat(feat, this.featLevel);
        this.goBack();
      } else {
        this.pickedFeat = feat;
        this.showSelectFeatOptions = true;
      }
    }

    finishSelectFeatOptions = (success: boolean) => {
      if (success && this.pickedFeat) {
        this.characterService.setFeat(this.pickedFeat, this.featLevel);
        this.showSelectFeatOptions = false;
        this.pickedFeat = undefined;
        this.goBack();
      } else {
        this.showSelectFeatOptions = false;
        this.pickedFeat = undefined;
      }
    }

    canSelectFeat(feat: Feat): boolean {
      return (feat.canReselect || !this.getCharacterService().hasFeat(feat, this.featLevel - 1)) && this.getCharacterService().satisfiesFeatRequirements(feat, this.featLevel);
    }

    getCharacterService() {
      return this.characterService;
    }

}
