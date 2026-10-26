import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatToolbarModule } from '@angular/material/toolbar';

import { JobsPage } from './features/jobs/jobs-page/jobs-page';

@Component({
  selector: 'app-root',
  imports: [MatToolbarModule, JobsPage],
  template: `
    <mat-toolbar class="toolbar">
      <span data-testid="app-title">Jobs Platform</span>
    </mat-toolbar>
    <main>
      <app-jobs-page />
    </main>
  `,
  styleUrl: './app.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {}
