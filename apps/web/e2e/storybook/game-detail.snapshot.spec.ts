/**
 * @mockup admin-mockups/design_files/sp4-game-detail.html
 *
 * GameDetail argTypes matrix snapshot suite — DS-17 Phase 2.5 (DEC-P3-3).
 *
 * 3 frame canonical Desktop. Mobile frames m1-m6 DEFERRED a Phase 4 hardening.
 *
 * Refs: spec, umbrella #2063.
 */

import { test, expect } from '@playwright/test';

const FRAMES = [
  {
    slug: 'pages-sp4-gamedetail-mockup-matrix--frame-07-desktop-own-info',
    file: 'game-detail-07-desktop-own-info.png',
  },
  {
    slug: 'pages-sp4-gamedetail-mockup-matrix--frame-08-desktop-community-locked',
    file: 'game-detail-08-desktop-community-locked.png',
  },
  {
    slug: 'pages-sp4-gamedetail-mockup-matrix--frame-09-desktop-loading',
    file: 'game-detail-09-desktop-loading.png',
  },
];

for (const { slug, file } of FRAMES) {
  test(`GameDetail ${file.replace(/\.png$/, '')} matches snapshot`, async ({ page }) => {
    await page.goto(`/iframe.html?id=${slug}&viewMode=story`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(2000);
    await expect(page).toHaveScreenshot(file, { fullPage: true });
  });
}
