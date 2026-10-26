import { Locator, Page, expect } from '@playwright/test';

/** Page object for the jobs page: keeps selectors out of the specs. */
export class JobsPageObject {
  constructor(private readonly page: Page) {}

  async open(): Promise<void> {
    await this.page.goto('/');
    await expect(this.page.getByTestId('app-title')).toHaveText('Jobs Platform');
  }

  async openCreateDialog(): Promise<void> {
    await this.page.getByTestId('new-job-button').click();
    await expect(this.page.getByRole('dialog')).toBeVisible();
  }

  nameInput(): Locator {
    return this.page.getByTestId('job-name-input');
  }

  submitButton(): Locator {
    return this.page.getByTestId('create-job-submit');
  }

  async choose(selectTestId: string, optionLabel: RegExp): Promise<void> {
    await this.page.getByTestId(selectTestId).click();
    await this.page.getByRole('option', { name: optionLabel }).click();
  }

  row(name: string): Locator {
    return this.page.getByTestId('job-row').filter({ hasText: name });
  }
}

export function uniqueName(prefix: string): string {
  return `${prefix} ${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 6)}`;
}
