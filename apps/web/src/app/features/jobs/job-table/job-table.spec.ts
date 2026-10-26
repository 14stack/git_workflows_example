import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Job } from '../../../core/jobs/job.model';
import { aJob, aSucceededJob } from '../../../testing/job-fixtures';
import { JobTable } from './job-table';

describe('JobTable', () => {
  let fixture: ComponentFixture<JobTable>;

  async function render(jobs: Job[]): Promise<HTMLElement> {
    fixture.componentRef.setInput('jobs', jobs);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(() => {
    fixture = TestBed.createComponent(JobTable);
  });

  it('shows an empty state when there are no jobs', async () => {
    const element = await render([]);

    expect(element.querySelector('[data-testid="jobs-empty"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="jobs-table"]')).toBeNull();
  });

  it('renders one row per job with readable labels', async () => {
    const element = await render([
      aJob({ id: 'a', name: 'First', dataset: 'breast_cancer', algorithm: 'logistic_regression' }),
      aJob({ id: 'b', name: 'Second' }),
    ]);

    const rows = element.querySelectorAll('[data-testid="job-row"]');
    expect(rows).toHaveLength(2);
    expect(rows[0].getAttribute('data-job-id')).toBe('a');
    expect(rows[0].textContent).toContain('Breast cancer');
    expect(rows[0].textContent).toContain('Logistic regression');
    expect(rows[0].textContent).toContain('20%');
  });

  it('shows status and metrics of finished jobs', async () => {
    const element = await render([aSucceededJob()]);

    expect(element.querySelector('[data-testid="job-status"]')?.textContent?.trim()).toBe(
      'Succeeded',
    );
    expect(element.querySelector('[data-testid="job-accuracy"]')?.textContent?.trim()).toBe(
      '96.7%',
    );
  });

  it('exposes the failure reason of failed jobs as a tooltip', async () => {
    const element = await render([aJob({ status: 'failed', error: 'Broker unavailable' })]);

    const status = element.querySelector('[data-testid="job-status"]');
    expect(status?.classList).toContain('status--failed');
    expect(status?.getAttribute('title')).toBe('Broker unavailable');
  });
});
