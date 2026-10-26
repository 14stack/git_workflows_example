import { expect, test } from '@playwright/test';

import { JobsPageObject, uniqueName } from './jobs-page';

test.describe('Jobs', () => {
  test('a new job is trained by the ML worker and its result is shown', async ({ page }) => {
    const jobs = new JobsPageObject(page);
    const name = uniqueName('Wine SVM');

    await jobs.open();
    await jobs.openCreateDialog();
    await jobs.nameInput().fill(name);
    await jobs.choose('job-dataset-select', /^Wine/);
    await jobs.choose('job-algorithm-select', /Support vector machine/);
    await page.getByTestId('job-test-size').press('ArrowRight'); // 20 % → 25 %
    await jobs.submitButton().click();

    await expect(page.getByRole('dialog')).toBeHidden();
    const row = jobs.row(name);
    await expect(row).toBeVisible();
    await expect(row).toContainText('Wine');
    await expect(row).toContainText('Support vector machine');
    await expect(row).toContainText('25%');

    // pending → running → succeeded happens asynchronously in the worker; the page polls.
    await expect(row.getByTestId('job-status')).toHaveText('Succeeded', { timeout: 30_000 });
    await expect(row.getByTestId('job-accuracy')).toHaveText(/\d{1,3}\.\d%/);
  });

  test('the form blocks names that are too short', async ({ page }) => {
    const jobs = new JobsPageObject(page);

    await jobs.open();
    await jobs.openCreateDialog();
    await jobs.nameInput().fill('ab');
    await jobs.nameInput().blur();

    await expect(page.getByTestId('job-name-error')).toBeVisible();
    await expect(jobs.submitButton()).toBeDisabled();
  });

  test('cancelling the dialog does not create a job', async ({ page, request }) => {
    const jobs = new JobsPageObject(page);
    const name = uniqueName('Cancelled');

    await jobs.open();
    await jobs.openCreateDialog();
    await jobs.nameInput().fill(name);
    await page.getByRole('button', { name: 'Cancel' }).click();

    await expect(page.getByRole('dialog')).toBeHidden();
    const response = await request.get('/api/jobs');
    expect(response.ok()).toBe(true);
    const all = (await response.json()) as { name: string }[];
    expect(all.map((j) => j.name)).not.toContain(name);
  });
});
