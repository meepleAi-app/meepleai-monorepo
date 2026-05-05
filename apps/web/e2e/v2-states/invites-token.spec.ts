/**
 * V2 State Coverage — /invites/[token] route (Issue #611, Wave A.5b).
 *
 * Captures all 7 surface states:
 *   default | logged-in | accepted-success | declined |
 *   token-expired | token-invalid | already-accepted
 *
 * Uses test-only `?state=...` query param (guarded by
 * `STATE_OVERRIDE_ENABLED = NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD`
 * in `app/(public)/invites/[token]/page-client.tsx`).
 *
 * Strategia token (visual-test fixture, NOT a backend fetch):
 *   Imports `VISUAL_TEST_FIXTURE_TOKEN` — the SSR fetch is short-circuited
 *   by `loadInitialData()` so visual tests don't depend on a live backend
 *   API. See `src/lib/invites/visual-test-fixture.ts`. The fixture supplies
 *   `PublicGameNightInvitation` data shape with deterministic host/game/
 *   schedule fields so all 7 states render with consistent content; the
 *   `?state=...` override forces the FSM transition for screenshots.
 *
 * Snapshots written to `apps/web/e2e/v2-states/invites-token.spec.ts-snapshots/`.
 * Run via CI bootstrap workflow (Linux x86-64 canonical baselines):
 *   `gh workflow run 266963272 --ref <branch> -f mode=bootstrap -f project_filter=both`
 */
import { test, expect, type Page } from '@playwright/test';

import { VISUAL_TEST_FIXTURE_TOKEN } from '../../src/lib/invites/visual-test-fixture';

/**
 * Seed cookie-consent localStorage entry BEFORE the page loads so the
 * GDPR banner (`CookieConsentBanner`) never mounts. Wave A.4 pattern:
 * banner is a known visual-flake source on mobile fullPage screenshots.
 *
 * Contract mirrors `apps/web/src/lib/cookie-consent.ts`:
 *   key     = 'meepleai-cookie-consent'
 *   version = '1.0'
 */
async function seedCookieConsent(page: Page): Promise<void> {
  await page.addInitScript(() => {
    try {
      window.localStorage.setItem(
        'meepleai-cookie-consent',
        JSON.stringify({
          version: '1.0',
          essential: true,
          analytics: false,
          functional: false,
          timestamp: '2026-01-01T00:00:00.000Z',
        })
      );
    } catch {
      // localStorage unavailable — banner may render, accept the risk.
    }
  });
}

async function waitForInviteReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-slot="invites-token-page"]', { timeout: 30_000 });
  await page.waitForSelector('[data-slot="invites-token-card"]', { timeout: 30_000 });

  await page.evaluate(async () => {
    if (typeof document !== 'undefined' && 'fonts' in document) {
      await (document as Document & { fonts: { ready: Promise<void> } }).fonts.ready;
    }
  });

  // Quad-RAF settle: 2 frames to commit DOM updates, 2 more to paint and
  // settle compositor. Wave A.4 pattern — under CI virtualization a render
  // cycle can span 2-3 frames, single double-RAF was insufficient on mobile.
  await page.evaluate(
    () =>
      new Promise<void>(resolve => {
        requestAnimationFrame(() =>
          requestAnimationFrame(() =>
            requestAnimationFrame(() => requestAnimationFrame(() => resolve()))
          )
        );
      })
  );
}

async function waitForState(page: Page, expected: string): Promise<void> {
  await expect(page.locator('[data-slot="invites-token-page"]')).toHaveAttribute(
    'data-state',
    expected,
    { timeout: 5000 }
  );
}

test.describe('Invites token — state coverage', () => {
  test.describe.configure({ retries: 0 });

  test('default state (anonymous)', async ({ page }) => {
    await seedCookieConsent(page);
    await page.goto(`/invites/${VISUAL_TEST_FIXTURE_TOKEN}`, { waitUntil: 'networkidle' });
    await waitForInviteReady(page);
    // No override — useAuthUser returns no user in CI test env, so state
    // derives to 'default'. Verify before screenshot.
    await waitForState(page, 'default');
    await expect(page).toHaveScreenshot('invites-token-default.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('logged-in state', async ({ page }) => {
    await seedCookieConsent(page);
    await page.goto(`/invites/${VISUAL_TEST_FIXTURE_TOKEN}?state=logged-in`, {
      waitUntil: 'networkidle',
    });
    await waitForInviteReady(page);
    await waitForState(page, 'logged-in');
    await expect(page).toHaveScreenshot('invites-token-logged-in.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('accepted-success state', async ({ page }) => {
    await seedCookieConsent(page);
    await page.goto(`/invites/${VISUAL_TEST_FIXTURE_TOKEN}?state=accepted-success`, {
      waitUntil: 'networkidle',
    });
    await waitForInviteReady(page);
    await waitForState(page, 'accepted-success');
    await expect(page).toHaveScreenshot('invites-token-accepted-success.png', {
      fullPage: true,
      animations: 'disabled',
      // Confetti is GPU-only transform/opacity; `animations: 'disabled'`
      // freezes them mid-flight. Mask the confetti container in case the
      // freeze frame still differs run-over-run.
      mask: [page.locator('[data-dynamic]'), page.locator('[data-testid="invite-confetti"]')],
    });
  });

  test('declined state', async ({ page }) => {
    await seedCookieConsent(page);
    await page.goto(`/invites/${VISUAL_TEST_FIXTURE_TOKEN}?state=declined`, {
      waitUntil: 'networkidle',
    });
    await waitForInviteReady(page);
    await waitForState(page, 'declined');
    await expect(page).toHaveScreenshot('invites-token-declined.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('token-expired state', async ({ page }) => {
    await seedCookieConsent(page);
    await page.goto(`/invites/${VISUAL_TEST_FIXTURE_TOKEN}?state=token-expired`, {
      waitUntil: 'networkidle',
    });
    await waitForInviteReady(page);
    await waitForState(page, 'token-expired');
    await expect(page).toHaveScreenshot('invites-token-token-expired.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('token-invalid state', async ({ page }) => {
    await seedCookieConsent(page);
    await page.goto(`/invites/${VISUAL_TEST_FIXTURE_TOKEN}?state=token-invalid`, {
      waitUntil: 'networkidle',
    });
    await waitForInviteReady(page);
    await waitForState(page, 'token-invalid');
    await expect(page).toHaveScreenshot('invites-token-token-invalid.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('already-accepted state', async ({ page }) => {
    await seedCookieConsent(page);
    await page.goto(`/invites/${VISUAL_TEST_FIXTURE_TOKEN}?state=already-accepted`, {
      waitUntil: 'networkidle',
    });
    await waitForInviteReady(page);
    await waitForState(page, 'already-accepted');
    await expect(page).toHaveScreenshot('invites-token-already-accepted.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
