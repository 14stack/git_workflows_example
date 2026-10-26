import { Job } from '../core/jobs/job.model';

export function aJob(overrides: Partial<Job> = {}): Job {
  return {
    id: '01960000-0000-7000-8000-000000000001',
    name: 'Iris forest',
    dataset: 'iris',
    algorithm: 'random_forest',
    testSize: 0.2,
    status: 'pending',
    metrics: null,
    error: null,
    createdAt: '2026-01-15T10:30:00Z',
    startedAt: null,
    completedAt: null,
    ...overrides,
  };
}

export function aSucceededJob(overrides: Partial<Job> = {}): Job {
  return aJob({
    status: 'succeeded',
    metrics: {
      accuracy: 0.9667,
      f1Macro: 0.9666,
      trainSamples: 120,
      testSamples: 30,
      durationMs: 42,
    },
    startedAt: '2026-01-15T10:30:01Z',
    completedAt: '2026-01-15T10:30:03Z',
    ...overrides,
  });
}
