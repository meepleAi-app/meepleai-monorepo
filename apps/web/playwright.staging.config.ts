/**
 * Playwright config for post-deploy staging smoke tests.
 * Runs smoke.spec.ts against the live staging environment (no local server).
 *
 * Usage (CI):
 *   pnpm exec playwright test e2e/smoke.spec.ts --config=playwright.staging.config.ts
 */

import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  retries: 2,
  workers: 1,
  forbidOnly: true,
  reporter: [['list'], ['html', { open: 'never' }]],

  use: {
    baseURL: process.env.STAGING_URL || 'https://meepleai.app',
    trace: 'on-first-retry',
    actionTimeout: 10_000,
    navigationTimeout: 30_000,
    launchOptions: {
      args: [
        '--no-sandbox',
        '--disable-setuid-sandbox',
        '--disable-dev-shm-usage',
        '--disable-gpu',
      ],
    },
  },

  projects: [
    {
      name: 'staging-smoke',
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  // No webServer — tests run against the live staging deployment
});
