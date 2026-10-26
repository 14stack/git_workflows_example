import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { App } from './app';
import { JobsApi } from './core/jobs/jobs-api';

describe('App', () => {
  it('renders the toolbar and the jobs page', async () => {
    TestBed.configureTestingModule({
      providers: [{ provide: JobsApi, useValue: { list: () => of([]) } }],
    });

    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('[data-testid="app-title"]')?.textContent).toBe('Jobs Platform');
    expect(element.querySelector('app-jobs-page')).not.toBeNull();
  });
});
