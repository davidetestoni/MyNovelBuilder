import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideAnimations } from '@angular/platform-browser/animations';
import { providePrimeNG } from 'primeng/config';
import Nora from '@primeuix/themes/aura';
import { definePreset } from '@primeuix/themes';

import { routes } from './app.routes';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideToastr } from 'ngx-toastr';
import { provideMarkdown } from 'ngx-markdown';
import { apiErrorInterceptor } from './interceptors/api-error.interceptor';
import { NovelService } from './services/novel.service';
import { ApiNovelService } from './services/api-novel.service';
import { MockNovelService } from './services/mock-novel.service';
import { environment } from '../environment';
import { PromptService } from './services/prompt.service';
import { ApiPromptService } from './services/api-prompt.service';
import { MockPromptService } from './services/mock-prompt.service';
import { CompendiumService } from './services/compendium.service';
import { ApiCompendiumService } from './services/api-compendium.service';
import { MockCompendiumService } from './services/mock-compendium.service';
import { ChatService } from './services/chat.service';
import { ApiChatService } from './services/api-chat.service';
import { MockChatService } from './services/mock-chat.service';
import { IntegrationsService } from './services/integrations.service';
import { ApiIntegrationsService } from './services/api-integrations.service';
import { MockIntegrationsService } from './services/mock-integrations.service';
import { GenerateTextService } from './services/generate-text.service';
import { ApiGenerateTextService } from './services/api-generate-text.service';
import { MockGenerateTextService } from './services/mock-generate-text.service';
import { GenerateImageService } from './services/generate-image.service';
import { ApiGenerateImageService } from './services/api-generate-image.service';
import { MockGenerateImageService } from './services/mock-generate-image.service';
import { GenerateVideoService } from './services/generate-video.service';
import { ApiGenerateVideoService } from './services/api-generate-video.service';
import { MockGenerateVideoService } from './services/mock-generate-video.service';
import { GenerateAudioService } from './services/generate-audio.service';
import { ApiGenerateAudioService } from './services/api-generate-audio.service';
import { MockGenerateAudioService } from './services/mock-generate-audio.service';
import { VoiceService } from './services/voice.service';
import { ApiVoiceService } from './services/api-voice.service';
import { MockVoiceService } from './services/mock-voice.service';
import { MediaLibraryService } from './services/media-library.service';
import { ApiMediaLibraryService } from './services/api-media-library.service';
import { MockMediaLibraryService } from './services/mock-media-library.service';
import { WorldBuildingSessionService } from './services/world-building-session.service';
import { ApiWorldBuildingSessionService } from './services/api-world-building-session.service';
import { MockWorldBuildingSessionService } from './services/mock-world-building-session.service';

const primeNgTheme = definePreset(Nora, {
  semantic: {
    primary: {
      50: '{stone.50}',
      100: '{stone.100}',
      200: '{stone.200}',
      300: '{stone.300}',
      400: '{stone.400}',
      500: '{stone.500}',
      600: '{stone.600}',
      700: '{stone.700}',
      800: '{stone.800}',
      900: '{stone.900}',
      950: '{stone.950}',
    },
    colorScheme: {
      dark: {
        formField: {
          background: '{surface.800}',
        },
      },
    },
  },
});

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideAnimations(),
    provideHttpClient(withInterceptors([apiErrorInterceptor])),
    {
      provide: NovelService,
      useClass: environment.mocked ? MockNovelService : ApiNovelService,
    },
    {
      provide: PromptService,
      useClass: environment.mocked ? MockPromptService : ApiPromptService,
    },
    {
      provide: CompendiumService,
      useClass: environment.mocked
        ? MockCompendiumService
        : ApiCompendiumService,
    },
    {
      provide: ChatService,
      useClass: environment.mocked ? MockChatService : ApiChatService,
    },
    {
      provide: WorldBuildingSessionService,
      useClass: environment.mocked
        ? MockWorldBuildingSessionService
        : ApiWorldBuildingSessionService,
    },
    {
      provide: IntegrationsService,
      useClass: environment.mocked
        ? MockIntegrationsService
        : ApiIntegrationsService,
    },
    {
      provide: GenerateTextService,
      useClass: environment.mocked
        ? MockGenerateTextService
        : ApiGenerateTextService,
    },
    {
      provide: GenerateImageService,
      useClass: environment.mocked
        ? MockGenerateImageService
        : ApiGenerateImageService,
    },
    {
      provide: GenerateVideoService,
      useClass: environment.mocked
        ? MockGenerateVideoService
        : ApiGenerateVideoService,
    },
    {
      provide: GenerateAudioService,
      useClass: environment.mocked
        ? MockGenerateAudioService
        : ApiGenerateAudioService,
    },
    {
      provide: VoiceService,
      useClass: environment.mocked ? MockVoiceService : ApiVoiceService,
    },
    {
      provide: MediaLibraryService,
      useClass: environment.mocked
        ? MockMediaLibraryService
        : ApiMediaLibraryService,
    },
    provideToastr(),
    provideMarkdown(),
    providePrimeNG({
      theme: {
        preset: primeNgTheme,
        options: {
          darkModeSelector: '.mnb-dark',
        },
      },
    }),
  ],
};
