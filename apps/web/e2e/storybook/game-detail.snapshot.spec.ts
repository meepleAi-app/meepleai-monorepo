/**
 * @mockup admin-mockups/design_files/sp4-game-detail.html
 *
 * Playwright snapshot test for GameDetailView pilot story (DS-17-8-v2).
 *
 * States covered: Default, Empty, Error.
 * Threshold: 5% area diff, light theme, 1440x900 desktop.
 *
 * Refs: spec, umbrella #2063.
 */

import { test, expect } from '@playwright/test';

const STORY_BASE = '/iframe.html?id=pages-sp4-gamedetail-mockup-pilot--';
const STATES = ['default', 'empty', 'error'] as const;

for (const state of STATES) {
  test(`GameDetail ${state} matches snapshot`, async ({ page }) => {
    await page.goto(`${STORY_BASE}${state}&viewMode=story`);
    await page.waitForSelector('#storybook-root > *', { timeout: 30_000 });
    await page.waitForTimeout(1000);
    await expect(page).toHaveScreenshot(`game-detail-${state}.png`, { fullPage: true });
  });
}
