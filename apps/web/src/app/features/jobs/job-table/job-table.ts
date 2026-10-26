import { DatePipe, PercentPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatTableModule } from '@angular/material/table';

import { ALGORITHMS, DATASETS, Job, STATUS_LABELS, labelOf } from '../../../core/jobs/job.model';

@Component({
  selector: 'app-job-table',
  imports: [MatTableModule, DatePipe, PercentPipe],
  templateUrl: './job-table.html',
  styleUrl: './job-table.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class JobTable {
  readonly jobs = input.required<readonly Job[]>();

  protected readonly columns = [
    'name',
    'dataset',
    'algorithm',
    'testSize',
    'status',
    'accuracy',
    'f1Macro',
    'createdAt',
  ];
  protected datasetLabel(job: Job): string {
    return labelOf(DATASETS, job.dataset).split(' (')[0];
  }

  protected algorithmLabel(job: Job): string {
    return labelOf(ALGORITHMS, job.algorithm);
  }

  protected statusLabel(job: Job): string {
    return STATUS_LABELS[job.status];
  }
}
