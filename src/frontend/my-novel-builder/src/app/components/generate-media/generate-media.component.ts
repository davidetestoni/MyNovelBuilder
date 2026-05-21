import { HttpEvent, HttpEventType } from '@angular/common/http';
import { Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import {
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { ToastrModule, ToastrService } from 'ngx-toastr';
import { ButtonModule } from 'primeng/button';
import {
  DialogService,
  DynamicDialogConfig,
  DynamicDialogRef,
} from 'primeng/dynamicdialog';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { LocalStorageService } from '../../services/local-storage.service';
import { PromptService } from '../../services/prompt.service';
import { GenerateImageService } from '../../services/generate-image.service';
import { GenerateVideoService } from '../../services/generate-video.service';
import {
  CompendiumTextGenerationType,
  CreateCompendiumRecordImageGenerationPromptContextInfoDto,
  GenerateTextRequestDto,
} from '../../types/dtos/generate/generate-text-request.dto';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { PromptType } from '../../types/enums/prompt-type';
import {
  extractImageFileFromClipboardData,
  readImageFileFromClipboard,
} from '../../utils/clipboard-image';
import { isVideoMimeType } from '../../utils/generated-media';
import {
  GenerateTextComponent,
  GenerateTextComponentData,
} from '../generate-text/generate-text.component';
import {
  GenerateTextResultComponent,
  GenerateTextResultComponentData,
} from '../generate-text-result/generate-text-result.component';
import { ModelSelectComponent } from '../model-select/model-select.component';

type GenerationMode = 'image' | 'textToVideo' | 'imageToVideo';

export interface GenerateMediaComponentData {
  enablePromptGeneration?: boolean;
  compendiumId?: string;
  compendiumRecordId?: string;
  width?: number;
  height?: number;
}

@Component({
  selector: 'app-generate-media',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    ToastrModule,
    TextareaModule,
    ButtonModule,
    SelectModule,
    ModelSelectComponent,
  ],
  templateUrl: './generate-media.component.html',
  styleUrl: './generate-media.component.scss',
})
export class GenerateMediaComponent implements OnInit, OnDestroy {
  private readonly defaultWidth = 832;
  private readonly defaultHeight = 1248;
  private generationTimerId: ReturnType<typeof setInterval> | null = null;
  private generationStartedAt: number | null = null;
  private generatedPreviewObjectUrl: string | null = null;
  private sourceImagePreviewObjectUrl: string | null = null;

  readonly generationModeOptions: { label: string; value: GenerationMode }[] = [
    { label: 'Image', value: 'image' },
    { label: 'Text to Video', value: 'textToVideo' },
    { label: 'Image to Video', value: 'imageToVideo' },
  ];

  dialogRef = inject(DynamicDialogRef);
  config = inject(DynamicDialogConfig);

  readonly generateImageService = inject(GenerateImageService);
  readonly generateVideoService = inject(GenerateVideoService);
  readonly localStorageService = inject(LocalStorageService);
  readonly toastrService = inject(ToastrService);
  readonly sanitizer = inject(DomSanitizer);
  readonly promptService = inject(PromptService);
  readonly dialogService = inject(DialogService);

  data: GenerateMediaComponentData = {};
  promptGenerationPrompts: PromptDto[] = [];
  promptGenerationDialogRef: DynamicDialogRef | null = null;
  isLoadingPromptGenerationPrompts = false;

  formGroup = new FormGroup({
    prompt: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    model: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    mode: new FormControl<GenerationMode>('image', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  generatedBlob: Blob | null = null;
  generatedPreview: SafeUrl | null = null;
  sourceImageFile: File | null = null;
  sourceImagePreview: SafeUrl | null = null;
  isGenerating = false;
  isPastingSourceImage = false;
  generationElapsedSeconds = 0;
  lastGenerationDurationSeconds: number | null = null;

  constructor() {
    this.data = (this.config.data ?? {}) as GenerateMediaComponentData;
    this.restoreStoredPromptForCurrentMode();
  }

  ngOnInit(): void {
    if (this.isPromptGenerationEnabled()) {
      this.getPromptGenerationPrompts();
    }

    this.formGroup.controls.mode.valueChanges.subscribe(() => {
      this.clearGeneratedMedia();
      this.restoreStoredPromptForCurrentMode();

      if (this.selectedMode !== 'imageToVideo') {
        this.clearSourceImage();
      }
    });
  }

  ngOnDestroy(): void {
    this.stopGenerationTimer();
    this.promptGenerationDialogRef?.close();
    this.clearGeneratedMedia();
    this.clearSourceImage();
  }

  get selectedMode(): GenerationMode {
    return this.formGroup.controls.mode.getRawValue();
  }

  get selectedCapability(): 'imageGeneration' | 'textToVideo' | 'imageToVideo' {
    switch (this.selectedMode) {
      case 'textToVideo':
        return 'textToVideo';
      case 'imageToVideo':
        return 'imageToVideo';
      case 'image':
      default:
        return 'imageGeneration';
    }
  }

  get outputLabel(): string {
    return this.selectedMode === 'image' ? 'image' : 'video';
  }

  get generateButtonLabel(): string {
    if (this.isGenerating) {
      return `Generating ${this.outputLabel} (${this.generationElapsedSeconds}s)`;
    }

    switch (this.selectedMode) {
      case 'textToVideo':
        return 'Generate Video';
      case 'imageToVideo':
        return 'Generate from Image';
      case 'image':
      default:
        return 'Generate Image';
    }
  }

  get generationStatusLabel(): string | null {
    if (this.isGenerating || this.lastGenerationDurationSeconds === null) {
      return null;
    }

    return `${this.capitalize(this.outputLabel)} generation took ${this.lastGenerationDurationSeconds}s`;
  }

  get promptPlaceholder(): string {
    switch (this.selectedMode) {
      case 'textToVideo':
        return 'Describe the video you want to generate...';
      case 'imageToVideo':
        return 'Describe how the uploaded image should move or evolve...';
      case 'image':
      default:
        return 'Describe the image you want to generate...';
    }
  }

  get hasGeneratedVideo(): boolean {
    return isVideoMimeType(this.generatedBlob?.type);
  }

  canGeneratePrompt(): boolean {
    return (
      this.isPromptGenerationEnabled() && this.promptGenerationPrompts.length > 0
    );
  }

  generateMedia(): void {
    if (this.formGroup.invalid) {
      this.toastrService.error('Please fill out all fields');
      return;
    }

    const { prompt, model } = this.formGroup.getRawValue();

    if (!prompt.trim() || !model.trim()) {
      this.toastrService.error('Please fill out all fields');
      return;
    }

    if (this.selectedMode === 'imageToVideo' && this.sourceImageFile === null) {
      this.toastrService.error('Please upload a source image first.');
      return;
    }

    this.persistPromptAndModel(prompt, model);

    this.isGenerating = true;
    this.lastGenerationDurationSeconds = null;
    this.startGenerationTimer();
    this.clearGeneratedMedia();

    const request = {
      modelId: model,
      prompt,
      width: this.data.width ?? this.defaultWidth,
      height: this.data.height ?? this.defaultHeight,
    };

    const generationRequest =
      this.selectedMode === 'image'
        ? this.generateImageService.generateImage(request)
        : this.selectedMode === 'textToVideo'
          ? this.generateVideoService.generateVideo(request)
          : this.generateVideoService.generateVideoFromImage(
              this.sourceImageFile!,
              request,
            );

    generationRequest.subscribe({
      next: (event: HttpEvent<Blob>) => {
        if (event.type === HttpEventType.Response && event.body !== null) {
          this.setGeneratedMedia(event.body);
        }
      },
      error: (err) => {
        console.error('Media generation failed', err);
        this.isGenerating = false;
        this.stopGenerationTimer();
      },
      complete: () => {
        this.isGenerating = false;
        this.stopGenerationTimer();
      },
    });
  }

  accept(): void {
    this.dialogRef.close(this.generatedBlob);
  }

  @HostListener('paste', ['$event'])
  onPaste(event: ClipboardEvent): void {
    if (this.selectedMode !== 'imageToVideo') {
      return;
    }

    const file = extractImageFileFromClipboardData(
      event.clipboardData?.items,
      'source-image',
    );
    if (file === null) {
      return;
    }

    event.preventDefault();
    this.applySourceImage(file);
  }

  onSourceImageSelected(event: Event): void {
    const input = event.target;
    if (!(input instanceof HTMLInputElement)) {
      return;
    }

    const file = input.files?.[0] ?? null;
    if (file === null) {
      return;
    }

    this.clearGeneratedMedia();
    this.setSourceImage(file);
    input.value = '';
  }

  clearSelectedSourceImage(): void {
    this.clearGeneratedMedia();
    this.clearSourceImage();
  }

  async pasteSourceImageFromClipboard(): Promise<void> {
    if (this.selectedMode !== 'imageToVideo' || this.isPastingSourceImage) {
      return;
    }

    this.isPastingSourceImage = true;

    try {
      const file = await readImageFileFromClipboard('source-image');
      this.applySourceImage(file);
    } catch (error) {
      console.error('Failed to read clipboard image', error);
      this.toastrService.error(
        error instanceof Error
          ? error.message
          : 'Clipboard image paste failed.',
      );
    } finally {
      this.isPastingSourceImage = false;
    }
  }

  openGeneratePromptDialog(): void {
    if (!this.canGeneratePrompt()) {
      return;
    }

    const { compendiumId, compendiumRecordId } = this.data;
    if (!compendiumId || !compendiumRecordId) {
      this.toastrService.error('Missing compendium context for prompt generation');
      return;
    }

    this.promptGenerationDialogRef = this.dialogService.open(
      GenerateTextComponent,
      {
        header: 'Generate Prompt',
        width: '50vw',
        contentStyle: { overflow: 'auto' },
        baseZIndex: 11000,
        modal: true,
        closable: true,
        closeOnEscape: true,
        dismissableMask: true,
        data: <GenerateTextComponentData>{
          prompts: this.promptGenerationPrompts,
          contextInfo: <CreateCompendiumRecordImageGenerationPromptContextInfoDto>{
            $type:
              CompendiumTextGenerationType.CreateCompendiumRecordImageGenerationPrompt,
            compendiumId: compendiumId,
            compendiumRecordId: compendiumRecordId,
            instructions: null,
          },
          instructionsRequired: false,
          showInstructions: true,
        },
      },
    );

    this.promptGenerationDialogRef?.onClose.subscribe(
      (request: GenerateTextRequestDto) => {
        if (request) {
          this.openGeneratePromptResultDialog(request);
        }
      },
    );
  }

  isPromptGenerationEnabled(): boolean {
    return (
      this.data.enablePromptGeneration === true &&
      !!this.data.compendiumId &&
      !!this.data.compendiumRecordId
    );
  }

  private get modelStorageContext(): string {
    switch (this.selectedMode) {
      case 'textToVideo':
        return 'generate-video';
      case 'imageToVideo':
        return 'generate-image-to-video';
      case 'image':
      default:
        return 'generate';
    }
  }

  private restoreStoredPromptForCurrentMode(): void {
    const prompt = this.isVideoMode()
      ? this.localStorageService.getNestedStringForKey(
          LocalStorageKey.LastVideoPromptByContext,
          this.modelStorageContext,
        ) ?? this.localStorageService.getStringForKey(LocalStorageKey.LastVideoPrompt)
      : this.localStorageService.getNestedStringForKey(
          LocalStorageKey.LastImagePromptByContext,
          this.modelStorageContext,
        ) ?? this.localStorageService.getStringForKey(LocalStorageKey.LastImagePrompt);

    if (prompt !== null && prompt.trim() !== '') {
      this.formGroup.patchValue({ prompt });
      this.formGroup.markAsDirty();
    }
  }

  private persistPromptAndModel(prompt: string, model: string): void {
    if (this.isVideoMode()) {
      this.localStorageService.setNestedStringForKey(
        LocalStorageKey.LastVideoPromptByContext,
        this.modelStorageContext,
        prompt,
      );
      this.localStorageService.setStringForKey(LocalStorageKey.LastVideoPrompt, prompt);
      this.localStorageService.setNestedStringForKey(
        LocalStorageKey.LastVideoModelByContext,
        this.modelStorageContext,
        model,
      );
      this.localStorageService.setStringForKey(LocalStorageKey.LastVideoModel, model);
      return;
    }

    this.localStorageService.setNestedStringForKey(
      LocalStorageKey.LastImagePromptByContext,
      this.modelStorageContext,
      prompt,
    );
    this.localStorageService.setStringForKey(LocalStorageKey.LastImagePrompt, prompt);
    this.localStorageService.setNestedStringForKey(
      LocalStorageKey.LastImageModelByContext,
      this.modelStorageContext,
      model,
    );
    this.localStorageService.setStringForKey(LocalStorageKey.LastImageModel, model);
  }

  private isVideoMode(): boolean {
    return this.selectedMode === 'textToVideo' || this.selectedMode === 'imageToVideo';
  }

  private setGeneratedMedia(blob: Blob): void {
    if (this.generatedPreviewObjectUrl !== null) {
      URL.revokeObjectURL(this.generatedPreviewObjectUrl);
    }

    this.generatedBlob = blob;
    this.generatedPreviewObjectUrl = URL.createObjectURL(blob);
    this.generatedPreview = this.sanitizer.bypassSecurityTrustUrl(
      this.generatedPreviewObjectUrl,
    );
  }

  private clearGeneratedMedia(): void {
    if (this.generatedPreviewObjectUrl !== null) {
      URL.revokeObjectURL(this.generatedPreviewObjectUrl);
      this.generatedPreviewObjectUrl = null;
    }

    this.generatedBlob = null;
    this.generatedPreview = null;
  }

  private setSourceImage(file: File): void {
    this.clearSourceImage();
    this.sourceImageFile = file;
    this.sourceImagePreviewObjectUrl = URL.createObjectURL(file);
    this.sourceImagePreview = this.sanitizer.bypassSecurityTrustUrl(
      this.sourceImagePreviewObjectUrl,
    );
  }

  private applySourceImage(file: File): void {
    this.clearGeneratedMedia();
    this.setSourceImage(file);
  }

  private clearSourceImage(): void {
    if (this.sourceImagePreviewObjectUrl !== null) {
      URL.revokeObjectURL(this.sourceImagePreviewObjectUrl);
      this.sourceImagePreviewObjectUrl = null;
    }

    this.sourceImageFile = null;
    this.sourceImagePreview = null;
  }

  private openGeneratePromptResultDialog(request: GenerateTextRequestDto): void {
    this.promptGenerationDialogRef = this.dialogService.open(
      GenerateTextResultComponent,
      {
        header: 'Generate Prompt',
        width: '50vw',
        contentStyle: { overflow: 'auto' },
        baseZIndex: 11000,
        modal: true,
        closable: true,
        closeOnEscape: true,
        dismissableMask: true,
        data: <GenerateTextResultComponentData>{
          request,
          textToReplace: '',
        },
      },
    );

    this.promptGenerationDialogRef?.onClose.subscribe(
      (result: string | 'back' | undefined) => {
        if (result === 'back') {
          this.openGeneratePromptDialog();
        } else if (result && result.trim() !== '') {
          this.formGroup.patchValue({ prompt: result.trim() });
          this.formGroup.markAsDirty();
        }
      },
    );
  }

  private getPromptGenerationPrompts(): void {
    this.isLoadingPromptGenerationPrompts = true;

    this.promptService.getPrompts().subscribe({
      next: (prompts) => {
        this.promptGenerationPrompts = prompts.filter(
          (p) =>
            p.type === PromptType.CreateCompendiumRecordImageGenerationPrompt,
        );

        if (this.promptGenerationPrompts.length === 0) {
          this.toastrService.warning(
            'No prompts are available for image prompt generation',
          );
        }
      },
      error: () => {
        this.toastrService.error('Failed to load prompt-generation prompts');
      },
      complete: () => {
        this.isLoadingPromptGenerationPrompts = false;
      },
    });
  }

  private startGenerationTimer(): void {
    this.stopGenerationTimer();
    this.generationStartedAt = Date.now();
    this.generationElapsedSeconds = 0;
    this.generationTimerId = setInterval(() => {
      if (this.generationStartedAt === null) {
        return;
      }

      this.generationElapsedSeconds = Math.floor(
        (Date.now() - this.generationStartedAt) / 1000,
      );
    }, 1000);
  }

  private stopGenerationTimer(): void {
    if (this.generationTimerId !== null) {
      clearInterval(this.generationTimerId);
      this.generationTimerId = null;
    }

    if (this.generationStartedAt !== null) {
      this.lastGenerationDurationSeconds = Math.floor(
        (Date.now() - this.generationStartedAt) / 1000,
      );
      this.generationElapsedSeconds = this.lastGenerationDurationSeconds;
      this.generationStartedAt = null;
    }
  }

  private capitalize(value: string): string {
    return value.length === 0
      ? value
      : `${value[0].toUpperCase()}${value.slice(1)}`;
  }
}
