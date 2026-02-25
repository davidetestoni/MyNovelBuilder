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
import { GenerateAudioService } from './services/generate-audio.service';
import { ApiGenerateAudioService } from './services/api-generate-audio.service';
import { MockGenerateAudioService } from './services/mock-generate-audio.service';
import { VoiceService } from './services/voice.service';
import { ApiVoiceService } from './services/api-voice.service';
import { MockVoiceService } from './services/mock-voice.service';

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
      provide: GenerateAudioService,
      useClass: environment.mocked
        ? MockGenerateAudioService
        : ApiGenerateAudioService,
    },
    {
      provide: VoiceService,
      useClass: environment.mocked ? MockVoiceService : ApiVoiceService,
    },
    provideToastr(),
    provideMarkdown(),
    providePrimeNG({
      theme: {
        preset: primeNgTheme,
      },
    }),
  ],
};
