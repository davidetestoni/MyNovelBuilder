import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CompendiumService } from '../../services/compendium.service';
import { FormsModule } from '@angular/forms';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { TitleCasePipe } from '@angular/common';
import { CompendiumRecordDto } from '../../types/dtos/compendium-record/compendium-record.dto';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import {
  GenerateMediaComponent,
  GenerateMediaComponentData,
} from '../generate-media/generate-media.component';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { SelectModule } from 'primeng/select';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { AliasSuggestionsComponent } from '../alias-suggestions/alias-suggestions.component';
import { CodeEditorComponent } from '../code-editor/code-editor.component';
import {
  EditImageComponent,
  EditImageComponentData,
} from '../edit-image/edit-image.component';
import { HttpClient } from '@angular/common/http';
import { CompendiumRecordMediaDto } from '../../types/dtos/compendium-record/compendium-record-media.dto';
import { TooltipModule } from 'primeng/tooltip';
import { ToastrService } from 'ngx-toastr';
import { readImageFileFromClipboard } from '../../utils/clipboard-image';
import { createGeneratedMediaFile } from '../../utils/generated-media';
import {
  ImageSourceSelectorComponent,
  ImageSourceSelectorComponentData,
} from '../image-source-selector/image-source-selector.component';
import { GenerateAudioService } from '../../services/generate-audio.service';
import { IntegrationsService } from '../../services/integrations.service';
import { TtsProvider } from '../../types/enums/tts-provider';
import { TtsModelDto } from '../../types/dtos/generate/tts-model.dto';
import { CharacterVoiceAssignmentDto } from '../../types/dtos/compendium-record/character-voice-assignment.dto';
import { StreamingWavPlayer } from '../../utils/streaming-wav-player';
import { WritingLanguage } from '../../types/enums/writing-language';
import { firstValueFrom } from 'rxjs';

@Component({
  selector: 'app-compendium-record',
  standalone: true,
  imports: [
    FormsModule,
    TitleCasePipe,
    InputTextModule,
    TextareaModule,
    ButtonModule,
    CheckboxModule,
    SelectModule,
    ConfirmDialogModule,
    TooltipModule,
    AliasSuggestionsComponent,
    CodeEditorComponent,
  ],
  providers: [DialogService, ConfirmationService],
  templateUrl: './compendium-record.component.html',
  styleUrl: './compendium-record.component.scss',
})
export class CompendiumRecordComponent {
  private readonly portraitImageSize = { width: 832, height: 1248 };
  private readonly landscapeImageSize = { width: 1248, height: 832 };

  @Input() record!: CompendiumRecordDto;
  @Input() compendiumId!: string;
  @Output() updateRecord = new EventEmitter<CompendiumRecordDto>();
  @Output() deleteRecord = new EventEmitter<CompendiumRecordDto>();
  readonly compendiumService: CompendiumService = inject(CompendiumService);
  private dialogService = inject(DialogService);
  private confirmationService = inject(ConfirmationService);
  private http = inject(HttpClient);
  private toastr = inject(ToastrService);
  private generateAudioService = inject(GenerateAudioService);
  private integrationsService = inject(IntegrationsService);
  private dialogRef: DynamicDialogRef | null = null;
  protected readonly TtsProvider = TtsProvider;

  protected selectedVoiceProvider: TtsProvider | null = null;
  protected availableVoiceModels: TtsModelDto[] = [];
  protected selectedVoiceModelId = '';
  protected selectedVoiceId = '';
  protected isLoadingVoiceModels = false;
  protected isPreviewingVoice = false;
  protected previewingVoiceKey: string | null = null;
  private previewPlayer: StreamingWavPlayer | null = null;

  recordTypes: CompendiumRecordType[] = [
    CompendiumRecordType.Character,
    CompendiumRecordType.Place,
    CompendiumRecordType.Object,
    CompendiumRecordType.Event,
    CompendiumRecordType.Concept,
    CompendiumRecordType.Other,
  ];

  CompendiumRecordType = CompendiumRecordType;

  get voiceProviderOptions(): { label: string; value: TtsProvider }[] {
    return Object.values(TtsProvider).map((provider) => ({
      label: this.formatTtsProviderLabel(provider),
      value: provider,
    }));
  }

  get voiceModelOptions(): { label: string; value: string }[] {
    return this.availableVoiceModels.map((model) => ({
      label: model.name,
      value: model.modelId,
    }));
  }

  get voiceOptions(): { label: string; value: string }[] {
    return (
      this.availableVoiceModels
        .find((model) => model.modelId === this.selectedVoiceModelId)
        ?.voices.map((voice) => ({
          label: voice.name,
          value: voice.voiceId,
        })) ?? []
    );
  }

  ngOnInit(): void {
    this.record.characterVoiceAssignments ??= [];

    if (this.record.type !== CompendiumRecordType.Character) {
      return;
    }

    this.integrationsService.getIntegrationsConfig().subscribe({
      next: (config) => {
        const matchingAssignment = this.record.characterVoiceAssignments.find(
          (assignment) =>
            assignment.provider === config.ttsProvider &&
            assignment.modelId === config.ttsModelId,
        );

        this.selectedVoiceProvider = config.ttsProvider;
        this.loadVoiceModels(
          config.ttsProvider,
          matchingAssignment?.modelId ?? config.ttsModelId,
          matchingAssignment?.voiceId,
        );
      },
      error: (error) => {
        console.error('Error loading integrations config:', error);
      },
    });
  }

  addAlias(alias: string): void {
    const currentAliasesValue = this.record.aliases || '';
    const currentAliases = currentAliasesValue
      .split(',')
      .map((a) => a.trim())
      .filter((a) => a.length > 0);

    if (!currentAliases.some((a) => a.toLowerCase() === alias.toLowerCase())) {
      currentAliases.push(alias);
      this.record.aliases = currentAliases.join(', ');
      this.updateRecord.emit(this.record);
    }
  }

  ngOnDestroy(): void {
    this.previewPlayer?.stop();

    if (this.dialogRef) {
      this.dialogRef.close();
    }
  }

  onBlur(): void {
    this.updateRecord.emit(this.record);
  }

  protected onVoiceProviderChange(): void {
    if (!this.selectedVoiceProvider) {
      this.availableVoiceModels = [];
      this.selectedVoiceModelId = '';
      this.selectedVoiceId = '';
      return;
    }

    const currentAssignment = this.record.characterVoiceAssignments.find(
      (assignment) => assignment.provider === this.selectedVoiceProvider,
    );

    this.loadVoiceModels(
      this.selectedVoiceProvider,
      currentAssignment?.modelId,
      currentAssignment?.voiceId,
    );
  }

  protected onVoiceModelChange(): void {
    const selectedModel = this.availableVoiceModels.find(
      (model) => model.modelId === this.selectedVoiceModelId,
    );
    this.selectedVoiceId = selectedModel?.voices[0]?.voiceId ?? '';
  }

  protected saveCharacterVoiceAssignment(): void {
    if (
      this.record.type !== CompendiumRecordType.Character ||
      !this.selectedVoiceProvider ||
      !this.selectedVoiceModelId ||
      !this.selectedVoiceId
    ) {
      return;
    }

    const selectedVoiceName =
      this.availableVoiceModels
        .find((model) => model.modelId === this.selectedVoiceModelId)
        ?.voices.find((voice) => voice.voiceId === this.selectedVoiceId)?.name ??
      null;

    const nextAssignment: CharacterVoiceAssignmentDto = {
      provider: this.selectedVoiceProvider,
      modelId: this.selectedVoiceModelId,
      voiceId: this.selectedVoiceId,
      voiceName: selectedVoiceName,
      updatedAt: new Date().toISOString(),
    };

    this.record.characterVoiceAssignments = [
      ...this.record.characterVoiceAssignments.filter(
        (assignment) =>
          !(
            assignment.provider === nextAssignment.provider &&
            assignment.modelId === nextAssignment.modelId
          ),
      ),
      nextAssignment,
    ].sort((a, b) =>
      `${a.provider}:${a.modelId}`.localeCompare(`${b.provider}:${b.modelId}`),
    );

    this.onBlur();
  }

  protected async previewSelectedVoice(): Promise<void> {
    if (
      !this.selectedVoiceProvider ||
      !this.selectedVoiceModelId ||
      !this.selectedVoiceId
    ) {
      this.toastr.error('Please select a TTS voice first.');
      return;
    }

    const language = this.getVoiceLanguage(
      this.availableVoiceModels,
      this.selectedVoiceModelId,
      this.selectedVoiceId,
    );

    await this.previewVoice(
      this.selectedVoiceProvider,
      this.selectedVoiceModelId,
      this.selectedVoiceId,
      language,
    );
  }

  protected async previewCharacterVoiceAssignment(
    assignment: CharacterVoiceAssignmentDto,
  ): Promise<void> {
    const language = await this.resolveVoiceLanguage(
      assignment.provider,
      assignment.modelId,
      assignment.voiceId,
    );

    await this.previewVoice(
      assignment.provider,
      assignment.modelId,
      assignment.voiceId,
      language,
    );
  }

  protected isPreviewingAssignment(
    assignment: CharacterVoiceAssignmentDto,
  ): boolean {
    return (
      this.isPreviewingVoice &&
      this.previewingVoiceKey ===
        this.getVoicePreviewKey(
          assignment.provider,
          assignment.modelId,
          assignment.voiceId,
        )
    );
  }

  protected get isPreviewingSelectedVoice(): boolean {
    return (
      this.isPreviewingVoice &&
      this.previewingVoiceKey ===
        this.getVoicePreviewKey(
          this.selectedVoiceProvider,
          this.selectedVoiceModelId,
          this.selectedVoiceId,
        )
    );
  }

  protected removeCharacterVoiceAssignment(
    assignment: CharacterVoiceAssignmentDto,
  ): void {
    this.record.characterVoiceAssignments =
      this.record.characterVoiceAssignments.filter(
        (item) =>
          !(
            item.provider === assignment.provider &&
            item.modelId === assignment.modelId
          ),
      );
    this.onBlur();
  }

  setCurrentImage(imageId: string): void {
    this.record.media.forEach((image) => {
      image.isCurrent = image.id === imageId;
    });
    this.compendiumService
      .setCurrentRecordImage(this.record.id, imageId)
      .subscribe();
  }

  removeMedia(mediaId: string): void {
    this.confirmationService.confirm({
      message:
        'Are you sure you want to delete this media? This action cannot be undone.',
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.record.media = this.record.media.filter(
          (media) => media.id !== mediaId,
        );
        this.compendiumService
          .deleteRecordMedia(this.record.id, mediaId)
          .subscribe();
      },
    });
  }

  removeRecord(): void {
    this.confirmationService.confirm({
      message:
        'Are you sure you want to delete this record? This action cannot be undone.',
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.deleteRecord.emit(this.record);
      },
    });
  }

  openAddMediaDialog(): void {
    this.dialogRef = this.dialogService.open(ImageSourceSelectorComponent, {
      header: 'Add Media',
      width: '300px',
      modal: true,
      closable: true,
      dismissableMask: true,
      closeOnEscape: true,
      data: <ImageSourceSelectorComponentData>{
        uploadLabel: 'Upload Image',
        generateLabel: 'Generate Media',
        clipboardLabel: 'Paste from Clipboard',
      },
    });

    this.dialogRef?.onClose.subscribe(
      (result: 'upload' | 'generate' | 'clipboard' | undefined) => {
        if (result === 'upload') {
          this.addMedia();
        } else if (result === 'generate') {
          this.generateImage();
        } else if (result === 'clipboard') {
          void this.addClipboardImage();
        }
      },
    );
  }

  private addMedia(): void {
    const fileInput = document.createElement('input');
    fileInput.type = 'file';
    fileInput.accept = 'image/*,video/*';
    fileInput.onchange = () => {
      if (fileInput.files && fileInput.files.length > 0) {
        const file = fileInput.files[0];
        this.compendiumService
          .uploadRecordMedia(
            this.record.id,
            file,
            this.record.media.length === 0,
          )
          .subscribe(() => {
            this.refreshRecordMedia();
            fileInput.remove();
          });
      }
    };
    fileInput.click();
  }

  private async addClipboardImage(): Promise<void> {
    try {
      const file = await readImageFileFromClipboard();
      this.compendiumService
        .uploadRecordMedia(this.record.id, file, this.record.media.length === 0)
        .subscribe(() => {
          this.refreshRecordMedia();
        });
    } catch (error) {
      this.toastr.error(
        error instanceof Error
          ? error.message
          : 'Failed to read image from clipboard.',
      );
    }
  }

  private generateImage() {
    const imageDimensions = this.getImageDimensionsForRecordType();

    this.dialogRef = this.dialogService.open(GenerateMediaComponent, {
      header: 'Generate Media',
      width: '50vw',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      closable: true,
      closeOnEscape: true,
      modal: true,
      dismissableMask: true,
      data: <GenerateMediaComponentData>{
        enablePromptGeneration: true,
        compendiumId: this.compendiumId,
        compendiumRecordId: this.record.id,
        width: imageDimensions.width,
        height: imageDimensions.height,
      },
    });

    this.dialogRef?.onClose.subscribe((media: Blob) => {
      if (media) {
        const file = createGeneratedMediaFile(media);
        this.compendiumService
          .uploadRecordMedia(
            this.record.id,
            file,
            this.record.media.length === 0,
          )
          .subscribe(() => {
            // Get the record and update the media
            this.compendiumService
              .getRecord(this.record.id)
              .subscribe((record) => {
                this.record.media = record.media;
                this.updateRecord.emit(this.record);
              });
          });
      }
    });
  }

  private loadVoiceModels(
    provider: TtsProvider,
    preferredModelId?: string,
    preferredVoiceId?: string,
  ): void {
    this.isLoadingVoiceModels = true;

    this.generateAudioService.getAvailableModels(provider).subscribe({
      next: (models) => {
        this.availableVoiceModels = models;
        this.selectedVoiceModelId =
          (preferredModelId &&
            models.some((model) => model.modelId === preferredModelId) &&
            preferredModelId) ||
          models[0]?.modelId ||
          '';

        const selectedModel = models.find(
          (model) => model.modelId === this.selectedVoiceModelId,
        );
        this.selectedVoiceId =
          (preferredVoiceId &&
            selectedModel?.voices.some(
              (voice) => voice.voiceId === preferredVoiceId,
            ) &&
            preferredVoiceId) ||
          selectedModel?.voices[0]?.voiceId ||
          '';
        this.isLoadingVoiceModels = false;
      },
      error: (error) => {
        console.error('Error loading TTS models for character voices:', error);
        this.availableVoiceModels = [];
        this.selectedVoiceModelId = '';
        this.selectedVoiceId = '';
        this.isLoadingVoiceModels = false;
      },
    });
  }

  protected formatTtsProviderLabel(provider: TtsProvider): string {
    return provider
      .replace(/([A-Z])/g, ' $1')
      .replace(/^./, (str) => str.toUpperCase())
      .replace('Api', 'API')
      .replace('Gpt', 'GPT');
  }

  private async previewVoice(
    provider: TtsProvider,
    modelId: string,
    voiceId: string,
    language: WritingLanguage,
  ): Promise<void> {
    if (this.isPreviewingVoice) {
      return;
    }

    const timerLabel = `Character voice preview (${provider}:${modelId}:${voiceId})`;

    this.isPreviewingVoice = true;
    this.previewingVoiceKey = this.getVoicePreviewKey(provider, modelId, voiceId);
    this.previewPlayer?.stop();
    this.previewPlayer = new StreamingWavPlayer();

    try {
      console.time(timerLabel);

      const response = await this.generateAudioService.textToSpeechStreamResponse({
        message: this.getPreviewSampleText(language),
        modelId,
        voiceId,
        provider,
      });

      const stream = response.body;
      if (!stream) {
        this.toastr.error('No audio stream was returned.');
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
      console.error('Character voice preview streaming error:', error);
      this.toastr.error(
        'Could not preview voice. Please verify your TTS configuration.',
      );
    } finally {
      console.timeEnd(timerLabel);
      this.isPreviewingVoice = false;
      this.previewingVoiceKey = null;
    }
  }

  private async resolveVoiceLanguage(
    provider: TtsProvider,
    modelId: string,
    voiceId: string,
  ): Promise<WritingLanguage> {
    if (this.selectedVoiceProvider === provider) {
      return this.getVoiceLanguage(this.availableVoiceModels, modelId, voiceId);
    }

    try {
      const models = await firstValueFrom(
        this.generateAudioService.getAvailableModels(provider),
      );

      return this.getVoiceLanguage(models, modelId, voiceId);
    } catch (error) {
      console.error('Error loading TTS models for character voice preview:', error);
      return WritingLanguage.English;
    }
  }

  private getVoiceLanguage(
    models: TtsModelDto[],
    modelId: string,
    voiceId: string,
  ): WritingLanguage {
    return (
      models
        .find((model) => model.modelId === modelId)
        ?.voices.find((voice) => voice.voiceId === voiceId)?.language ??
      WritingLanguage.English
    );
  }

  private getVoicePreviewKey(
    provider: TtsProvider | null,
    modelId: string,
    voiceId: string,
  ): string {
    return `${provider ?? ''}:${modelId}:${voiceId}`;
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

  editImage(media: CompendiumRecordMediaDto) {
    if (media.isVideo) return;

    this.http.get(media.url, { responseType: 'blob' }).subscribe({
      next: (blob) => {
        const file = new File([blob], 'image.png', { type: blob.type });

        this.dialogRef = this.dialogService.open(EditImageComponent, {
          header: 'Edit Image',
          width: '70vw',
          contentStyle: { overflow: 'auto' },
          baseZIndex: 10000,
          closable: true,
          closeOnEscape: true,
          modal: true,
          dismissableMask: true,
          data: <EditImageComponentData>{
            image: file,
            width: this.getImageDimensionsForRecordType().width,
            height: this.getImageDimensionsForRecordType().height,
          },
        });

        this.dialogRef?.onClose.subscribe((editedImage: Blob) => {
          if (editedImage) {
            this.compendiumService
              .uploadRecordMedia(this.record.id, editedImage, media.isCurrent)
              .subscribe(() => {
                this.refreshRecordMedia();
              });
          }
        });
      },
      error: (err) => {
        console.error('Failed to download image', err);
      },
    });
  }

  private refreshRecordMedia(): void {
    this.compendiumService.getRecord(this.record.id).subscribe((record) => {
      this.record.media = record.media;
      this.updateRecord.emit(this.record);
    });
  }

  private getImageDimensionsForRecordType(): {
    width: number;
    height: number;
  } {
    if (this.record.type === CompendiumRecordType.Place) {
      return this.landscapeImageSize;
    }

    return this.portraitImageSize;
  }
}
