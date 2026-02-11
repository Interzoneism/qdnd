import { Component, OnInit } from '@angular/core';
import { CharacterService } from '../character.service';
import { Feature } from '../features';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { Class } from '../classes';
import { Feat } from '../feats';
import { Clipboard } from '@angular/cdk/clipboard';

enum State {
  MainView,
  SelectRaceView,
  SelectBackgroundView,
  SelectFeatureOption,
  SelectClassView,
  SelectSubclassView,
  SelectFeatView
}

@Component({
  selector: 'app-character-builder',
  templateUrl: './character-builder.component.html',
  styleUrls: ['./character-builder.component.css']
})
export class CharacterBuilderComponent implements OnInit {
  state : State = State.MainView;
  selectedFeature : Feature | undefined;
  featureLevel: number | undefined;
  selectedLevel: number | undefined;
  selectedClass: Class | undefined;
  featLevel: number | undefined;

  year: number;

  constructor(
    private characterService: CharacterService,
    private router: Router,
    private route: ActivatedRoute,
    private clipboard: Clipboard
  ) {
    this.year = (new Date()).getFullYear();
  }

  public showMainView = () => {
    this.state = State.MainView;
    this.selectedFeature = undefined;
    this.selectedLevel = undefined;
    this.featureLevel = undefined;
    this.selectedClass = undefined;
    this.featLevel = undefined;
    this.updateQueryParams();
  }

  ngOnInit() {
    this.characterService.loadFromParamMap(this.route.snapshot.queryParamMap);
    this.updateQueryParams();
  }

  public updateQueryParams = () => {
    this.router.navigate(
      [],
      {
          relativeTo: this.route,
          queryParams: this.characterService.createParamMap(),
          queryParamsHandling: 'merge'
      }
    );
  }

  isMainViewShown() {
    return this.state == State.MainView;
  }

  public showSelectRaceView = () => {
    this.state = State.SelectRaceView;
  }

  isSelectRaceViewShown() {
    return this.state == State.SelectRaceView;
  }

  public showSelectBackgroundView = () => {
    this.state = State.SelectBackgroundView;
  }

  isSelectBackgroundViewShown() {
    return this.state == State.SelectBackgroundView;
  }

  public showSelectFeatureOptionView = (feature: Feature, featureLevel: number) => {
    this.state = State.SelectFeatureOption;
    this.selectedFeature = feature;
    this.featureLevel = featureLevel;
  }

  isSelectFeatureOptionViewShown() {
    return this.state == State.SelectFeatureOption;
  }

  public showSelectClassView = (level: number) => {
    this.state = State.SelectClassView;
    this.selectedLevel = level;
  }

  isSelectClassViewShown() {
    return this.state == State.SelectClassView;
  }

  public showSelectSubclassView = (cls: Class) => {
    this.state = State.SelectSubclassView;
    this.selectedClass = cls;
  }

  isSelectSubclassViewShown() {
    return this.state == State.SelectSubclassView;
  }

  public showSelectFeatView = (featLevel: number) => {
    this.state = State.SelectFeatView;
    this.featLevel = featLevel;
  }

  isSelectFeatViewShown() {
    return this.state == State.SelectFeatView;
  }

  resetCharacter() {
    if (confirm("Are you sure to reset your character?")) {
      this.characterService.resetCharacter();
      this.updateQueryParams();
      this.showMainView();
    }
  }

  copyURL() {
    this.clipboard.copy(document.location.href);
  }
}
