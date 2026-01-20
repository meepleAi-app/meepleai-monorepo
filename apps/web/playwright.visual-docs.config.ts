/**
 * Playwright Configuration for Visual Documentation Tests
 *
 * This configuration is specifically for generating visual documentation
 * screenshots with annotations and metadata.
 *
 * Usage:
 *   pnpm test:visual-docs
 *   pnpm playwright test --config=playwright.visual-docs.config.ts
 */

import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e/visual-docs',
  outputDir: '../docs/screenshots/.test-output',

  /* Run tests sequentially to ensure consistent screenshot ordering */
  fullyParallel: false,
  workers: 1,

  /* Fail the build on CI if you accidentally left test.only */
  forbidOnly: !!process.env.CI,

  /* Retry on CI only */
  retries: process.env.CI ? 1 : 0,

  /* Reporter */
  reporter: [
    ['html', { outputFolder: '../docs/screenshots/.report' }],
    ['list'],
  ],

  /* Shared settings for all tests */
  use: {
    /* Base URL for relative navigation */
    baseURL: process.env.PLAYWRIGHT_BASE_URL || 'http://localhost:3000',

    /* Consistent viewport for all screenshots */
    viewport: { width: 1280, height: 900 },

    /* Disable automatic screenshots - we handle them manually */
    screenshot: 'off',
    video: 'off',

    /* Collect trace when retrying */
    trace: 'on-first-retry',

    /* Timeout settings */
    actionTimeout: 15000,
    navigationTimeout: 30000,
  },

  /* Global timeout for each test */
  timeout: 60000,

  /* Configure projects for consistent browser behavior */
  projects: [
    {
      name: 'visual-docs',
      use: {
        ...devices['Desktop Chrome'],
        /* Force consistent rendering */
        deviceScaleFactor: 1,
        isMobile: false,
        hasTouch: false,
        /* Disable features that might affect screenshots */
        javaScriptEnabled: true,
        bypassCSP: true,
      },
    },
  ],

  /* Local dev server - disabled by default to avoid memory issues
   * Start the dev server manually with: pnpm dev:visual-docs
   * Then run tests with: pnpm test:visual-docs
   */
  webServer: process.env.VISUAL_DOCS_AUTO_SERVER
    ? {
        command: 'cross-env NODE_OPTIONS="--max-old-space-size=8192" pnpm dev',
        url: 'http://localhost:3000',
        reuseExistingServer: true,
        timeout: 120000,
      }
    : undefined,

  /* Global setup/teardown */
  globalSetup: './e2e/visual-docs/global-setup.ts',
  globalTeardown: undefined,

  /* Expect configuration */
  expect: {
    /* Timeout for expect() assertions */
    timeout: 10000,
    /* Screenshot comparison options */
    toHaveScreenshot: {
      maxDiffPixels: 100,
      threshold: 0.2,
    },
  },
});
