/**
 * @mockup DS-17 Phase C-1 cluster sp6-7-nano (#2166)
 *
 * sp6-7-nano cluster snapshot suite — 4 SHIP mockup × N Frame exports.
 * Mirrors apps/web/e2e/storybook/{library,game-detail,auth}.snapshot.spec.ts
 * pattern (Phase 2.5 + Phase 4 prelude + DS-17-9 auth).
 *
 * Refs:
 * - Spec: docs/superpowers/specs/2026-06-11-ds-17-11-sp6-7-nano-cluster-design.md
 * - Phase D-1 (game-night stems): #2174 — adds sp7-game-night-transition + sp7-game-night-join-public
 */

import { test, expect } from '@playwright/test';

const FRAMES = [
  // sp7-game-night-new (10 Frame + 2 State = 12 stories)
  {
    slug: 'pages-sp7-game-night-new--frame-01-step-1-quando',
    file: 'game-night-new-01-step-1-quando.png',
  },
  {
    slug: 'pages-sp7-game-night-new--frame-02-step-1-warning',
    file: 'game-night-new-02-step-1-warning.png',
  },
  {
    slug: 'pages-sp7-game-night-new--frame-03-step-2-location',
    file: 'game-night-new-03-step-2-location.png',
  },
  {
    slug: 'pages-sp7-game-night-new--frame-04-step-3-empty',
    file: 'game-night-new-04-step-3-empty.png',
  },
  {
    slug: 'pages-sp7-game-night-new--frame-05-step-3-typing',
    file: 'game-night-new-05-step-3-typing.png',
  },
  {
    slug: 'pages-sp7-game-night-new--frame-06-step-3-filled',
    file: 'game-night-new-06-step-3-filled.png',
  },
  {
    slug: 'pages-sp7-game-night-new--frame-07-step-4-games',
    file: 'game-night-new-07-step-4-games.png',
  },
  {
    slug: 'pages-sp7-game-night-new--frame-08-step-4-decide-group',
    file: 'game-night-new-08-step-4-decide-group.png',
  },
  {
    slug: 'pages-sp7-game-night-new--frame-09-mobile-step-flow',
    file: 'game-night-new-09-mobile-step-flow.png',
  },
  {
    slug: 'pages-sp7-game-night-new--frame-10-desktop-split',
    file: 'game-night-new-10-desktop-split.png',
  },
  { slug: 'pages-sp7-game-night-new--loading', file: 'game-night-new-loading.png' },
  { slug: 'pages-sp7-game-night-new--error-state', file: 'game-night-new-error-state.png' },

  // sp7-game-night-detail-rsvp (12 Frame + 2 State = 14 stories)
  {
    slug: 'pages-sp7-game-night-detail-rsvp--frame-01-host-pending',
    file: 'game-night-detail-rsvp-01-host-pending.png',
  },
  {
    slug: 'pages-sp7-game-night-detail-rsvp--frame-02-host-ready',
    file: 'game-night-detail-rsvp-02-host-ready.png',
  },
  {
    slug: 'pages-sp7-game-night-detail-rsvp--frame-03-invitee-pending',
    file: 'game-night-detail-rsvp-03-invitee-pending.png',
  },
  {
    slug: 'pages-sp7-game-night-detail-rsvp--frame-04-invitee-confirmed',
    file: 'game-night-detail-rsvp-04-invitee-confirmed.png',
  },
  {
    slug: 'pages-sp7-game-night-detail-rsvp--frame-05-voting-active',
    file: 'game-night-detail-rsvp-05-voting-active.png',
  },
  {
    slug: 'pages-sp7-game-night-detail-rsvp--frame-06-voting-tied',
    file: 'game-night-detail-rsvp-06-voting-tied.png',
  },
  {
    slug: 'pages-sp7-game-night-detail-rsvp--frame-07-cancelled',
    file: 'game-night-detail-rsvp-07-cancelled.png',
  },
  {
    slug: 'pages-sp7-game-night-detail-rsvp--frame-08-in-progress',
    file: 'game-night-detail-rsvp-08-in-progress.png',
  },
  {
    slug: 'pages-sp7-game-night-detail-rsvp--frame-09-completed',
    file: 'game-night-detail-rsvp-09-completed.png',
  },
  {
    slug: 'pages-sp7-game-night-detail-rsvp--frame-10-mobile-tab-chat',
    file: 'game-night-detail-rsvp-10-mobile-tab-chat.png',
  },
  {
    slug: 'pages-sp7-game-night-detail-rsvp--frame-11-desktop-split-detail',
    file: 'game-night-detail-rsvp-11-desktop-split-detail.png',
  },
  {
    slug: 'pages-sp7-game-night-detail-rsvp--frame-12-desktop-split-voting',
    file: 'game-night-detail-rsvp-12-desktop-split-voting.png',
  },
  { slug: 'pages-sp7-game-night-detail-rsvp--loading', file: 'game-night-detail-rsvp-loading.png' },
  {
    slug: 'pages-sp7-game-night-detail-rsvp--error-state',
    file: 'game-night-detail-rsvp-error-state.png',
  },

  // sp7-game-night-live (10 Frame = 10 stories)
  {
    slug: 'pages-sp7-game-night-live--frame-01-game-1-in-progress',
    file: 'game-night-live-01-game-1-in-progress.png',
  },
  {
    slug: 'pages-sp7-game-night-live--frame-02-mid-night-game-2',
    file: 'game-night-live-02-mid-night-game-2.png',
  },
  {
    slug: 'pages-sp7-game-night-live--frame-03-transition-pending',
    file: 'game-night-live-03-transition-pending.png',
  },
  { slug: 'pages-sp7-game-night-live--frame-04-paused', file: 'game-night-live-04-paused.png' },
  {
    slug: 'pages-sp7-game-night-live--frame-05-diary-empty',
    file: 'game-night-live-05-diary-empty.png',
  },
  {
    slug: 'pages-sp7-game-night-live--frame-06-diary-widget-embedded',
    file: 'game-night-live-06-diary-widget-embedded.png',
  },
  {
    slug: 'pages-sp7-game-night-live--frame-07-mobile-tab-current',
    file: 'game-night-live-07-mobile-tab-current.png',
  },
  {
    slug: 'pages-sp7-game-night-live--frame-08-mobile-tab-planned',
    file: 'game-night-live-08-mobile-tab-planned.png',
  },
  {
    slug: 'pages-sp7-game-night-live--frame-09-mobile-tab-diary',
    file: 'game-night-live-09-mobile-tab-diary.png',
  },
  {
    slug: 'pages-sp7-game-night-live--frame-10-auto-save-toast',
    file: 'game-night-live-10-auto-save-toast.png',
  },

  // sp7-game-night-summary (6 Frame + 2 State = 8 stories)
  {
    slug: 'pages-sp7-game-night-summary--frame-01-summary-full',
    file: 'game-night-summary-01-summary-full.png',
  },
  {
    slug: 'pages-sp7-game-night-summary--frame-02-summary-no-photos',
    file: 'game-night-summary-02-summary-no-photos.png',
  },
  {
    slug: 'pages-sp7-game-night-summary--frame-03-summary-single-game',
    file: 'game-night-summary-03-summary-single-game.png',
  },
  {
    slug: 'pages-sp7-game-night-summary--frame-04-share-success-toast',
    file: 'game-night-summary-04-share-success-toast.png',
  },
  {
    slug: 'pages-sp7-game-night-summary--frame-05-archived',
    file: 'game-night-summary-05-archived.png',
  },
  {
    slug: 'pages-sp7-game-night-summary--frame-06-mobile-single-col',
    file: 'game-night-summary-06-mobile-single-col.png',
  },
  {
    slug: 'pages-sp7-game-night-summary--state-no-mvp',
    file: 'game-night-summary-state-no-mvp.png',
  },
  {
    slug: 'pages-sp7-game-night-summary--state-empty-all',
    file: 'game-night-summary-state-empty-all.png',
  },

  // sp7-game-night-transition (6 Frame + 1 State = 7 stories) — Phase D-1 #2174
  {
    slug: 'pages-sp7-game-night-transition--frame-01-default-two-col-split',
    file: 'game-night-transition-01-default-two-col-split.png',
  },
  {
    slug: 'pages-sp7-game-night-transition--frame-02-rules-loading-skeleton',
    file: 'game-night-transition-02-rules-loading-skeleton.png',
  },
  {
    slug: 'pages-sp7-game-night-transition--frame-03-rules-load-error',
    file: 'game-night-transition-03-rules-load-error.png',
  },
  {
    slug: 'pages-sp7-game-night-transition--frame-04-skip-confirm-submodal',
    file: 'game-night-transition-04-skip-confirm-submodal.png',
  },
  {
    slug: 'pages-sp7-game-night-transition--frame-05-end-night-confirm-submodal',
    file: 'game-night-transition-05-end-night-confirm-submodal.png',
  },
  {
    slug: 'pages-sp7-game-night-transition--frame-06-mobile-fullscreen',
    file: 'game-night-transition-06-mobile-fullscreen.png',
  },
  {
    slug: 'pages-sp7-game-night-transition--state-closed',
    file: 'game-night-transition-state-closed.png',
  },

  // sp7-game-night-join-public (6 Frame + 2 State = 8 stories) — Phase D-1 #2174
  {
    slug: 'pages-sp7-game-night-join-public--frame-01-pending-empty',
    file: 'game-night-join-public-01-pending-empty.png',
  },
  {
    slug: 'pages-sp7-game-night-join-public--frame-02-pending-filled',
    file: 'game-night-join-public-02-pending-filled.png',
  },
  {
    slug: 'pages-sp7-game-night-join-public--frame-03-submitting',
    file: 'game-night-join-public-03-submitting.png',
  },
  {
    slug: 'pages-sp7-game-night-join-public--frame-04-already-responded',
    file: 'game-night-join-public-04-already-responded.png',
  },
  {
    slug: 'pages-sp7-game-night-join-public--frame-05-token-expired',
    file: 'game-night-join-public-05-token-expired.png',
  },
  {
    slug: 'pages-sp7-game-night-join-public--frame-06-rate-limited',
    file: 'game-night-join-public-06-rate-limited.png',
  },
  {
    slug: 'pages-sp7-game-night-join-public--state-token-invalid',
    file: 'game-night-join-public-state-token-invalid.png',
  },
  {
    slug: 'pages-sp7-game-night-join-public--state-loading',
    file: 'game-night-join-public-state-loading.png',
  },
];

for (const { slug, file } of FRAMES) {
  test(`sp6-7-nano ${file.replace(/\.png$/, '')} matches snapshot`, async ({ page }) => {
    await page.goto(`/iframe.html?id=${slug}&viewMode=story`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(2000);
    await expect(page).toHaveScreenshot(file, { fullPage: true });
  });
}
