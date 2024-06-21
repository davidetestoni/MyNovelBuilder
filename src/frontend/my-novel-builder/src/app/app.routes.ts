import { Routes } from '@angular/router';
import { NovelsComponent } from './pages/novels/novels.component';
import { LayoutType } from './types/enums/layout-type';
import { CompendiaComponent } from './pages/compendia/compendia.component';
import { NovelEditorComponent } from './pages/novel-editor/novel-editor.component';

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
];
