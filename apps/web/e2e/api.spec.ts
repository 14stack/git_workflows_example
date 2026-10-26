import { expect, test } from '@playwright/test';

import { uniqueName } from './jobs-page';

// Smoke tests through the same origin the browser uses (nginx → API), without the UI.
test.describe('Jobs API via web gateway', () => {
  test('rejects invalid jobs with a problem document', async ({ request }) => {
    const response = await request.post('/api/jobs', {
      data: { name: 'x', dataset: 'iris', algorithm: 'svm', testSize: 0.9 },
    });

    expect(response.status()).toBe(400);
    expect(response.headers()['content-type']).toContain('application/problem+json');
  });

  test('a job created through the API reaches a terminal state', async ({ request }) => {
    const created = await request.post('/api/jobs', {
      data: { name: uniqueName('API smoke'), dataset: 'iris', algorithm: 'logistic_regression' },
    });
    expect(created.status()).toBe(201);
    const location = created.headers()['location'];

    await expect
      .poll(
        async () => ((await (await request.get(location)).json()) as { status: string }).status,
        {
          timeout: 30_000,
        },
      )
      .toBe('succeeded');
  });
});
