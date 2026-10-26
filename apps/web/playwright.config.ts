import { defineConfig, devices } from '@playwright/test';

/**
 * End-to-end tests run against the full docker compose stack (web → API → RabbitMQ → worker → DB).
 * Start it with `just up-wait`, or let `just web-test-e2e` do it for you.
 */
const baseURL = process.env['E2E_BASE_URL'] ?? 'http://localhost:8080';
const isCI = !!process.env['CI'];
const reportDir = '../../artifacts/test-results/web-e2e';

export default defineConfig({
  testDir: './e2e',
  outputDir: `${reportDir}/output`,
  fullyParallel: true,
  forbidOnly: isCI,
  retries: 0,
  workers: isCI ? 2 : undefined,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: [
    [isCI ? 'github' : 'list'],
    ['html', { outputFolder: `${reportDir}/html`, open: 'never' }],
    ['junit', { outputFile: `${reportDir}/junit.xml` }],
  ],
  use: {
    baseURL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
