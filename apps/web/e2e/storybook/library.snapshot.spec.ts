/**
 * @mockup admin-mockups/design_files/sp4-library-desktop.html
 *
 * Library argTypes matrix snapshot suite — DS-17 Phase 2.5 (DEC-P3-3).
 *
 * 9 frame canonical Desktop mappati 1:1 al mockup stage. Mobile frames
 * deferred a Phase 4 hardening (Code-reviewer C1+C2).
 *
 * Refs: spec, umbrella #2063.
 */

import { test, expect } from '@playwright/test';

const FRAMES = [
  {
    slug: 'pages-sp4-library-mockup-matrix--frame-09-all-grid-rail',
    file: 'library-09-all-grid-rail.png',
  },
  {
    slug: 'pages-sp4-library-mockup-matrix--frame-10-giochi-grid-bulk',
    file: 'library-10-giochi-grid-bulk.png',
  },
  {
    slug: 'pages-sp4-library-mockup-matrix--frame-11-filters-drawer-open',
    file: 'library-11-filters-drawer-open.png',
  },
  {
    slug: 'pages-sp4-library-mockup-matrix--frame-12-list-view-search',
    file: 'library-12-list-view-search.png',
  },
  {
    slug: 'pages-sp4-library-mockup-matrix--frame-13-empty-first-run',
    file: 'library-13-empty-first-run.png',
  },
  {
    slug: 'pages-sp4-library-mockup-matrix--frame-14-empty-filtered',
    file: 'library-14-empty-filtered.png',
  },
  {
    slug: 'pages-sp4-library-mockup-matrix--frame-15-empty-tab-agents',
    file: 'library-15-empty-tab-agents.png',
  },
  { slug: 'pages-sp4-library-mockup-matrix--frame-16-loading', file: 'library-16-loading.png' },
  {
    slug: 'pages-sp4-library-mockup-matrix--frame-17-error-state',
    file: 'library-17-error-state.png',
  },
];

for (const { slug, file } of FRAMES) {
  test(`Library ${file.replace(/\.png$/, '')} matches snapshot`, async ({ page }) => {
    await page.goto(`/iframe.html?id=${slug}&viewMode=story`);
    await page.waitForSelector('#storybook-root > *', { timeout: 30_000 });
    await page.waitForTimeout(1000);
    await expect(page).toHaveScreenshot(file, { fullPage: true });
  });
}
