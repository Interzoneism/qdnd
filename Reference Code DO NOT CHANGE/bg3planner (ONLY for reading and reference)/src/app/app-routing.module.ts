import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CharacterBuilderComponent } from './character-builder/character-builder.component';

const routes: Routes = [
  { path: '', redirectTo: '/build', pathMatch: 'full' },
  { path: 'build', component: CharacterBuilderComponent }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
