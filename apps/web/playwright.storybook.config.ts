/**
 * Playwright config for Storybook visual snapshot tests (DS-17-8-v2).
 *
 * Builds the Storybook static artifact and serves it via http-server on port 6007
 * (avoids collision with `pnpm storybook` dev server on 6006). Runs snapshot
 * specs under `e2e/storybook/*.snapshot.spec.ts`.
 *
 * Threshold: 5% area diff (DEC-4 scope), 1440x900 desktop, light theme only.
 *
 * Refs:
 *   - Spec:  docs/superpowers/specs/2026-06-09-ds-17-phase-2-design.md
 *   - Plan:  docs/superpowers/plans/2026-06-09-ds-17-phase-2-implementation-plan.md
 *   - Sub-issue: #2095 (DS-17-8-v2)
 *   - Umbrella: #2063
 */

import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e/storybook',
  testMatch: '*.snapshot.spec.ts',
  // Visual snapshots prefer sequential to avoid flakey diffs from parallel render races.
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: 1,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: 'http://127.0.0.1:6007',
    trace: 'retain-on-failure',
    // Force consistent rendering env (mitigates font/locale drift; DEC-4 scope).
    locale: 'en-US',
    timezoneId: 'UTC',
    colorScheme: 'light',
  },
  expect: {
    toHaveScreenshot: {
      // 5% area threshold per umbrella DEC-4.
      maxDiffPixelRatio: 0.05,
      // Per-pixel tolerance (Playwright default); balances anti-aliasing differences
      // across Chromium versions.
      threshold: 0.2,
      animations: 'disabled',
    },
  },
  projects: [
    {
      name: 'storybook-desktop',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1440, height: 900 },
      },
    },
  ],
  webServer: {
    command: 'pnpm build-storybook && pnpm exec http-server storybook-static -p 6007 -s',
    url: 'http://127.0.0.1:6007',
    // build-storybook can take ~2-3 min cold; safety margin for CI.
    timeout: 5 * 60 * 1000,
    reuseExistingServer: !process.env.CI,
  },
});
