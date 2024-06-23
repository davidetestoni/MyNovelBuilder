import { Component, inject } from '@angular/core';
import {
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { CompendiumService } from '../../services/compendium.service';
import {
  MatDialogActions,
  MatDialogClose,
  MatDialogRef,
} from '@angular/material/dialog';
import { ToastrModule, ToastrService } from 'ngx-toastr';

@Component({
  selector: 'app-create-compendium',
  standalone: true,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    MatDialogActions,
    MatDialogClose,
    ToastrModule,
  ],
  templateUrl: './create-compendium.component.html',
  styleUrl: './create-compendium.component.scss',
})
export class CreateCompendiumComponent {
  compendiumService: CompendiumService = inject(CompendiumService);

  formGroup = new FormGroup({
    name: new FormControl('', [Validators.required, Validators.maxLength(100)]),
    description: new FormControl('', [Validators.maxLength(500)]),
  });

  constructor(
    public dialogRef: MatDialogRef<CreateCompendiumComponent>,
    private toastr: ToastrService
  ) {}

  createCompendium(): void {
    this.compendiumService
      .createCompendium({
        name: this.formGroup.get('name')!.value!,
        description: this.formGroup.get('description')?.value ?? '',
      })
      .subscribe(() => {
        this.toastr.success('Compendium created successfully.');
        this.dialogRef.close(true);
      });
  }
}
