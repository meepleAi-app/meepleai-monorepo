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
    slug: 'pages-librogame-game-detail--frame-01-kb-indexing',
    file: 'librogame-game-detail-01-kb-indexing.png',
  },
  {
    slug: 'pages-librogame-game-detail--frame-01-kb-error',
    file: 'librogame-game-detail-01-kb-error.png',
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
  // librogame-library-search (6 frames) — Task 2 #2174
  {
    slug: 'pages-librogame-library-search--frame-01-default',
    file: 'librogame-library-search-01-default.png',
  },
  {
    slug: 'pages-librogame-library-search--frame-02-empty',
    file: 'librogame-library-search-02-empty.png',
  },
  {
    slug: 'pages-librogame-library-search--frame-03-quota-soft',
    file: 'librogame-library-search-03-quota-soft.png',
  },
  {
    slug: 'pages-librogame-library-search--frame-04-quota-hard',
    file: 'librogame-library-search-04-quota-hard.png',
  },
  {
    slug: 'pages-librogame-library-search--frame-05-loading',
    file: 'librogame-library-search-05-loading.png',
  },
  {
    slug: 'pages-librogame-library-search--frame-06-error',
    file: 'librogame-library-search-06-error.png',
  },
  // librogame-setup-wizard (4 frames) — Task 3 #2174
  {
    slug: 'pages-librogame-setup-wizard--frame-01-step-1-name',
    file: 'librogame-setup-wizard-01-step1-name.png',
  },
  {
    slug: 'pages-librogame-setup-wizard--frame-02-step-2-players',
    file: 'librogame-setup-wizard-02-step2-players.png',
  },
  {
    slug: 'pages-librogame-setup-wizard--frame-03-step-3-confirm',
    file: 'librogame-setup-wizard-03-step3-confirm.png',
  },
  {
    slug: 'pages-librogame-setup-wizard--frame-04-validation-err',
    file: 'librogame-setup-wizard-04-validation-err.png',
  },
  // librogame-encounter-cheatsheet (4 frames) — Task 4 #2174
  {
    slug: 'pages-librogame-encounter-cheatsheet--frame-01-idle',
    file: 'librogame-encounter-cheatsheet-01-idle.png',
  },
  {
    slug: 'pages-librogame-encounter-cheatsheet--frame-02-parsing',
    file: 'librogame-encounter-cheatsheet-02-parsing.png',
  },
  {
    slug: 'pages-librogame-encounter-cheatsheet--frame-03-rendered',
    file: 'librogame-encounter-cheatsheet-03-rendered.png',
  },
  {
    slug: 'pages-librogame-encounter-cheatsheet--frame-04-error',
    file: 'librogame-encounter-cheatsheet-04-error.png',
  },
  // librogame-play-session (4 frames) — Task 5 #2174
  {
    slug: 'pages-librogame-play-session--frame-01-story-tab',
    file: 'librogame-play-session-01-story-tab.png',
  },
  {
    slug: 'pages-librogame-play-session--frame-02-encounter-tab',
    file: 'librogame-play-session-02-encounter-tab.png',
  },
  {
    slug: 'pages-librogame-play-session--frame-03-chat-overlay',
    file: 'librogame-play-session-03-chat-overlay.png',
  },
  {
    slug: 'pages-librogame-play-session--frame-04-glossary-inline',
    file: 'librogame-play-session-04-glossary-inline.png',
  },
  // librogame-resume-picker (5 frames) — Task 7 #2174
  {
    slug: 'pages-librogame-resume-picker--frame-01-first-time',
    file: 'librogame-resume-picker-01-first-time.png',
  },
  {
    slug: 'pages-librogame-resume-picker--frame-02-single-resume',
    file: 'librogame-resume-picker-02-single-resume.png',
  },
  {
    slug: 'pages-librogame-resume-picker--frame-03-multi-campaign',
    file: 'librogame-resume-picker-03-multi-campaign.png',
  },
  {
    slug: 'pages-librogame-resume-picker--frame-04-stale-warning',
    file: 'librogame-resume-picker-04-stale-warning.png',
  },
  {
    slug: 'pages-librogame-resume-picker--frame-05-with-tutorial',
    file: 'librogame-resume-picker-05-with-tutorial.png',
  },
  // librogame-translate-viewer (12 frames, 13 mockup states — state J merged into Frame05) — Task 6 #2174
  {
    slug: 'pages-librogame-translate-viewer--frame-01-idle',
    file: 'librogame-translate-viewer-01-idle.png',
  },
  {
    slug: 'pages-librogame-translate-viewer--frame-02-segmenting',
    file: 'librogame-translate-viewer-02-segmenting.png',
  },
  {
    slug: 'pages-librogame-translate-viewer--frame-03-segments-list',
    file: 'librogame-translate-viewer-03-segments-list.png',
  },
  {
    slug: 'pages-librogame-translate-viewer--frame-04-translating',
    file: 'librogame-translate-viewer-04-translating.png',
  },
  {
    slug: 'pages-librogame-translate-viewer--frame-05-translated',
    file: 'librogame-translate-viewer-05-translated.png',
  },
  {
    slug: 'pages-librogame-translate-viewer--frame-06-low-confidence',
    file: 'librogame-translate-viewer-06-low-confidence.png',
  },
  {
    slug: 'pages-librogame-translate-viewer--frame-07-loading-4-step',
    file: 'librogame-translate-viewer-07-loading-4-step.png',
  },
  {
    slug: 'pages-librogame-translate-viewer--frame-08-reader-mode',
    file: 'librogame-translate-viewer-08-reader-mode.png',
  },
  {
    slug: 'pages-librogame-translate-viewer--frame-09-wake-lock',
    file: 'librogame-translate-viewer-09-wake-lock.png',
  },
  {
    slug: 'pages-librogame-translate-viewer--frame-10-lang-badge-high',
    file: 'librogame-translate-viewer-10-lang-badge-high.png',
  },
  {
    slug: 'pages-librogame-translate-viewer--frame-11-lang-override-modal',
    file: 'librogame-translate-viewer-11-lang-override-modal.png',
  },
  {
    slug: 'pages-librogame-translate-viewer--frame-12-manual-input-mode',
    file: 'librogame-translate-viewer-12-manual-input-mode.png',
  },
  // librogame-glossary-editor (6 frames) — Task 8 #2174
  {
    slug: 'pages-librogame-glossary-editor--frame-01-edit-pristine',
    file: 'librogame-glossary-editor-01-edit-pristine.png',
  },
  {
    slug: 'pages-librogame-glossary-editor--frame-02-edited',
    file: 'librogame-glossary-editor-02-edited.png',
  },
  {
    slug: 'pages-librogame-glossary-editor--frame-03-save-error',
    file: 'librogame-glossary-editor-03-save-error.png',
  },
  {
    slug: 'pages-librogame-glossary-editor--frame-04-collision',
    file: 'librogame-glossary-editor-04-collision.png',
  },
  {
    slug: 'pages-librogame-glossary-editor--frame-05-bulk-import',
    file: 'librogame-glossary-editor-05-bulk-import.png',
  },
  {
    slug: 'pages-librogame-glossary-editor--frame-06-variants',
    file: 'librogame-glossary-editor-06-variants.png',
  },
  // librogame-quota-credits (7 frames) — Task 9 #2174
  {
    slug: 'pages-librogame-quota-credits--frame-01-step-1-quota',
    file: 'librogame-quota-credits-01-step1-quota.png',
  },
  {
    slug: 'pages-librogame-quota-credits--frame-02-step-2-picker',
    file: 'librogame-quota-credits-02-step2-picker.png',
  },
  {
    slug: 'pages-librogame-quota-credits--frame-03-step-3-form',
    file: 'librogame-quota-credits-03-step3-form.png',
  },
  {
    slug: 'pages-librogame-quota-credits--frame-04-step-3-loading',
    file: 'librogame-quota-credits-04-step3-loading.png',
  },
  {
    slug: 'pages-librogame-quota-credits--frame-05-step-3-failed',
    file: 'librogame-quota-credits-05-step3-failed.png',
  },
  {
    slug: 'pages-librogame-quota-credits--frame-06-step-4-success',
    file: 'librogame-quota-credits-06-step4-success.png',
  },
  {
    slug: 'pages-librogame-quota-credits--frame-07-soft-warning',
    file: 'librogame-quota-credits-07-soft-warning.png',
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
