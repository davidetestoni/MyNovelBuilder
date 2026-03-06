import { Routes } from '@angular/router';
import { LayoutType } from './types/enums/layout-type';
import { AppRouteData } from './types/router/app-route-data';

const mainLayoutData = {
  layoutType: LayoutType.Main,
} as const satisfies AppRouteData;

export const routes: Routes = [
  // Redirect / to /novels
  {
    path: '',
    redirectTo: '/novels',
    pathMatch: 'full',
  },
  {
    path: 'novels',
    loadComponent: () =>
      import('./pages/novels/novels.component').then((m) => m.NovelsComponent),
    data: mainLayoutData,
  },
  {
    path: 'chat',
    loadComponent: () =>
      import('./pages/chats/chats.component').then((m) => m.ChatsComponent),
    data: mainLayoutData,
  },
  {
    path: 'chat/:id',
    loadComponent: () =>
      import('./pages/chats/chats.component').then((m) => m.ChatsComponent),
    data: mainLayoutData,
  },
  {
    path: 'compendia',
    loadComponent: () =>
      import('./pages/compendia/compendia.component').then(
        (m) => m.CompendiaComponent,
      ),
    data: mainLayoutData,
  },
  {
    path: 'novel/:id',
    loadComponent: () =>
      import('./pages/novel-editor/novel-editor.component').then(
        (m) => m.NovelEditorComponent,
      ),
    data: mainLayoutData, // TODO: Add a layout type for the editor
  },
  {
    path: 'novel/:id/settings',
    loadComponent: () =>
      import('./pages/novel-settings/novel-settings.component').then(
        (m) => m.NovelSettingsComponent,
      ),
    data: mainLayoutData,
  },
  {
    path: 'novel/:id/planner',
    loadComponent: () =>
      import('./pages/story-planner/story-planner.component').then(
        (m) => m.StoryPlannerComponent,
      ),
    data: mainLayoutData,
  },
  {
    path: 'compendium/:id',
    loadComponent: () =>
      import('./pages/compendium/compendium.component').then(
        (m) => m.CompendiumComponent,
      ),
    data: mainLayoutData,
  },
  {
    path: 'compendium/:id/record/:recordId',
    loadComponent: () =>
      import('./pages/compendium/compendium.component').then(
        (m) => m.CompendiumComponent,
      ),
    data: mainLayoutData,
  },
  {
    path: 'prompts',
    loadComponent: () =>
      import('./pages/prompts/prompts.component').then((m) => m.PromptsComponent),
    data: mainLayoutData,
  },
  {
    path: 'media-library',
    loadComponent: () =>
      import('./pages/media-library/media-library.component').then(
        (m) => m.MediaLibraryComponent,
      ),
    data: mainLayoutData,
  },
  {
    path: 'integrations',
    loadComponent: () =>
      import('./pages/integrations/integrations.component').then(
        (m) => m.IntegrationsComponent,
      ),
    data: mainLayoutData,
  },
  {
    path: 'voices',
    loadComponent: () =>
      import('./pages/voices/voices.component').then((m) => m.VoicesComponent),
    data: mainLayoutData,
  },
];
