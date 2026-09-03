import {
  Component,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  OnInit,
  Output,
  SimpleChanges,
  forwardRef,
  inject,
} from '@angular/core';
import {
  ControlValueAccessor,
  FormsModule,
  NG_VALUE_ACCESSOR,
} from '@angular/forms';
import { PromptService } from '../../services/prompt.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { PromptType } from '../../types/enums/prompt-type';
import { SelectModule } from 'primeng/select';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-prompt-select',
  standalone: true,
  imports: [FormsModule, SelectModule],
  templateUrl: './prompt-select.component.html',
  styleUrl: './prompt-select.component.scss',
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => PromptSelectComponent),
      multi: true,
    },
  ],
})
export class PromptSelectComponent
  implements OnInit, OnChanges, OnDestroy, ControlValueAccessor
{
  @Input() promptType: PromptType | null = null;
  @Input() prompts: PromptDto[] | null = null;
  @Input() storagePromptType: PromptType | null = null;
  @Input() placeholder = 'Select a prompt';
  @Input() width = '100%';
  @Input() size: 'small' | 'large' | undefined;
  @Input() disabled = false;

  @Output() optionsChanged = new EventEmitter<number>();

  private readonly promptService = inject(PromptService);
  private readonly localStorageService = inject(LocalStorageService);

  options: PromptDto[] = [];
  value: string | null = null;
  isLoading = false;

  private isDisabledFromForm = false;
  private optionsSubscription?: Subscription;
  private onChange: (value: string | null) => void = () => {};
  private onTouched: () => void = () => {};

  ngOnInit(): void {
    this.refreshOptions();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (
      ('prompts' in changes && !changes['prompts'].firstChange) ||
      ('promptType' in changes && !changes['promptType'].firstChange) ||
      ('storagePromptType' in changes &&
        !changes['storagePromptType'].firstChange)
    ) {
      this.refreshOptions();
    }
  }

  writeValue(value: string | null): void {
    const normalizedValue =
      value !== null && value.trim() !== '' ? value.trim() : null;

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
    const normalizedValue =
      value !== null && value.trim() !== '' ? value.trim() : null;
    this.value = normalizedValue;
    this.onChange(normalizedValue);
    this.onTouched();

    const storagePromptType = this.getStoragePromptType();
    if (storagePromptType !== null && normalizedValue !== null) {
      this.localStorageService.setNestedStringForKey(
        LocalStorageKey.RecentPrompts,
        storagePromptType,
        normalizedValue,
      );
    }
  }

  get isDisabled(): boolean {
    return this.disabled || this.isDisabledFromForm || this.isLoading;
  }

  ngOnDestroy(): void {
    this.optionsSubscription?.unsubscribe();
    this.isLoading = false;
  }

  private refreshOptions(): void {
    this.optionsSubscription?.unsubscribe();
    this.optionsSubscription = undefined;

    if (this.prompts !== null) {
      this.isLoading = false;
      this.options = this.filterPrompts(this.prompts);
      this.applyDefaultValue();
      return;
    }

    this.isLoading = true;
    this.optionsSubscription = this.promptService.getPrompts().subscribe({
      next: (prompts) => {
        this.options = this.filterPrompts(prompts);
        this.applyDefaultValue();
      },
      error: () => {
        this.options = [];
        this.applyDefaultValue();
        this.isLoading = false;
      },
      complete: () => {
        this.isLoading = false;
      },
    });
  }

  private filterPrompts(prompts: PromptDto[]): PromptDto[] {
    if (this.promptType === null) {
      return prompts;
    }

    return prompts.filter((prompt) => prompt.type === this.promptType);
  }

  private applyDefaultValue(): void {
    this.optionsChanged.emit(this.options.length);

    if (this.options.length === 0) {
      this.value = null;
      this.onChange(this.value);
      return;
    }

    if (this.value && this.options.some((prompt) => prompt.id === this.value)) {
      return;
    }

    const storagePromptType = this.getStoragePromptType();
    if (storagePromptType !== null) {
      const storedPromptId = this.localStorageService.getNestedStringForKey(
        LocalStorageKey.RecentPrompts,
        storagePromptType,
      );

      if (
        storedPromptId !== null &&
        this.options.some((prompt) => prompt.id === storedPromptId)
      ) {
        this.value = storedPromptId;
        this.onChange(this.value);
        return;
      }
    }

    this.value = this.options[0].id;
    this.onChange(this.value);
  }

  private getStoragePromptType(): PromptType | null {
    return this.storagePromptType ?? this.promptType;
  }

  private isValidOption(value: string | null): boolean {
    return (
      value !== null && this.options.some((prompt) => prompt.id === value)
    );
  }
}
