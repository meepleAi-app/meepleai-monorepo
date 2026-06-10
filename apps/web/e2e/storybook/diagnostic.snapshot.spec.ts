/**
 * DS-17 Phase 4 prelude — Storybook decorator regression test (sub-issue #2120).
 *
 * Verifies that pilot page-mock stories render WITHOUT provider/context errors.
 * Detected issues during Phase 4 prelude investigation:
 *   1. Duplicate preview.{ts,tsx} files loaded the wrong decorator chain
 *   2. Missing mockServiceWorker.js + staticDirs config in main.ts broke MSW init
 *   3. Missing nextjs navigation parameter broke usePathname/useRouter consumers
 *
 * If pilot stories regress to "Could not find required intl object",
 * "No QueryClient set", or "router mocks not available", this test fires.
 *
 * Refs:
 *   - Spec: docs/superpowers/specs/2026-06-10-ds-17-phase-4-prelude-intl-hardening-design.md
 *   - Umbrella: #2063
 */

import { test, expect } from '@playwright/test';

const STORIES = [
  { name: 'Button (primitive control)', slug: 'ui-button--default' },
  {
    name: 'Library Frame09 (pilot)',
    slug: 'pages-sp4-library-mockup-matrix--frame-09-all-grid-rail',
  },
  {
    name: 'Library Frame13 (pilot empty)',
    slug: 'pages-sp4-library-mockup-matrix--frame-13-empty-first-run',
  },
  {
    name: 'GameDetail Frame07 (pilot)',
    slug: 'pages-sp4-gamedetail-mockup-matrix--frame-07-desktop-own-info',
  },
];

const ERROR_SUBSTRINGS = [
  'Could not find required',
  'No QueryClient set',
  'router mocks',
  'IntlProvider',
];

for (const { name, slug } of STORIES) {
  test(`Diagnostic: ${name} renders without provider errors`, async ({ page }) => {
    await page.goto(`/iframe.html?id=${slug}&viewMode=story`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(500);

    const rootChildren = await page.locator('#storybook-root > *').count();
    expect(rootChildren, `${name} should render content`).toBeGreaterThan(0);

    const bodyText = await page.locator('body').innerText();
    for (const needle of ERROR_SUBSTRINGS) {
      expect(bodyText, `${name} should not show "${needle}" error`).not.toContain(needle);
    }
  });
}
