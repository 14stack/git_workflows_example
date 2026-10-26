import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSliderModule } from '@angular/material/slider';

import { ALGORITHMS, Algorithm, DATASETS, Dataset, Job } from '../../../core/jobs/job.model';
import { JobsApi } from '../../../core/jobs/jobs-api';

export const NAME_MIN_LENGTH = 3;
export const NAME_MAX_LENGTH = 100;

/** Same rule as the API: surrounding whitespace does not count towards the minimum length. */
export function trimmedMinLength(min: number): ValidatorFn {
  return (control: AbstractControl<string>): ValidationErrors | null => {
    const length = (control.value ?? '').trim().length;
    return length > 0 && length < min
      ? { minlength: { requiredLength: min, actualLength: length } }
      : null;
  };
}

@Component({
  selector: 'app-create-job-dialog',
  imports: [
    DecimalPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSliderModule,
  ],
  templateUrl: './create-job-dialog.html',
  styleUrl: './create-job-dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreateJobDialog {
  private readonly api = inject(JobsApi);
  private readonly dialogRef = inject<MatDialogRef<CreateJobDialog, Job>>(MatDialogRef);

  protected readonly datasets = DATASETS;
  protected readonly algorithms = ALGORITHMS;
  protected readonly nameMaxLength = NAME_MAX_LENGTH;
  protected readonly submitting = signal(false);
  protected readonly submitError = signal<string | null>(null);
  protected readonly formatPercent = (value: number): string => `${Math.round(value * 100)}%`;

  readonly form = inject(NonNullableFormBuilder).group({
    name: [
      '',
      [
        Validators.required,
        Validators.pattern(/\S/),
        trimmedMinLength(NAME_MIN_LENGTH),
        Validators.maxLength(NAME_MAX_LENGTH),
      ],
    ],
    dataset: ['iris' as Dataset, Validators.required],
    algorithm: ['random_forest' as Algorithm, Validators.required],
    testSize: [0.2, [Validators.required, Validators.min(0.1), Validators.max(0.5)]],
  });

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.submitError.set(null);

    const value = this.form.getRawValue();
    this.api.create({ ...value, name: value.name.trim() }).subscribe({
      next: (job) => this.dialogRef.close(job),
      error: (error: unknown) => {
        this.submitting.set(false);
        this.submitError.set(describeError(error));
      },
    });
  }
}

function describeError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const detail = (error.error as { detail?: string; title?: string } | null) ?? null;
    return detail?.detail ?? detail?.title ?? `Request failed (${error.status}).`;
  }
  return 'Something went wrong. Please try again.';
}
