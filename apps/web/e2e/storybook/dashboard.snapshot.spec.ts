/**
 * @mockup admin-mockups/design_files/sp4-dashboard.html
 *
 * Playwright snapshot test for DashboardClient pilot story (DS-17-8-v2).
 *
 * States covered: Default, Empty, Loading, Error (1 PNG per state).
 * Threshold: 5% area diff (config), light theme, 1440x900 desktop only.
 *
 * Refs: spec docs/superpowers/specs/2026-06-09-ds-17-phase-2-design.md, umbrella #2063.
 */

import { test, expect } from '@playwright/test';

const STORY_BASE = '/iframe.html?id=pages-sp4-dashboard-mockup-pilot--';
const STATES = ['default', 'empty', 'loading', 'error'] as const;

for (const state of STATES) {
  test(`Dashboard ${state} matches snapshot`, async ({ page }) => {
    await page.goto(`${STORY_BASE}${state}&viewMode=story`);
    // Wait for story root to render
    await page.waitForSelector('#storybook-root > *', { timeout: 30_000 });
    // Settle animations + suspense + react-query fetch
    await page.waitForTimeout(1000);
    await expect(page).toHaveScreenshot(`dashboard-${state}.png`, { fullPage: true });
  });
}
