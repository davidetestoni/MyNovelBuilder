import { Routes } from '@angular/router';
import { NovelsComponent } from './pages/novels/novels.component';
import { LayoutType } from './types/enums/layout-type';
import { CompendiaComponent } from './pages/compendia/compendia.component';
import { NovelEditorComponent } from './pages/novel-editor/novel-editor.component';
import { CompendiumComponent } from './pages/compendium/compendium.component';
import { PromptsComponent } from './pages/prompts/prompts.component';
import { NovelSettingsComponent } from './pages/novel-settings/novel-settings.component';

export const routes: Routes = [
  // Redirect / to /novels
  {
    path: '',
    redirectTo: '/novels',
    pathMatch: 'full',
  },
  {
    path: 'novels',
    component: NovelsComponent,
    data: {
      layoutType: LayoutType.Main,
    },
  },
  {
    path: 'compendia',
    component: CompendiaComponent,
    data: {
      layoutType: LayoutType.Main,
    },
  },
  {
    path: 'novel/:id',
    component: NovelEditorComponent,
    data: {
      layoutType: LayoutType.Main, // TODO: Add a layout type for the editor
    },
  },
  {
    path: 'novel/:id/settings',
    component: NovelSettingsComponent,
    data: {
      layoutType: LayoutType.Main,
    },
  },
  {
    path: 'compendium/:id',
    component: CompendiumComponent,
    data: {
      layoutType: LayoutType.Main,
    },
  },
  {
    path: 'prompts',
    component: PromptsComponent,
    data: {
      layoutType: LayoutType.Main,
    },
  },
];
