/**
 * Asse D follow-up P1 — Polymorphic ScoreType editor E2E skeleton.
 *
 * Full data-driven assertions require:
 *  - Authenticated session (E2E auth fixture)
 *  - Live session seeded with `scoringType !== 'Points'`
 *  - Host user binding so the polymorphic-edit path renders
 *
 * Those preconditions ride on the asse-D follow-up P4 cross-asse E2E harness
 * (not yet shipped); this skeleton verifies that the scores route mounts the
 * polymorphic editor wrapper *or* redirects to login, depending on auth state,
 * to guard against silent regressions in routing or layout while we wait for
 * the harness.
 */

import { expect, test } from '@playwright/test';

test.describe('Asse D P1 polymorphic ScoreType editor', () => {
  test.skip(
    ({ browserName }) => browserName !== 'chromium',
    'Chromium-only for E2E skeleton speed'
  );

  test('scores page route mounts (skeleton)', async ({ page }) => {
    await page.goto('/sessions/live/test-session-id/scores');

    const polymorphicEditor = page.locator('[data-testid="polymorphic-scores-page"]');
    const isAuthenticated = (await polymorphicEditor.count()) > 0;

    if (isAuthenticated) {
      // Authenticated path — the polymorphic editor wrapper is in the DOM.
      await expect(polymorphicEditor).toBeVisible();
    } else {
      // Unauthenticated path — verify the auth redirect happened (covers
      // `/login`, `/auth/*`, `/sign-in`, etc.).
      await expect(page).toHaveURL(/\/(login|auth|sign-in)/);
    }
  });
});
