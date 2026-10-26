import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  InjectionToken,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Subject, catchError, interval, merge, of, startWith, switchMap } from 'rxjs';

import { Job } from '../../../core/jobs/job.model';
import { JobsApi } from '../../../core/jobs/jobs-api';
import { JobTable } from '../job-table/job-table';

/** How often the job list is refreshed so status changes made by the worker show up. */
export const JOBS_POLL_INTERVAL_MS = new InjectionToken<number>('JOBS_POLL_INTERVAL_MS', {
  factory: () => 3_000,
});

@Component({
  selector: 'app-jobs-page',
  imports: [MatButtonModule, MatProgressBarModule, JobTable],
  templateUrl: './jobs-page.html',
  styleUrl: './jobs-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class JobsPage implements OnInit {
  private readonly api = inject(JobsApi);
  private readonly dialog = inject(MatDialog);
  private readonly destroyRef = inject(DestroyRef);
  private readonly pollIntervalMs = inject(JOBS_POLL_INTERVAL_MS);
  private readonly refresh$ = new Subject<void>();

  protected readonly jobs = signal<readonly Job[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);

  ngOnInit(): void {
    merge(interval(this.pollIntervalMs), this.refresh$)
      .pipe(
        startWith(undefined),
        switchMap(() =>
          this.api.list().pipe(
            catchError(() => {
              this.loadError.set(true);
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((jobs) => {
        this.loading.set(false);
        if (jobs) {
          this.loadError.set(false);
          this.jobs.set(jobs);
        }
      });
  }

  async openCreateDialog(): Promise<void> {
    // Lazy-loaded: the form controls (select, slider, …) stay out of the initial bundle.
    const { CreateJobDialog } = await import('../create-job-dialog/create-job-dialog');
    this.dialog
      .open(CreateJobDialog, { autoFocus: 'first-tabbable' })
      .afterClosed()
      .subscribe((created?: Job) => {
        if (created) {
          this.refresh$.next();
        }
      });
  }
}
