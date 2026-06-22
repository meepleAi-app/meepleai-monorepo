/**
 * @mockup DS-17 Phase D-2 cluster librogame (#2174)
 *
 * librogame cluster snapshot suite — 13 mockup `librogame-*` × N Frame exports.
 * Mirrors apps/web/e2e/storybook/{sp6-7-nano,library,game-detail,auth}.snapshot.spec.ts.
 * FRAMES popolato dai task per-mockup (Task 1-13 del plan). Baseline PNG deferiti
 * (gate `continue-on-error` — cattura nel batch di chiusura Phase, non in questa PR).
 *
 * Refs:
 * - Spec: docs/superpowers/specs/2026-06-22-ds-17-phase-d-2-librogame-storybook-migration-design.md
 * - Plan: docs/superpowers/plans/2026-06-22-ds-17-phase-d-2-librogame-storybook-migration.md
 * - Umbrella #2063, issue #2174
 */

import { test, expect } from '@playwright/test';

const FRAMES: { slug: string; file: string }[] = [
  // librogame-game-detail (3 Frame + 3 variants = 6 stories) — Task 1 PILOTA #2174
  {
    slug: 'pages-librogame-game-detail--frame-01-default',
    file: 'librogame-game-detail-01-default.png',
  },
  {
    slug: 'pages-librogame-game-detail--frame-01-b-kb-indexing',
    file: 'librogame-game-detail-01b-kb-indexing.png',
  },
  {
    slug: 'pages-librogame-game-detail--frame-01-c-kb-error',
    file: 'librogame-game-detail-01c-kb-error.png',
  },
  {
    slug: 'pages-librogame-game-detail--frame-02-loading',
    file: 'librogame-game-detail-02-loading.png',
  },
  {
    slug: 'pages-librogame-game-detail--frame-03-error',
    file: 'librogame-game-detail-03-error.png',
  },
  {
    slug: 'pages-librogame-game-detail--frame-04-not-found',
    file: 'librogame-game-detail-04-not-found.png',
  },
];

for (const { slug, file } of FRAMES) {
  test(`librogame ${file.replace(/\.png$/, '')} matches snapshot`, async ({ page }) => {
    await page.goto(`/iframe.html?id=${slug}&viewMode=story`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(2000);
    await expect(page).toHaveScreenshot(file, { fullPage: true });
  });
}
