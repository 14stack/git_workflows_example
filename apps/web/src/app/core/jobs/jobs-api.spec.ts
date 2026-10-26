import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { aJob } from '../../testing/job-fixtures';
import { JobsApi } from './jobs-api';

describe('JobsApi', () => {
  let api: JobsApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(JobsApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists jobs from GET /api/jobs', () => {
    const jobs = [aJob()];
    let received: unknown;

    api.list().subscribe((result) => (received = result));
    http.expectOne({ method: 'GET', url: '/api/jobs' }).flush(jobs);

    expect(received).toEqual(jobs);
  });

  it('creates a job with POST /api/jobs', () => {
    const request = {
      name: 'Wine SVM',
      dataset: 'wine',
      algorithm: 'svm',
      testSize: 0.3,
    } as const;
    let received: unknown;

    api.create(request).subscribe((result) => (received = result));
    const call = http.expectOne({ method: 'POST', url: '/api/jobs' });
    call.flush(aJob({ name: 'Wine SVM' }));

    expect(call.request.body).toEqual(request);
    expect(received).toMatchObject({ name: 'Wine SVM' });
  });
});
