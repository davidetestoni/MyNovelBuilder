import { Component, OnInit, inject } from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { IntegrationsService } from '../../services/integrations.service';
import {
  IntegrationsConfigDto,
  UpdateIntegrationsConfigDto,
} from '../../types/dtos/integrations/integrations-config.dto';
import { ToastrService } from 'ngx-toastr';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { PasswordModule } from 'primeng/password';
import { TtsProvider } from '../../types/enums/tts-provider';
import { SelectModule } from 'primeng/select';
import { TextGenerationProvider } from '../../types/enums/text-generation-provider';
import { ImageGenerationProvider } from '../../types/enums/image-generation-provider';
import { GenerateAudioService } from '../../services/generate-audio.service';
import { TtsVoiceDto } from '../../types/dtos/generate/tts-voice.dto';

@Component({
  selector: 'app-integrations',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    InputTextModule,
    ButtonModule,
    PasswordModule,
    SelectModule,
  ],
  templateUrl: './integrations.component.html',
  styleUrl: './integrations.component.scss',
})
export class IntegrationsComponent implements OnInit {
  protected readonly TextGenerationProvider = TextGenerationProvider;
  protected readonly TtsProvider = TtsProvider;
  protected readonly ImageGenerationProvider = ImageGenerationProvider;

  private integrationsService = inject(IntegrationsService);
  private generateAudioService = inject(GenerateAudioService);
  private toastrService = inject(ToastrService);

  integrationsForm = new FormGroup({
    textGenerationProvider: new FormControl<TextGenerationProvider>(
      TextGenerationProvider.OpenRouter,
    ),
    openRouterApiKey: new FormControl<string>('', Validators.maxLength(1000)),
    googleGenAiApiKey: new FormControl<string>('', Validators.maxLength(1000)),
    ttsProvider: new FormControl<TtsProvider>(TtsProvider.Custom),
    elevenLabsApiKey: new FormControl<string>('', Validators.maxLength(1000)),
    unrealSpeechApiKey: new FormControl<string>('', Validators.maxLength(1000)),
    ttsVoiceId: new FormControl<string>(''),
    imageGenerationProvider: new FormControl<ImageGenerationProvider>(
      ImageGenerationProvider.DeApi,
    ),
    deApiApiKey: new FormControl<string>('', Validators.maxLength(1000)),
  });
  hasOpenRouterApiKey: boolean = false;
  hasGoogleGenAiApiKey: boolean = false;
  hasElevenLabsApiKey: boolean = false;
  hasUnrealSpeechApiKey: boolean = false;
  hasDeApiApiKey: boolean = false;

  ttsProviderOptions = Object.values(TtsProvider).map((provider) => ({
    // camelCase to spaced Pascal Case for display
    label: provider
      .replace(/([A-Z])/g, ' $1')
      .replace(/^./, (str) => str.toUpperCase()),
    value: provider,
  }));

  ttsVoiceOptions: { label: string; value: string }[] = [];

  textGenerationProviderOptions = Object.values(TextGenerationProvider).map(
    (provider) => ({
      // camelCase to spaced Pascal Case for display
      label: provider
        .replace(/([A-Z])/g, ' $1')
        .replace(/^./, (str) => str.toUpperCase())
        .replace('Api', 'API')
        .replace('Ai', 'AI'),
      value: provider,
    }),
  );

  imageGenerationProviderOptions = Object.values(ImageGenerationProvider).map(
    (provider) => ({
      // camelCase to spaced Pascal Case for display
      label: provider
        .replace(/([A-Z])/g, ' $1')
        .replace(/^./, (str) => str.toUpperCase())
        .replace('Api', 'API'),
      value: provider,
    }),
  );

  ngOnInit(): void {
    this.integrationsForm.controls.ttsProvider.valueChanges.subscribe(
      (provider) => {
        if (provider) {
          this.loadTtsVoices(provider);
        }
      },
    );

    this.integrationsService.getIntegrationsConfig().subscribe({
      next: (config: IntegrationsConfigDto) => {
        this.hasOpenRouterApiKey = config.hasOpenRouterApiKey;
        this.hasGoogleGenAiApiKey = config.hasGoogleGenAiApiKey;
        this.hasElevenLabsApiKey = config.hasElevenLabsApiKey;
        this.hasUnrealSpeechApiKey = config.hasUnrealSpeechApiKey;
        this.hasDeApiApiKey = config.hasDeApiApiKey;
        this.integrationsForm.patchValue({
          textGenerationProvider: config.textGenerationProvider,
          ttsProvider: config.ttsProvider,
          imageGenerationProvider: config.imageGenerationProvider,
        });

        this.loadTtsVoices(config.ttsProvider, config.ttsVoiceId);
      },
      error: (error) => {
        this.toastrService.error('Failed to load integrations configuration.');
        console.error('Error loading configuration:', error);
      },
    });
  }

  loadTtsVoices(provider: TtsProvider, selectedVoiceId?: string): void {
    this.generateAudioService.getAvailableVoices(provider).subscribe({
      next: (voices: TtsVoiceDto[]) => {
        this.ttsVoiceOptions = voices.map((v) => ({
          label: v.name,
          value: v.voiceId,
        }));

        if (selectedVoiceId) {
          this.integrationsForm.patchValue({ ttsVoiceId: selectedVoiceId });
        } else if (
          voices.length > 0 &&
          !this.ttsVoiceOptions.find(
            (v) => v.value === this.integrationsForm.value.ttsVoiceId,
          )
        ) {
          // If the current value is not in the new list, select the first one or reset
          this.integrationsForm.patchValue({ ttsVoiceId: voices[0].voiceId });
        }
      },
      error: (error) => {
        console.error('Error loading TTS voices:', error);
        this.toastrService.error('Failed to load TTS voices.');
        this.ttsVoiceOptions = [];
      },
    });
  }

  onSubmit(): void {
    if (!this.integrationsForm.valid) {
      return;
    }

    const updateDto: UpdateIntegrationsConfigDto = {
      textGenerationProvider:
        this.integrationsForm.value.textGenerationProvider,
      openRouterApiKey:
        this.integrationsForm.value.openRouterApiKey || undefined,
      googleGenAiApiKey:
        this.integrationsForm.value.googleGenAiApiKey || undefined,
      elevenLabsApiKey:
        this.integrationsForm.value.elevenLabsApiKey || undefined,
      unrealSpeechApiKey:
        this.integrationsForm.value.unrealSpeechApiKey || undefined,
      deApiApiKey: this.integrationsForm.value.deApiApiKey || undefined,
      ttsProvider: this.integrationsForm.value.ttsProvider,
      ttsVoiceId: this.integrationsForm.value.ttsVoiceId,
      imageGenerationProvider:
        this.integrationsForm.value.imageGenerationProvider,
    };

    this.integrationsService.updateIntegrationsConfig(updateDto).subscribe({
      next: () => {
        if (this.integrationsForm.value.openRouterApiKey) {
          this.hasOpenRouterApiKey = true;
          this.integrationsForm.get('openRouterApiKey')?.reset();
        }
        if (this.integrationsForm.value.googleGenAiApiKey) {
          this.hasGoogleGenAiApiKey = true;
          this.integrationsForm.get('googleGenAiApiKey')?.reset();
        }
        if (this.integrationsForm.value.elevenLabsApiKey) {
          this.hasElevenLabsApiKey = true;
          this.integrationsForm.get('elevenLabsApiKey')?.reset();
        }
        if (this.integrationsForm.value.unrealSpeechApiKey) {
          this.hasUnrealSpeechApiKey = true;
          this.integrationsForm.get('unrealSpeechApiKey')?.reset();
        }
        if (this.integrationsForm.value.deApiApiKey) {
          this.hasDeApiApiKey = true;
          this.integrationsForm.get('deApiApiKey')?.reset();
        }
        this.toastrService.success(
          'Integrations configuration updated successfully.',
        );
      },
      error: (error) => {
        this.toastrService.error(
          'Failed to update integrations configuration.',
        );
        console.error('Error updating configuration:', error);
      },
    });
  }
}
