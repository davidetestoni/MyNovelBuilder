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
  private integrationsService = inject(IntegrationsService);
  private toastrService = inject(ToastrService);

  integrationsForm = new FormGroup({
    openRouterApiKey: new FormControl<string>('', Validators.maxLength(1000)),
    ttsProvider: new FormControl<TtsProvider>(TtsProvider.Custom),
  });
  hasOpenRouterApiKey: boolean = false;

  ttsProviderOptions = Object.values(TtsProvider).map((provider) => ({
    // camelCase to spaced Pascal Case for display
    label: provider
      .replace(/([A-Z])/g, ' $1')
      .replace(/^./, (str) => str.toUpperCase()),
    value: provider,
  }));

  ngOnInit(): void {
    this.integrationsService.getIntegrationsConfig().subscribe({
      next: (config: IntegrationsConfigDto) => {
        this.hasOpenRouterApiKey = config.hasOpenRouterApiKey;
        this.integrationsForm.patchValue({
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
      openRouterApiKey:
        this.integrationsForm.value.openRouterApiKey || undefined,
      ttsProvider: this.integrationsForm.value.ttsProvider,
    };
    this.integrationsService.updateIntegrationsConfig(updateDto).subscribe({
      next: () => {
        if (this.integrationsForm.value.openRouterApiKey) {
          this.hasOpenRouterApiKey = true;
          this.integrationsForm.get('openRouterApiKey')?.reset();
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
