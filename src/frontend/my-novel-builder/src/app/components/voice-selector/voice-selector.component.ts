import { Component, forwardRef, Input, OnChanges } from '@angular/core';
import { ControlValueAccessor, FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';

export interface VoiceOption { label: string; value: string; }

/** Selects an advertised voice or edits a custom ID, exposing only the actual ID to forms. */
@Component({
  selector: 'app-voice-selector',
  standalone: true,
  imports: [FormsModule, SelectModule, InputTextModule],
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => VoiceSelectorComponent), multi: true }],
  template: `
    <div class="voice-inputs" [class.custom]="customMode">
      <p-select [inputId]="inputId" [options]="selectOptions" optionLabel="label" optionValue="value"
        [ngModel]="selection" (ngModelChange)="selectVoice($event)" [ngModelOptions]="{standalone: true}"
        placeholder="Select a voice" [filter]="true" [disabled]="disabled || formDisabled"
        (onBlur)="touch()" appendTo="body" [style]="{width: '100%'}" />
      @if (customMode) {
        <input pInputText type="text" [id]="inputId + '-custom'" aria-label="Custom voice ID"
          placeholder="Custom voice ID" [ngModel]="value" (ngModelChange)="editCustomVoice($event)"
          [ngModelOptions]="{standalone: true}" [disabled]="disabled || formDisabled" (blur)="touch()" />
      }
    </div>
  `,
  styles: `
    :host { display: block; min-width: 0; width: 100%; }
    .voice-inputs { display: flex; gap: .65rem; min-width: 0; align-items: center; }
    p-select { display: inline-flex; align-items: center; flex: 1 1 100%; min-width: 0; }
    .custom p-select { flex: 0 0 9rem; min-width: 9rem; }
    input { flex: 1 1 12rem; min-width: 0; width: 100%; }
    @media (max-width: 480px) {
      .custom { flex-wrap: wrap; }
      .custom p-select { flex-basis: 100%; }
    }
  `,
})
export class VoiceSelectorComponent implements ControlValueAccessor, OnChanges {
  @Input() options: VoiceOption[] = [];
  @Input() allowCustom = false;
  @Input() inputId = 'voice';
  @Input() disabled = false;
  readonly customOption = '__custom_voice_option__';
  value = '';
  customMode = false;
  formDisabled = false;
  private customDraft = '';
  private optionIdentity = '';
  private customAllowed = false;
  private change: (value: string) => void = () => {};
  touch: () => void = () => {};

  get selectOptions(): VoiceOption[] {
    return this.allowCustom ? [...this.options, { label: 'Custom', value: this.customOption }] : this.options;
  }
  get selection(): string { return this.customMode ? this.customOption : this.value; }

  ngOnChanges(): void {
    const identity = JSON.stringify(this.options.map(option => option.value));
    if (identity === this.optionIdentity && this.customAllowed === this.allowCustom) return;
    this.optionIdentity = identity;
    this.customAllowed = this.allowCustom;
    if (!this.allowCustom) this.customMode = false;
    else if (this.value) this.customMode = !this.options.some(option => option.value === this.value);
    if (this.customMode) this.customDraft = this.value;
  }
  writeValue(value: string | null): void {
    if ((value ?? '') === this.value) return;
    this.value = value ?? '';
    this.customMode = this.allowCustom && !!this.value && !this.options.some(option => option.value === this.value);
    if (this.customMode) this.customDraft = this.value;
  }
  registerOnChange(fn: (value: string) => void): void { this.change = fn; }
  registerOnTouched(fn: () => void): void { this.touch = fn; }
  setDisabledState(disabled: boolean): void { this.formDisabled = disabled; }

  selectVoice(selection: string): void {
    this.customMode = this.allowCustom && selection === this.customOption;
    this.value = this.customMode ? this.customDraft : selection;
    this.change(this.value);
    this.touch();
  }
  editCustomVoice(value: string): void {
    this.value = this.customDraft = value;
    this.change(value);
  }
}
