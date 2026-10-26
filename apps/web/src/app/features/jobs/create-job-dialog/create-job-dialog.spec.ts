import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogRef } from '@angular/material/dialog';
import { of, throwError } from 'rxjs';

import { JobsApi } from '../../../core/jobs/jobs-api';
import { aJob } from '../../../testing/job-fixtures';
import { CreateJobDialog, trimmedMinLength } from './create-job-dialog';
import { FormControl } from '@angular/forms';

describe('CreateJobDialog', () => {
  let fixture: ComponentFixture<CreateJobDialog>;
  let component: CreateJobDialog;
  const api = { create: vi.fn() };
  const dialogRef = { close: vi.fn() };

  beforeEach(async () => {
    api.create.mockReset();
    dialogRef.close.mockReset();

    TestBed.configureTestingModule({
      providers: [
        { provide: JobsApi, useValue: api },
        { provide: MatDialogRef, useValue: dialogRef },
      ],
    });
    fixture = TestBed.createComponent(CreateJobDialog);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  function submitButton(): HTMLButtonElement {
    return (fixture.nativeElement as HTMLElement).querySelector(
      '[data-testid="create-job-submit"]',
    ) as HTMLButtonElement;
  }

  it('starts invalid until a name is entered', async () => {
    expect(component.form.invalid).toBe(true);
    expect(submitButton().disabled).toBe(true);

    component.form.controls.name.setValue('Iris forest');
    await fixture.whenStable();

    expect(component.form.valid).toBe(true);
    expect(submitButton().disabled).toBe(false);
  });

  it.each(['ab', '   ', '  ab  '])('rejects the name %j', (name) => {
    component.form.controls.name.setValue(name);

    expect(component.form.controls.name.invalid).toBe(true);
  });

  it('rejects test splits outside 10–50 %', () => {
    component.form.controls.name.setValue('Valid');
    component.form.controls.testSize.setValue(0.6);

    expect(component.form.invalid).toBe(true);
  });

  it('creates the job with a trimmed name and closes with the result', () => {
    const created = aJob({ name: 'Wine SVM' });
    api.create.mockReturnValue(of(created));
    component.form.setValue({
      name: '  Wine SVM ',
      dataset: 'wine',
      algorithm: 'svm',
      testSize: 0.3,
    });

    component.submit();

    expect(api.create).toHaveBeenCalledWith({
      name: 'Wine SVM',
      dataset: 'wine',
      algorithm: 'svm',
      testSize: 0.3,
    });
    expect(dialogRef.close).toHaveBeenCalledWith(created);
  });

  it('shows the API problem detail when creation fails and stays open', async () => {
    api.create.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 503,
            error: { title: 'Job dispatch failed', detail: 'Please retry.' },
          }),
      ),
    );
    component.form.controls.name.setValue('Valid name');

    component.submit();
    await fixture.whenStable();

    const error = (fixture.nativeElement as HTMLElement).querySelector(
      '[data-testid="create-job-error"]',
    );
    expect(error?.textContent).toContain('Please retry.');
    expect(dialogRef.close).not.toHaveBeenCalled();
    expect(submitButton().disabled).toBe(false);
  });

  it('does not call the API while the form is invalid', () => {
    component.submit();

    expect(api.create).not.toHaveBeenCalled();
    expect(component.form.controls.name.touched).toBe(true);
  });
});

describe('trimmedMinLength', () => {
  const validator = trimmedMinLength(3);

  it.each([
    ['abc', null],
    ['  abc  ', null],
    ['', null], // emptiness is the job of Validators.required
  ])('accepts %j', (value, expected) => {
    expect(validator(new FormControl(value))).toBe(expected);
  });

  it('reports the trimmed length', () => {
    expect(validator(new FormControl(' ab '))).toEqual({
      minlength: { requiredLength: 3, actualLength: 2 },
    });
  });
});
