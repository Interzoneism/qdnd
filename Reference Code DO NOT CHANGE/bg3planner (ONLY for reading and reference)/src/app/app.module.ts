import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';

import { AppComponent } from './app.component';
import { CharacterViewComponent } from './character-view/character-view.component';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { SelectRaceComponent } from './select-race/select-race.component';
import { AppRoutingModule } from './app-routing.module';
import { CharacterBuilderComponent } from './character-builder/character-builder.component';
import { SelectBackgroundComponent } from './select-background/select-background.component';
import { SelectFeatureOptionComponent } from './select-feature-option/select-feature-option.component';
import { SelectClassComponent } from './select-class/select-class.component';
import { SelectSubclassComponent } from './select-subclass/select-subclass.component';
import { SelectFeatComponent } from './select-feat/select-feat.component';

@NgModule({
  declarations: [
    AppComponent,
    CharacterViewComponent,
    SelectRaceComponent,
    CharacterBuilderComponent,
    SelectBackgroundComponent,
    SelectFeatureOptionComponent,
    SelectClassComponent,
    SelectSubclassComponent,
    SelectFeatComponent
  ],
  imports: [
    BrowserModule,
    BrowserAnimationsModule,
    AppRoutingModule
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule { }
