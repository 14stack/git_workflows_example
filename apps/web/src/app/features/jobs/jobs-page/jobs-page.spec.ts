import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { of, throwError } from 'rxjs';

import { JobsApi } from '../../../core/jobs/jobs-api';
import { aJob, aSucceededJob } from '../../../testing/job-fixtures';
import { JOBS_POLL_INTERVAL_MS, JobsPage } from './jobs-page';

describe('JobsPage', () => {
  const api = { list: vi.fn() };
  const dialog = { open: vi.fn() };
  let fixture: ComponentFixture<JobsPage>;

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(() => {
    vi.useFakeTimers();
    api.list.mockReset();
    dialog.open.mockReset();

    TestBed.configureTestingModule({
      providers: [
        { provide: JobsApi, useValue: api },
        { provide: MatDialog, useValue: dialog },
        { provide: JOBS_POLL_INTERVAL_MS, useValue: 1_000 },
      ],
    });
  });

  afterEach(() => vi.useRealTimers());

  async function render(): Promise<void> {
    fixture = TestBed.createComponent(JobsPage);
    fixture.detectChanges();
    await fixture.whenStable();
  }

  it('loads jobs immediately and renders them', async () => {
    api.list.mockReturnValue(of([aJob({ name: 'Loaded job' })]));

    await render();

    expect(api.list).toHaveBeenCalledTimes(1);
    expect(element().textContent).toContain('Loaded job');
  });

  it('polls so status changes made by the worker show up', async () => {
    api.list.mockReturnValueOnce(of([aJob()])).mockReturnValue(of([aSucceededJob()]));
    await render();

    await vi.advanceTimersByTimeAsync(1_000);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(api.list).toHaveBeenCalledTimes(2);
    expect(element().querySelector('[data-testid="job-status"]')?.textContent?.trim()).toBe(
      'Succeeded',
    );
  });

  it('keeps polling and shows a notice while the API is unreachable', async () => {
    api.list.mockReturnValue(throwError(() => new Error('offline')));

    await render();

    expect(element().querySelector('[data-testid="jobs-load-error"]')).not.toBeNull();
  });

  it('refreshes right after a job is created', async () => {
    api.list.mockReturnValue(of([]));
    dialog.open.mockReturnValue({ afterClosed: () => of(aJob()) });
    await render();

    (element().querySelector('[data-testid="new-job-button"]') as HTMLButtonElement).click();
    await vi.dynamicImportSettled();

    expect(dialog.open).toHaveBeenCalledTimes(1);
    expect(api.list).toHaveBeenCalledTimes(2);
  });

  it('does not refresh when the dialog is cancelled', async () => {
    api.list.mockReturnValue(of([]));
    dialog.open.mockReturnValue({ afterClosed: () => of(undefined) });
    await render();

    (element().querySelector('[data-testid="new-job-button"]') as HTMLButtonElement).click();
    await vi.dynamicImportSettled();

    expect(dialog.open).toHaveBeenCalledTimes(1);
    expect(api.list).toHaveBeenCalledTimes(1);
  });
});
