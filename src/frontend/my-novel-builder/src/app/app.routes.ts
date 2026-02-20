import { Routes } from '@angular/router';
import { NovelsComponent } from './pages/novels/novels.component';
import { LayoutType } from './types/enums/layout-type';
import { CompendiaComponent } from './pages/compendia/compendia.component';
import { NovelEditorComponent } from './pages/novel-editor/novel-editor.component';
import { CompendiumComponent } from './pages/compendium/compendium.component';
import { PromptsComponent } from './pages/prompts/prompts.component';
import { NovelSettingsComponent } from './pages/novel-settings/novel-settings.component';
import { IntegrationsComponent } from './pages/integrations/integrations.component';
import { ChatsComponent } from './pages/chats/chats.component';
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
    component: NovelsComponent,
    data: mainLayoutData,
  },
  {
    path: 'chat',
    component: ChatsComponent,
    data: mainLayoutData,
  },
  {
    path: 'chat/:id',
    component: ChatsComponent,
    data: mainLayoutData,
  },
  {
    path: 'compendia',
    component: CompendiaComponent,
    data: mainLayoutData,
  },
  {
    path: 'novel/:id',
    component: NovelEditorComponent,
    data: mainLayoutData, // TODO: Add a layout type for the editor
  },
  {
    path: 'novel/:id/settings',
    component: NovelSettingsComponent,
    data: mainLayoutData,
  },
  {
    path: 'compendium/:id',
    component: CompendiumComponent,
    data: mainLayoutData,
  },
  {
    path: 'compendium/:id/record/:recordId',
    component: CompendiumComponent,
    data: mainLayoutData,
  },
  {
    path: 'prompts',
    component: PromptsComponent,
    data: mainLayoutData,
  },
  {
    path: 'integrations',
    component: IntegrationsComponent,
    data: mainLayoutData,
  },
];
