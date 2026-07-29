import { Component, OnDestroy, OnInit, inject } from '@angular/core';
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
import { CheckboxModule } from 'primeng/checkbox';
import { InputGroup } from 'primeng/inputgroup';
import { InputGroupAddon } from 'primeng/inputgroupaddon';
import { TextGenerationProvider } from '../../types/enums/text-generation-provider';
import { ImageGenerationProvider } from '../../types/enums/image-generation-provider';
import { VideoGenerationProvider } from '../../types/enums/video-generation-provider';
import { GenerateAudioService } from '../../services/generate-audio.service';
import { TtsModelDto } from '../../types/dtos/generate/tts-model.dto';
import { WritingLanguage } from '../../types/enums/writing-language';
import { GenerateTextService } from '../../services/generate-text.service';
import { TextGenerationModelInfoDto } from '../../types/dtos/generate/text-generation-model-info.dto';
import {
  STREAMING_WAV_PLAYER_FACTORY,
  StreamingWavPlayerHandle,
} from '../../utils/streaming-wav-player.factory';

@Component({
  selector: 'app-integrations',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    InputTextModule,
    ButtonModule,
    PasswordModule,
    SelectModule,
    CheckboxModule,
    InputGroup,
    InputGroupAddon,
  ],
  templateUrl: './integrations.component.html',
  styleUrl: './integrations.component.scss',
})
export class IntegrationsComponent implements OnInit, OnDestroy {
  protected readonly TextGenerationProvider = TextGenerationProvider;
  protected readonly TtsProvider = TtsProvider;
  protected readonly ImageGenerationProvider = ImageGenerationProvider;
  protected readonly VideoGenerationProvider = VideoGenerationProvider;

  private integrationsService = inject(IntegrationsService);
  private generateAudioService = inject(GenerateAudioService);
  private generateTextService = inject(GenerateTextService);
  private toastrService = inject(ToastrService);
  private createStreamingWavPlayer = inject(STREAMING_WAV_PLAYER_FACTORY);

  integrationsForm = new FormGroup({
    textGenerationProvider: new FormControl<TextGenerationProvider>(
      TextGenerationProvider.OpenRouter,
    ),
    textGenerationModelId: new FormControl<string>(''),
    openRouterApiKey: new FormControl<string>('', Validators.maxLength(1000)),
    googleGenAiApiKey: new FormControl<string>('', Validators.maxLength(1000)),
    ttsProvider: new FormControl<TtsProvider>(TtsProvider.Custom),
    elevenLabsApiKey: new FormControl<string>('', Validators.maxLength(1000)),
    unrealSpeechApiKey: new FormControl<string>('', Validators.maxLength(1000)),
    nanoGptApiKey: new FormControl<string>('', Validators.maxLength(1000)),
    customTtsBaseUrl: new FormControl<string>('', Validators.maxLength(2000)),
    pocketTtsBaseUrl: new FormControl<string>('', Validators.maxLength(2000)),
    vibeVoiceBaseUrl: new FormControl<string>('', Validators.maxLength(2000)),
    chatterboxBaseUrl: new FormControl<string>('', Validators.maxLength(2000)),
    qwen3BaseUrl: new FormControl<string>('', Validators.maxLength(2000)),
    omniVoiceBaseUrl: new FormControl<string>('', Validators.maxLength(2000)),
    ttsModelId: new FormControl<string>(''),
    ttsVoiceId: new FormControl<string>(''),
    ttsEnableTextEmphasis: new FormControl<boolean>(false),
    ttsEnableImmersive: new FormControl<boolean>(false),
    ttsImmersivePauseMs: new FormControl<number>(150),
    imageGenerationProvider: new FormControl<ImageGenerationProvider>(
      ImageGenerationProvider.DeApi,
    ),
    videoGenerationProvider: new FormControl<VideoGenerationProvider>(
      VideoGenerationProvider.DeApi,
    ),
    deApiApiKey: new FormControl<string>('', Validators.maxLength(1000)),
  });
  hasOpenRouterApiKey: boolean = false;
  hasGoogleGenAiApiKey: boolean = false;
  hasElevenLabsApiKey: boolean = false;
  hasUnrealSpeechApiKey: boolean = false;
  hasDeApiApiKey: boolean = false;
  hasNanoGptApiKey: boolean = false;
  deApiBalanceUsd: number | null = null;
  nanoGptBalanceUsd: number | null = null;
  isLoadingDeApiBalance: boolean = false;
  isLoadingNanoGptBalance: boolean = false;
  isPreviewingTtsVoice: boolean = false;
  private previewPlayer: StreamingWavPlayerHandle | null = null;

  ttsProviderOptions = Object.values(TtsProvider).map((provider) => ({
    // camelCase to spaced Pascal Case for display
    label: provider
      .replace(/([A-Z])/g, ' $1')
      .replace(/^./, (str) => str.toUpperCase())
      .replace('Gpt', 'GPT'),
    value: provider,
  }));

  availableTtsModels: TtsModelDto[] = [];
  availableTextGenerationModels: TextGenerationModelInfoDto[] = [];

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
        .replace('Api', 'API')
        .replace('Gpt', 'GPT'),
      value: provider,
    }),
  );

  videoGenerationProviderOptions = Object.values(VideoGenerationProvider).map(
    (provider) => ({
      label: provider
        .replace(/([A-Z])/g, ' $1')
        .replace(/^./, (str) => str.toUpperCase())
        .replace('Api', 'API'),
      value: provider,
    }),
  );

  ngOnInit(): void {
    this.integrationsForm.controls.textGenerationProvider.valueChanges.subscribe(
      (provider) => {
        if (provider) {
          this.loadTextGenerationModels(provider);
        }
      },
    );

    this.integrationsForm.controls.ttsProvider.valueChanges.subscribe(
      (provider) => {
        if (provider) {
          this.loadTtsModels(provider);
        }
      },
    );

    this.integrationsForm.controls.ttsModelId.valueChanges.subscribe((modelId) => {
      this.ensureValidTtsVoiceSelection(modelId ?? undefined);
    });

    this.integrationsService.getIntegrationsConfig().subscribe({
      next: (config: IntegrationsConfigDto) => {
        this.hasOpenRouterApiKey = config.hasOpenRouterApiKey;
        this.hasGoogleGenAiApiKey = config.hasGoogleGenAiApiKey;
        this.hasElevenLabsApiKey = config.hasElevenLabsApiKey;
        this.hasUnrealSpeechApiKey = config.hasUnrealSpeechApiKey;
        this.hasDeApiApiKey = config.hasDeApiApiKey;
        this.hasNanoGptApiKey = config.hasNanoGptApiKey;
        this.integrationsForm.patchValue({
          textGenerationProvider: config.textGenerationProvider,
          textGenerationModelId: config.textGenerationModelId,
          ttsProvider: config.ttsProvider,
          customTtsBaseUrl: config.customTtsBaseUrl,
          pocketTtsBaseUrl: config.pocketTtsBaseUrl,
          vibeVoiceBaseUrl: config.vibeVoiceBaseUrl,
          chatterboxBaseUrl: config.chatterboxBaseUrl,
          qwen3BaseUrl: config.qwen3BaseUrl,
          omniVoiceBaseUrl: config.omniVoiceBaseUrl,
          ttsEnableTextEmphasis: config.ttsEnableTextEmphasis,
          ttsEnableImmersive: config.ttsEnableImmersive,
          ttsImmersivePauseMs: config.ttsImmersivePauseMs,
          imageGenerationProvider: config.imageGenerationProvider,
          videoGenerationProvider: config.videoGenerationProvider,
        }, { emitEvent: false });

        this.loadTextGenerationModels(
          config.textGenerationProvider,
          config.textGenerationModelId,
        );
        this.loadTtsModels(
          config.ttsProvider,
          config.ttsModelId,
          config.ttsVoiceId,
        );
        this.loadConfiguredBalances();
      },
      error: (error) => {
        console.error('Error loading configuration:', error);
      },
    });
  }

  ngOnDestroy(): void {
    this.previewPlayer?.stop();
  }

  protected get ttsModelOptions(): { label: string; value: string }[] {
    return this.availableTtsModels.map((model) => ({
      label: model.name,
      value: model.modelId,
    }));
  }

  protected get textGenerationModelOptions(): { label: string; value: string }[] {
    return this.generateTextService
      .sortModels(this.availableTextGenerationModels.map((model) => model.id))
      .map((modelId) => ({
        label: modelId,
        value: modelId,
      }));
  }

  protected get ttsVoiceOptions(): {
    label: string;
    value: string;
    language: WritingLanguage;
  }[] {
    return this.getSelectedModel()?.voices.map((voice) => ({
      label: voice.name,
      value: voice.voiceId,
      language: voice.language,
    })) ?? [];
  }

  protected get supportsSelectedTtsModelTextEmphasis(): boolean {
    return this.getSelectedModel()?.supportsTextEmphasis ?? false;
  }

  protected get selectedTtsProviderBaseUrlControl():
    | FormControl<string | null>
    | null {
    switch (this.integrationsForm.value.ttsProvider) {
      case TtsProvider.Custom:
        return this.integrationsForm.controls.customTtsBaseUrl;
      case TtsProvider.PocketTts:
        return this.integrationsForm.controls.pocketTtsBaseUrl;
      case TtsProvider.VibeVoice:
        return this.integrationsForm.controls.vibeVoiceBaseUrl;
      case TtsProvider.Chatterbox:
        return this.integrationsForm.controls.chatterboxBaseUrl;
      case TtsProvider.Qwen3:
        return this.integrationsForm.controls.qwen3BaseUrl;
      case TtsProvider.OmniVoice:
        return this.integrationsForm.controls.omniVoiceBaseUrl;
      default:
        return null;
    }
  }

  protected get selectedTtsProviderBaseUrlLabel(): string | null {
    switch (this.integrationsForm.value.ttsProvider) {
      case TtsProvider.Custom:
        return 'Custom TTS';
      case TtsProvider.PocketTts:
        return 'Pocket TTS';
      case TtsProvider.VibeVoice:
        return 'VibeVoice';
      case TtsProvider.Chatterbox:
        return 'Chatterbox';
      case TtsProvider.Qwen3:
        return 'Qwen3';
      case TtsProvider.OmniVoice:
        return 'OmniVoice';
      default:
        return null;
    }
  }

  protected get selectedTtsProviderBaseUrlPlaceholder(): string {
    switch (this.integrationsForm.value.ttsProvider) {
      case TtsProvider.Custom:
        return 'http://localhost:5000/';
      case TtsProvider.PocketTts:
      case TtsProvider.VibeVoice:
      case TtsProvider.Chatterbox:
      case TtsProvider.Qwen3:
      case TtsProvider.OmniVoice:
        return 'http://localhost:8000/';
      default:
        return 'http://localhost:8000/';
    }
  }

  private loadTtsModels(
    provider: TtsProvider,
    selectedModelId?: string,
    selectedVoiceId?: string,
  ): void {
    this.generateAudioService.getAvailableModels(provider).subscribe({
      next: (models: TtsModelDto[]) => {
        this.availableTtsModels = models;
        const nextModelId =
          selectedModelId ||
          this.resolveModelIdForVoice(selectedVoiceId) ||
          this.integrationsForm.value.ttsModelId ||
          this.availableTtsModels[0]?.modelId ||
          '';

        this.integrationsForm.patchValue(
          {
            ttsModelId: this.isValidModelId(nextModelId) ? nextModelId : '',
          },
          { emitEvent: false },
        );

        this.ensureValidTtsVoiceSelection(
          this.integrationsForm.value.ttsModelId ?? undefined,
          selectedVoiceId,
        );
        this.syncTtsEmphasisToggle();
      },
      error: (error) => {
        console.error('Error loading TTS voices:', error);
        this.availableTtsModels = [];
        this.integrationsForm.patchValue(
          {
            ttsModelId: '',
            ttsVoiceId: '',
            ttsEnableTextEmphasis: false,
          },
          { emitEvent: false },
        );
      },
    });
  }

  private loadTextGenerationModels(
    provider: TextGenerationProvider,
    selectedModelId?: string,
  ): void {
    this.generateTextService.getAvailableModelInfos(provider).subscribe({
      next: (models) => {
        this.availableTextGenerationModels = models.filter(
          (model) => model.supportsStructuredOutputs,
        );

        const nextModelId =
          selectedModelId ||
          this.integrationsForm.value.textGenerationModelId ||
          this.availableTextGenerationModels[0]?.id ||
          '';

        this.integrationsForm.patchValue(
          {
            textGenerationModelId: this.isValidTextGenerationModelId(nextModelId)
              ? nextModelId
              : '',
          },
          { emitEvent: false },
        );
      },
      error: (error) => {
        console.error('Error loading text generation models:', error);
        this.availableTextGenerationModels = [];
        this.integrationsForm.patchValue(
          {
            textGenerationModelId: '',
          },
          { emitEvent: false },
        );
      },
    });
  }

  loadConfiguredBalances(): void {
    if (this.hasDeApiApiKey) {
      this.loadDeApiBalance();
    } else {
      this.deApiBalanceUsd = null;
    }

    if (this.hasNanoGptApiKey) {
      this.loadNanoGptBalance();
    } else {
      this.nanoGptBalanceUsd = null;
    }
  }

  loadDeApiBalance(): void {
    this.isLoadingDeApiBalance = true;

    this.generateAudioService.getBalanceUsd(TtsProvider.DeApi).subscribe({
      next: (balance) => {
        this.deApiBalanceUsd = balance;
        this.isLoadingDeApiBalance = false;
      },
      error: (error) => {
        console.error('Error loading DeAPI balance:', error);
        this.deApiBalanceUsd = null;
        this.isLoadingDeApiBalance = false;
      },
    });
  }

  loadNanoGptBalance(): void {
    this.isLoadingNanoGptBalance = true;

    this.generateAudioService.getBalanceUsd(TtsProvider.NanoGpt).subscribe({
      next: (balance) => {
        this.nanoGptBalanceUsd = balance;
        this.isLoadingNanoGptBalance = false;
      },
      error: (error) => {
        console.error('Error loading NanoGPT balance:', error);
        this.nanoGptBalanceUsd = null;
        this.isLoadingNanoGptBalance = false;
      },
    });
  }

  async previewTtsVoice(): Promise<void> {
    const timerLabel = 'TTS voice preview';

    if (this.isPreviewingTtsVoice) {
      return;
    }

    if (!this.integrationsForm.value.ttsVoiceId) {
      this.toastrService.error('Please select a TTS voice first.');
      return;
    }

    this.isPreviewingTtsVoice = true;
    this.previewPlayer?.stop();
    this.previewPlayer = this.createStreamingWavPlayer();

    try {
      console.time(timerLabel);
      const previewMessage = this.getPreviewSampleTextForSelectedVoice();

      const response =
        await this.generateAudioService.textToSpeechStreamResponse({
          message: previewMessage,
          modelId: this.integrationsForm.value.ttsModelId ?? undefined,
          voiceId: this.integrationsForm.value.ttsVoiceId ?? undefined,
          provider: this.integrationsForm.value.ttsProvider ?? undefined,
        });

      const stream = response.body;
      if (!stream) {
        this.toastrService.error('No audio stream was returned.');
        return;
      }

      const reader = stream.getReader();

      while (true) {
        const { done, value } = await reader.read();
        if (done) {
          break;
        }
        if (value) {
          this.previewPlayer.addChunk(value);
        }
      }
    } catch (error) {
      console.error('WAV preview streaming error:', error);
      this.toastrService.error(
        'Could not preview voice. Please verify your TTS configuration.',
      );
    } finally {
      console.timeEnd(timerLabel);
      this.isPreviewingTtsVoice = false;
    }
  }

  private getPreviewSampleTextForSelectedVoice(): string {
    const selectedVoiceId = this.integrationsForm.value.ttsVoiceId;
    const language =
      this.getSelectedModel()
        ?.voices.find((voice) => voice.voiceId === selectedVoiceId)?.language ??
      WritingLanguage.English;

    return this.getPreviewSampleText(language);
  }

  private ensureValidTtsVoiceSelection(
    selectedModelId?: string,
    preferredVoiceId?: string,
  ): void {
    const voices = this.getSelectedModel(selectedModelId)?.voices ?? [];
    const currentVoiceId = preferredVoiceId ?? this.integrationsForm.value.ttsVoiceId;
    const nextVoiceId = voices.some((voice) => voice.voiceId === currentVoiceId)
      ? currentVoiceId
      : (voices[0]?.voiceId ?? '');

    this.integrationsForm.patchValue(
      {
        ttsVoiceId: nextVoiceId,
      },
      { emitEvent: false },
    );

    this.syncTtsEmphasisToggle(selectedModelId);
  }

  private resolveModelIdForVoice(selectedVoiceId?: string): string | undefined {
    if (!selectedVoiceId) {
      return undefined;
    }

    return this.availableTtsModels.find((model) =>
      model.voices.some((voice) => voice.voiceId === selectedVoiceId),
    )?.modelId;
  }

  private getSelectedModel(selectedModelId?: string): TtsModelDto | undefined {
    const modelId = selectedModelId ?? this.integrationsForm.value.ttsModelId ?? '';
    return (
      this.availableTtsModels.find((model) => model.modelId === modelId) ??
      this.availableTtsModels[0]
    );
  }

  private syncTtsEmphasisToggle(selectedModelId?: string): void {
    const supportsTextEmphasis =
      this.getSelectedModel(selectedModelId)?.supportsTextEmphasis ?? false;

    if (!supportsTextEmphasis && this.integrationsForm.value.ttsEnableTextEmphasis) {
      this.integrationsForm.patchValue(
        {
          ttsEnableTextEmphasis: false,
        },
        { emitEvent: false },
      );
    }
  }

  private isValidModelId(modelId?: string | null): modelId is string {
    return !!modelId && this.availableTtsModels.some((model) => model.modelId === modelId);
  }

  private isValidTextGenerationModelId(modelId?: string | null): modelId is string {
    return !!modelId &&
      this.availableTextGenerationModels.some((model) => model.id === modelId);
  }

  private getPreviewSampleText(language: WritingLanguage): string {
    switch (language) {
      case WritingLanguage.Italian:
        return "Ciao, questo è un breve esempio per ascoltare l'anteprima della voce selezionata.";
      case WritingLanguage.French:
        return 'Bonjour, ceci est un court exemple pour prévisualiser la voix sélectionnée.';
      case WritingLanguage.Spanish:
        return 'Hola, este es un ejemplo rápido para previsualizar la voz seleccionada.';
      case WritingLanguage.German:
        return 'Hallo, dies ist ein kurzes Beispiel, um die ausgewählte Stimme vorzuhören.';
      case WritingLanguage.Russian:
        return 'Привет, это короткий пример для предварительного прослушивания выбранного голоса.';
      case WritingLanguage.English:
      default:
        return 'Hello, this is a quick sample to preview the selected voice.';
    }
  }

  onSubmit(): void {
    if (!this.integrationsForm.valid) {
      return;
    }

    const updateDto: UpdateIntegrationsConfigDto = {
      textGenerationProvider:
        this.integrationsForm.value.textGenerationProvider,
      textGenerationModelId:
        this.integrationsForm.value.textGenerationModelId || undefined,
      openRouterApiKey:
        this.integrationsForm.value.openRouterApiKey || undefined,
      googleGenAiApiKey:
        this.integrationsForm.value.googleGenAiApiKey || undefined,
      elevenLabsApiKey:
        this.integrationsForm.value.elevenLabsApiKey || undefined,
      unrealSpeechApiKey:
        this.integrationsForm.value.unrealSpeechApiKey || undefined,
      nanoGptApiKey: this.integrationsForm.value.nanoGptApiKey || undefined,
      deApiApiKey: this.integrationsForm.value.deApiApiKey || undefined,
      customTtsBaseUrl: this.integrationsForm.value.customTtsBaseUrl ?? undefined,
      pocketTtsBaseUrl: this.integrationsForm.value.pocketTtsBaseUrl ?? undefined,
      vibeVoiceBaseUrl: this.integrationsForm.value.vibeVoiceBaseUrl ?? undefined,
      chatterboxBaseUrl: this.integrationsForm.value.chatterboxBaseUrl ?? undefined,
      qwen3BaseUrl: this.integrationsForm.value.qwen3BaseUrl ?? undefined,
      omniVoiceBaseUrl: this.integrationsForm.value.omniVoiceBaseUrl ?? undefined,
      ttsProvider: this.integrationsForm.value.ttsProvider,
      ttsModelId: this.integrationsForm.value.ttsModelId,
      ttsVoiceId: this.integrationsForm.value.ttsVoiceId,
      ttsEnableTextEmphasis:
        this.supportsSelectedTtsModelTextEmphasis
          ? (this.integrationsForm.value.ttsEnableTextEmphasis ?? false)
          : false,
      ttsEnableImmersive:
        this.integrationsForm.value.ttsEnableImmersive ?? false,
      ttsImmersivePauseMs:
        this.integrationsForm.value.ttsImmersivePauseMs ?? 150,
      imageGenerationProvider:
        this.integrationsForm.value.imageGenerationProvider,
      videoGenerationProvider:
        this.integrationsForm.value.videoGenerationProvider,
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
        if (this.integrationsForm.value.nanoGptApiKey) {
          this.hasNanoGptApiKey = true;
          this.integrationsForm.get('nanoGptApiKey')?.reset();
        }
        if (this.integrationsForm.value.deApiApiKey) {
          this.hasDeApiApiKey = true;
          this.integrationsForm.get('deApiApiKey')?.reset();
        }
        this.toastrService.success(
          'Integrations configuration updated successfully.',
        );
        if (this.integrationsForm.value.ttsProvider) {
          this.loadTtsModels(
            this.integrationsForm.value.ttsProvider,
            this.integrationsForm.value.ttsModelId ?? undefined,
            this.integrationsForm.value.ttsVoiceId ?? undefined,
          );
        }
        this.loadConfiguredBalances();
      },
      error: (error) => {
        console.error('Error updating configuration:', error);
      },
    });
  }

  protected formatUsdBalance(balance: number | null): string {
    if (balance === null) {
      return '$0.00';
    }

    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(balance);
  }
}
