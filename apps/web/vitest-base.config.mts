import { defineConfig } from 'vitest/config';

// Merged by the Angular unit-test builder (see angular.json → test.options.runnerConfig).
export default defineConfig({
  test: {
    coverage: {
      reportsDirectory: '../../artifacts/coverage/web',
      thresholds: {
        lines: 80,
        functions: 80,
        branches: 75,
        statements: 80,
      },
    },
  },
});
