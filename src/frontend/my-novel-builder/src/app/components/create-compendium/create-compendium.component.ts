import { Component, inject } from '@angular/core';
import {
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { CompendiumService } from '../../services/compendium.service';
import { ToastrService } from 'ngx-toastr';
import { DynamicDialogRef } from 'primeng/dynamicdialog';

// PrimeNG Imports
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { ButtonModule } from 'primeng/button';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-create-compendium',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    // PrimeNG Modules
    InputTextModule,
    TextareaModule,
    ButtonModule,
  ],
  templateUrl: './create-compendium.component.html',
  styleUrl: './create-compendium.component.scss',
})
export class CreateCompendiumComponent {
  private dialogRef = inject(DynamicDialogRef);
  private toastr = inject(ToastrService);

  isCreating = false;
  readonly compendiumService: CompendiumService = inject(CompendiumService);

  formGroup = new FormGroup({
    name: new FormControl('', [Validators.required, Validators.maxLength(100)]),
    description: new FormControl('', [Validators.maxLength(500)]),
  });

  createCompendium(): void {
    if (this.formGroup.invalid || this.isCreating) {
      return;
    }

    this.isCreating = true;
    this.compendiumService
      .createCompendium({
        name: this.formGroup.get('name')!.value!,
        description: this.formGroup.get('description')?.value ?? '',
      })
      .pipe(
        finalize(() => {
          this.isCreating = false;
        }),
      )
      .subscribe({
        next: () => {
          this.toastr.success('Compendium created successfully.');
          this.dialogRef.close(true);
        },
        error: () => {
          this.toastr.error('Failed to create compendium.');
        },
      });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
