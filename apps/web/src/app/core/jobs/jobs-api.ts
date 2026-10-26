import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { CreateJobRequest, Job } from './job.model';

/** Thin client for the Jobs API. In every environment the API is reachable under `/api` (same origin). */
@Injectable({ providedIn: 'root' })
export class JobsApi {
  static readonly baseUrl = '/api/jobs';

  private readonly http = inject(HttpClient);

  list(): Observable<Job[]> {
    return this.http.get<Job[]>(JobsApi.baseUrl);
  }

  create(request: CreateJobRequest): Observable<Job> {
    return this.http.post<Job>(JobsApi.baseUrl, request);
  }
}
