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

  private integrationsService = inject(IntegrationsService);
  private toastrService = inject(ToastrService);

  integrationsForm = new FormGroup({
    textGenerationProvider: new FormControl<TextGenerationProvider>(
      TextGenerationProvider.OpenRouter,
    ),
    openRouterApiKey: new FormControl<string>('', Validators.maxLength(1000)),
    googleGenAiApiKey: new FormControl<string>('', Validators.maxLength(1000)),
    ttsProvider: new FormControl<TtsProvider>(TtsProvider.Custom),
  });
  hasOpenRouterApiKey: boolean = false;
  hasGoogleGenAiApiKey: boolean = false;

  ttsProviderOptions = Object.values(TtsProvider).map((provider) => ({
    // camelCase to spaced Pascal Case for display
    label: provider
      .replace(/([A-Z])/g, ' $1')
      .replace(/^./, (str) => str.toUpperCase()),
    value: provider,
  }));

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

  ngOnInit(): void {
    this.integrationsService.getIntegrationsConfig().subscribe({
      next: (config: IntegrationsConfigDto) => {
        this.hasOpenRouterApiKey = config.hasOpenRouterApiKey;
        this.hasGoogleGenAiApiKey = config.hasGoogleGenAiApiKey;
        this.integrationsForm.patchValue({
          textGenerationProvider: config.textGenerationProvider,
          ttsProvider: config.ttsProvider,
        });
      },
      error: (error) => {
        this.toastrService.error('Failed to load integrations configuration.');
        console.error('Error loading configuration:', error);
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
      ttsProvider: this.integrationsForm.value.ttsProvider,
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
