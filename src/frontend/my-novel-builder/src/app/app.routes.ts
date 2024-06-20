import { Routes } from '@angular/router';
import { NovelsComponent } from './pages/novels/novels.component';
import { LayoutType } from './types/enums/layout-type';
import { CompendiumsComponent } from './pages/compendiums/compendiums.component';

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
    path: 'compendiums',
    component: CompendiumsComponent,
    data: {
      layoutType: LayoutType.Main,
    },
  },
];
