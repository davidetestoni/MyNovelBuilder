import { TitleCasePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { GenerateAudioService } from '../../services/generate-audio.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { VoiceService } from '../../services/voice.service';
import { TtsProviderDto } from '../../types/dtos/generate/tts-provider.dto';
import { VoiceDto } from '../../types/dtos/voice/voice.dto';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { TtsProvider } from '../../types/enums/tts-provider';
import { VoiceGender } from '../../types/enums/voice-gender';
import { WritingLanguage } from '../../types/enums/writing-language';
import { StreamingWavPlayer } from '../../utils/streaming-wav-player';

export interface VoiceDialogData {
  mode: 'create' | 'edit';
  voice?: VoiceDto;
}

interface VoiceDesignDraft {
  provider: TtsProvider | null;
  language: WritingLanguage;
  prompt: string;
  voiceDescription: string;
}

@Component({
  selector: 'app-voice-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    InputTextModule,
    SelectModule,
    ButtonModule,
    DialogModule,
    TextareaModule,
    TitleCasePipe,
  ],
  templateUrl: './voice-dialog.component.html',
  styleUrl: './voice-dialog.component.scss',
})
export class VoiceDialogComponent implements OnInit, OnDestroy {
  private dialogRef = inject(DynamicDialogRef);
  private config = inject(DynamicDialogConfig);
  private voiceService = inject(VoiceService);
  private generateAudioService = inject(GenerateAudioService);
  private localStorageService = inject(LocalStorageService);
  private toastr = inject(ToastrService);
  private previewPlayer: StreamingWavPlayer | null = null;

  protected readonly data = (this.config.data || { mode: 'create' }) as VoiceDialogData;
  protected selectedFileName = '';
  protected isVoiceDesignDialogVisible = false;
  protected isGeneratingDesignedVoice = false;
  protected isPreviewingDesignedVoice = false;
  protected availableVoiceDesignProviders: TtsProviderDto[] = [];
  protected generatedVoiceSample: Blob | null = null;
  protected generatedVoiceSampleFileName = '';

  protected readonly voiceGenderOptions = [
    { label: 'Both', value: VoiceGender.Both },
    { label: 'Male', value: VoiceGender.Male },
    { label: 'Female', value: VoiceGender.Female },
  ];

  protected readonly languageOptions = Object.values(WritingLanguage);

  protected readonly voiceDesignFormGroup = new FormGroup({
    provider: new FormControl<TtsProvider | null>(null, [Validators.required]),
    language: new FormControl<WritingLanguage>(WritingLanguage.English, [Validators.required]),
    prompt: new FormControl('', [Validators.required, Validators.maxLength(50_000)]),
    voiceDescription: new FormControl('', [Validators.required, Validators.maxLength(2_000)]),
  });

  protected readonly formGroup = new FormGroup({
    name: new FormControl('', [Validators.required, Validators.maxLength(100)]),
    voiceGender: new FormControl<VoiceGender>(VoiceGender.Both, [Validators.required]),
    language: new FormControl<WritingLanguage>(WritingLanguage.English, [Validators.required]),
    file: new FormControl<File | null>(null, [Validators.required]),
  });

  constructor() {
    if (this.data.mode === 'edit' && this.data.voice) {
      this.formGroup.patchValue({
        name: this.data.voice.name,
        voiceGender: this.data.voice.voiceGender,
        language: this.data.voice.language,
      });
    }
  }

  ngOnInit(): void {
    this.restoreVoiceDesignDraft();
    this.generateAudioService.getAvailableProviders().subscribe({
      next: (providers) => {
        this.availableVoiceDesignProviders = providers.filter((provider) => provider.supportsVoiceDesign);
        const currentProvider = this.voiceDesignFormGroup.controls.provider.value;
        const nextProvider = this.availableVoiceDesignProviders.some(
          (provider) => provider.provider === currentProvider,
        )
          ? currentProvider
          : (this.availableVoiceDesignProviders[0]?.provider ?? null);

        this.voiceDesignFormGroup.patchValue({
          provider: nextProvider,
          language:
            this.voiceDesignFormGroup.controls.language.value ??
            this.formGroup.controls.language.value ??
            WritingLanguage.English,
        }, { emitEvent: false });
      },
      error: (error) => {
        console.error('Error loading TTS providers:', error);
        this.availableVoiceDesignProviders = [];
      },
    });

    this.voiceDesignFormGroup.valueChanges.subscribe(() => {
      this.persistVoiceDesignDraft();
    });
  }

  ngOnDestroy(): void {
    this.previewPlayer?.stop();
  }

  protected get voiceDesignProviderOptions(): { label: string; value: TtsProvider }[] {
    return this.availableVoiceDesignProviders.map((provider) => ({
      label: this.formatTtsProviderLabel(provider.provider),
      value: provider.provider,
    }));
  }

  protected get currentVoiceDesignHint(): string | null {
    switch (this.voiceDesignFormGroup.controls.provider.value) {
      case TtsProvider.OmniVoice:
        return 'Valid English items: american accent, australian accent, british accent, canadian accent, child, chinese accent, elderly, female, high pitch, indian accent, japanese accent, korean accent, low pitch, male, middle-aged, moderate pitch, portuguese accent, russian accent, teenager, very high pitch, very low pitch, whisper, young adult';
      default:
        return null;
    }
  }

  protected openVoiceDesignDialog(): void {
    this.generatedVoiceSample = null;
    this.generatedVoiceSampleFileName = '';
    this.voiceDesignFormGroup.patchValue(
      {
        language: this.formGroup.controls.language.value ?? WritingLanguage.English,
        provider: this.voiceDesignFormGroup.controls.provider.value ?? this.availableVoiceDesignProviders[0]?.provider ?? null,
      },
      { emitEvent: false },
    );
    this.isVoiceDesignDialogVisible = true;
  }

  protected closeVoiceDesignDialog(): void {
    this.previewPlayer?.stop();
    this.isPreviewingDesignedVoice = false;
    this.isVoiceDesignDialogVisible = false;
  }

  protected generateDesignedVoice(): void {
    if (this.voiceDesignFormGroup.invalid || this.isGeneratingDesignedVoice) {
      return;
    }

    const provider = this.voiceDesignFormGroup.controls.provider.value;
    const language = this.voiceDesignFormGroup.controls.language.value ?? WritingLanguage.English;
    const prompt = this.voiceDesignFormGroup.controls.prompt.value?.trim() ?? '';
    const voiceDescription = this.voiceDesignFormGroup.controls.voiceDescription.value?.trim() ?? '';

    if (!provider || !prompt || !voiceDescription) {
      return;
    }

    this.isGeneratingDesignedVoice = true;

    this.generateAudioService.voiceDesign({
      provider,
      prompt,
      language,
      voiceDescription,
    }).subscribe({
      next: (audioBlob) => {
        const fileName = `${provider}-voice-design.wav`;
        this.generatedVoiceSample = audioBlob;
        this.generatedVoiceSampleFileName = fileName;
        this.isGeneratingDesignedVoice = false;
        this.formGroup.controls.language.setValue(language);
        this.toastr.success('Voice sample generated. Preview it, then apply it if it sounds right.');
      },
      error: (error) => {
        console.error('Voice design generation error:', error);
        this.isGeneratingDesignedVoice = false;
        this.toastr.error('Could not generate the voice sample.');
      },
    });
  }

  protected async previewDesignedVoice(): Promise<void> {
    if (!this.generatedVoiceSample || this.isPreviewingDesignedVoice) {
      return;
    }

    this.isPreviewingDesignedVoice = true;
    this.previewPlayer?.stop();
    this.previewPlayer = new StreamingWavPlayer();

    try {
      const stream = this.generatedVoiceSample.stream();
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
      console.error('Designed voice preview error:', error);
      this.toastr.error('Could not preview the generated voice sample.');
    } finally {
      this.isPreviewingDesignedVoice = false;
    }
  }

  protected applyDesignedVoice(): void {
    if (!this.generatedVoiceSample) {
      return;
    }

    const designedVoiceFile = new File(
      [this.generatedVoiceSample],
      this.generatedVoiceSampleFileName || 'designed-voice.wav',
      { type: 'audio/wav' },
    );
    this.formGroup.controls.file.setValue(designedVoiceFile);
    this.formGroup.controls.file.markAsDirty();
    this.selectedFileName = designedVoiceFile.name;
    this.closeVoiceDesignDialog();
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;

    if (file === null) {
      this.formGroup.controls.file.setValue(null);
      this.selectedFileName = '';
      return;
    }

    if (!file.name.toLowerCase().endsWith('.wav')) {
      this.formGroup.controls.file.setValue(null);
      input.value = '';
      this.selectedFileName = '';
      this.toastr.error('Only .wav files are allowed.');
      return;
    }

    this.formGroup.controls.file.setValue(file);
    this.selectedFileName = file.name;
    this.formGroup.controls.file.markAsDirty();
  }

  protected submit(): void {
    if (this.formGroup.invalid) {
      return;
    }

    const name = this.formGroup.controls.name.value?.trim() ?? '';
    const voiceGender = this.formGroup.controls.voiceGender.value ?? VoiceGender.Both;
    const language = this.formGroup.controls.language.value ?? WritingLanguage.English;
    const file = this.formGroup.controls.file.value;

    if (!name || file === null) {
      return;
    }

    if (this.data.mode === 'edit' && this.data.voice) {
      this.voiceService
        .updateVoice(this.data.voice.id, name, voiceGender, language, file)
        .subscribe(() => {
          this.toastr.success('Voice updated successfully.');
          this.dialogRef.close(true);
        });
      return;
    }

    this.voiceService.createVoice(name, voiceGender, language, file).subscribe(() => {
      this.toastr.success('Voice created successfully.');
      this.dialogRef.close(true);
    });
  }

  protected cancel(): void {
    this.dialogRef.close();
  }

  private formatTtsProviderLabel(provider: TtsProvider): string {
    return provider
      .replace(/([A-Z])/g, ' $1')
      .replace(/^./, (str) => str.toUpperCase())
      .replace('Api', 'API')
      .replace('Gpt', 'GPT');
  }

  private restoreVoiceDesignDraft(): void {
    const draft = this.localStorageService.getObjectForKey<VoiceDesignDraft>(
      LocalStorageKey.VoiceDesignDraft,
    );

    if (!draft) {
      return;
    }

    this.voiceDesignFormGroup.patchValue({
      provider: draft.provider,
      language: draft.language,
      prompt: draft.prompt,
      voiceDescription: draft.voiceDescription,
    }, { emitEvent: false });
  }

  private persistVoiceDesignDraft(): void {
    this.localStorageService.setObjectForKey<VoiceDesignDraft>(
      LocalStorageKey.VoiceDesignDraft,
      {
        provider: this.voiceDesignFormGroup.controls.provider.value,
        language:
          this.voiceDesignFormGroup.controls.language.value ??
          WritingLanguage.English,
        prompt: this.voiceDesignFormGroup.controls.prompt.value ?? '',
        voiceDescription: this.voiceDesignFormGroup.controls.voiceDescription.value ?? '',
      },
    );
  }
}
