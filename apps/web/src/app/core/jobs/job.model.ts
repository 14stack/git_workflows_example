/** Mirrors the Jobs API contract (camelCase properties, snake_case enum values). */

export type Dataset = 'iris' | 'wine' | 'breast_cancer';
export type Algorithm = 'logistic_regression' | 'random_forest' | 'svm';
export type JobStatus = 'pending' | 'running' | 'succeeded' | 'failed';

export interface JobMetrics {
  accuracy: number;
  f1Macro: number;
  trainSamples: number;
  testSamples: number;
  durationMs: number;
}

export interface Job {
  id: string;
  name: string;
  dataset: Dataset;
  algorithm: Algorithm;
  testSize: number;
  status: JobStatus;
  metrics: JobMetrics | null;
  error: string | null;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
}

export interface CreateJobRequest {
  name: string;
  dataset: Dataset;
  algorithm: Algorithm;
  testSize: number;
}

export interface Option<T extends string> {
  value: T;
  label: string;
}

export const DATASETS: readonly Option<Dataset>[] = [
  { value: 'iris', label: 'Iris (150 flowers, 3 classes)' },
  { value: 'wine', label: 'Wine (178 wines, 3 cultivars)' },
  { value: 'breast_cancer', label: 'Breast cancer (569 samples, 2 classes)' },
];

export const ALGORITHMS: readonly Option<Algorithm>[] = [
  { value: 'logistic_regression', label: 'Logistic regression' },
  { value: 'random_forest', label: 'Random forest' },
  { value: 'svm', label: 'Support vector machine' },
];

export const STATUS_LABELS: Readonly<Record<JobStatus, string>> = {
  pending: 'Pending',
  running: 'Running',
  succeeded: 'Succeeded',
  failed: 'Failed',
};

export function labelOf<T extends string>(options: readonly Option<T>[], value: T): string {
  return options.find((o) => o.value === value)?.label ?? value;
}
