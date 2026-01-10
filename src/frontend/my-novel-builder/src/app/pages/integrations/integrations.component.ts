import { Component, OnInit, inject } from '@angular/core';

import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { IntegrationsService } from '../../services/integrations.service';
import { IntegrationsConfigDto, UpdateIntegrationsConfigDto } from '../../types/dtos/integrations/integrations-config.dto';
import { ToastrService } from 'ngx-toastr';

@Component({
  selector: 'app-integrations',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './integrations.component.html',
  styleUrl: './integrations.component.scss'
})
export class IntegrationsComponent implements OnInit {
  private integrationsService = inject(IntegrationsService);
  private toastrService = inject(ToastrService);

  integrationsForm = new FormGroup({
    openRouterApiKey: new FormControl('', Validators.maxLength(1000))
  });
  hasOpenRouterApiKey: boolean = false;

  ngOnInit(): void {
    this.integrationsService.getIntegrationsConfig().subscribe({
      next: (config: IntegrationsConfigDto) => {
        this.hasOpenRouterApiKey = config.hasOpenRouterApiKey;
      },
      error: (error) => {
        this.toastrService.error('Failed to load integrations configuration.');
        console.error('Error loading configuration:', error);
      }
    });
  }

  onSubmit(): void {
    if (!this.integrationsForm.valid) {
      return;
    }

    const updateDto: UpdateIntegrationsConfigDto = {
      openRouterApiKey: this.integrationsForm.value.openRouterApiKey
    };
    this.integrationsService.updateIntegrationsConfig(updateDto).subscribe({
      next: () => {
        this.hasOpenRouterApiKey = !!updateDto.openRouterApiKey;
        this.integrationsForm.get('openRouterApiKey')?.reset();
        this.toastrService.success('Integrations configuration updated successfully.');
      },
      error: (error) => {
        this.toastrService.error('Failed to update integrations configuration.');
        console.error('Error updating configuration:', error);
      }
    });
  }
}
