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
    // #2799: all of staging is behind Cloudflare Access (owner-only). Without
    // these service-token headers every navigation lands on the CF Access
    // "Sign in" page instead of the app — smoke.spec.ts step 2 (email input)
    // fails deterministically and the lenient steps silently pass against CF's
    // page. Mirrors the CF_ACCESS_CLIENT_ID/SECRET bypass the curl smoke already
    // uses in deploy-staging.yml (Post-deploy Validation). Conditional so local
    // runs without the tokens don't send empty headers.
    extraHTTPHeaders:
      process.env.CF_ACCESS_CLIENT_ID && process.env.CF_ACCESS_CLIENT_SECRET
        ? {
            'CF-Access-Client-Id': process.env.CF_ACCESS_CLIENT_ID,
            'CF-Access-Client-Secret': process.env.CF_ACCESS_CLIENT_SECRET,
          }
        : {},
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
