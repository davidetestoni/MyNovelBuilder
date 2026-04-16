import {
  Component,
  Input,
  OnChanges,
  OnInit,
  SimpleChanges,
  forwardRef,
  inject,
} from '@angular/core';
import { ControlValueAccessor, FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
import { SelectModule } from 'primeng/select';
import { GenerateTextService } from '../../services/generate-text.service';
import { GenerateImageService } from '../../services/generate-image.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { ImageGenerationModelInfoDto } from '../../types/dtos/generate/image-generation-model-info.dto';
import { TextGenerationModelInfoDto } from '../../types/dtos/generate/text-generation-model-info.dto';

type ModelCapability =
  | 'text'
  | 'vision'
  | 'structuredOutput'
  | 'imageGeneration'
  | 'imageEdit';

@Component({
  selector: 'app-model-select',
  standalone: true,
  imports: [FormsModule, SelectModule],
  templateUrl: './model-select.component.html',
  styleUrl: './model-select.component.scss',
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => ModelSelectComponent),
      multi: true,
    },
  ],
})
export class ModelSelectComponent
  implements OnInit, OnChanges, ControlValueAccessor
{
  @Input() capability: ModelCapability = 'text';
  @Input() placeholder = 'Select a model';
  @Input() width = '100%';
  @Input() size: 'small' | 'large' | undefined;
  @Input() disabled = false;
  @Input() filter = true;
  @Input() appendTo: string | null = 'body';
  @Input() storageContext: string | null = null;

  private readonly generateTextService = inject(GenerateTextService);
  private readonly generateImageService = inject(GenerateImageService);
  private readonly localStorageService = inject(LocalStorageService);

  options: { label: string; value: string }[] = [];
  value: string | null = null;
  isLoading = false;

  private isDisabledFromForm = false;
  private onChange: (value: string | null) => void = () => {};
  private onTouched: () => void = () => {};

  ngOnInit(): void {
    this.loadOptions();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if ('capability' in changes && !changes['capability'].firstChange) {
      this.loadOptions();
    }
  }

  writeValue(value: string | null): void {
    const normalizedValue =
      value !== null && value.trim() !== '' ? value : null;

    if (normalizedValue === null) {
      if (this.isValidOption(this.value)) {
        return;
      }

      this.value = null;
      if (this.options.length > 0) {
        this.applyDefaultValue();
      }
      return;
    }

    this.value = normalizedValue;

    if (this.options.length > 0 && !this.isValidOption(this.value)) {
      this.applyDefaultValue();
    }
  }

  registerOnChange(fn: (value: string | null) => void): void {
    this.onChange = fn;
    this.onChange(this.value);
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.isDisabledFromForm = isDisabled;
  }

  onValueChange(value: string | null): void {
    this.value = value;
    this.saveTextModelForContext(value);
    this.onChange(value);
    this.onTouched();
  }

  get isDisabled(): boolean {
    return this.disabled || this.isDisabledFromForm || this.isLoading;
  }

  private loadOptions(): void {
    this.isLoading = true;

    if (
      this.capability === 'text' ||
      this.capability === 'vision' ||
      this.capability === 'structuredOutput'
    ) {
      this.generateTextService.getAvailableModelInfos().subscribe({
        next: (models) => this.setTextOptionsFromInfos(models),
        error: () => this.setTextOptions([]),
        complete: () => (this.isLoading = false),
      });
      return;
    }

    this.generateImageService.getAvailableModels().subscribe({
      next: (models) => this.setImageOptions(models),
      error: () => this.setTextOptions([]),
      complete: () => (this.isLoading = false),
    });
  }

  private setTextOptions(models: string[]): void {
    this.options = models.map((model) => ({ label: model, value: model }));
    this.applyDefaultValue();
  }

  private setTextOptionsFromInfos(models: TextGenerationModelInfoDto[]): void {
    const filteredModels = models.filter((model) => {
      if (this.capability === 'vision') {
        return model.isVisionCapable;
      }

      if (this.capability === 'structuredOutput') {
        return model.supportsStructuredOutputs;
      }

      return true;
    });

    const sortedModels = this.generateTextService.sortModels(
      filteredModels.map((model) => model.id),
    );

    this.setTextOptions(this.sortTextModelsForContext(sortedModels));
  }

  private setImageOptions(models: ImageGenerationModelInfoDto[]): void {
    const onlyEditors = this.capability === 'imageEdit';
    const filteredModels = models.filter((model) =>
      onlyEditors ? model.isImageEditor : !model.isImageEditor,
    );

    this.options = filteredModels.map((model) => ({
      label: model.name,
      value: model.modelId,
    }));
    this.applyDefaultValue();
  }

  private applyDefaultValue(): void {
    if (this.options.length === 0) {
      this.value = null;
      this.onChange(this.value);
      return;
    }

    if (this.value && this.options.some((option) => option.value === this.value)) {
      return;
    }

    if (
      (this.capability === 'text' ||
        this.capability === 'vision' ||
        this.capability === 'structuredOutput') &&
      this.storageContext
    ) {
      const recentModels = this.getRecentTextModelsForContext();
      const storedModel = recentModels.find((model) =>
        this.options.some((option) => option.value === model),
      );

      if (storedModel) {
        this.value = storedModel;
        this.onChange(this.value);
        return;
      }
    }

    if (this.capability === 'imageGeneration' || this.capability === 'imageEdit') {
      const storageContext =
        this.capability === 'imageEdit' ? 'edit' : 'generate';
      const storedModel =
        this.localStorageService.getNestedStringForKey(
          LocalStorageKey.LastImageModelByContext,
          storageContext,
        ) ??
        this.localStorageService.getStringForKey(LocalStorageKey.LastImageModel);
      if (
        storedModel !== null &&
        this.options.some((option) => option.value === storedModel)
      ) {
        this.value = storedModel;
        this.onChange(this.value);
        return;
      }
    }

    this.value = this.options[0].value;
    this.onChange(this.value);
  }

  private isValidOption(value: string | null): boolean {
    return (
      value !== null && this.options.some((option) => option.value === value)
    );
  }

  private sortTextModelsForContext(models: string[]): string[] {
    const recentModels = this.getRecentTextModelsForContext().filter((model) =>
      models.includes(model),
    );

    if (recentModels.length === 0) {
      return models;
    }

    const remainingModels = models.filter((model) => !recentModels.includes(model));
    return [...recentModels, ...remainingModels];
  }

  private getRecentTextModelsForContext(): string[] {
    if (!this.storageContext) {
      return [];
    }

    return this.localStorageService.getNestedStringArrayForKey(
      LocalStorageKey.RecentTextModelsByContext,
      this.storageContext,
    );
  }

  private saveTextModelForContext(value: string | null): void {
    if (
      value === null ||
      !this.storageContext ||
      (this.capability !== 'text' &&
        this.capability !== 'vision' &&
        this.capability !== 'structuredOutput')
    ) {
      return;
    }

    this.localStorageService.pushNestedRecentStringForKey(
      LocalStorageKey.RecentTextModelsByContext,
      this.storageContext,
      value,
      5,
    );
  }
}
