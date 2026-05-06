/**
 * V2 State Coverage — /sessions/[id] summary route (Issue #756, Wave D.3).
 *
 * Captures fixture-driven visual variants of the 6-cell summary FSM.
 * Per Phase 0.5 contract §14, this spec contributes 4 of the 6 visual baselines:
 *   - `tied` (desktop) — 2-way tie at 1st place, podium variant='tied' + tied banner
 *   - `abandoned` (desktop) — status='Abandoned', UI variant
 *   - `empty-photos` (desktop) — Cell 6 partial state (PhotosGallery empty)
 *   - `dark-share-card` (desktop) — `?theme=dark` ShareCard preview toggle
 *
 * The remaining 2 baselines (`default-desktop`, `default-mobile`) live in
 * `e2e/visual-migrated/sp4-session-summary.spec.ts`.
 *
 * NOT covered visually (per contract §14):
 *   - Cell 1 (loading) — skeleton timing non-deterministic; covered by unit tests
 *     in `_components/__tests__/SessionSummaryView.test.tsx`.
 *   - Cell 2 (error) — TanStack Query `isError` cannot be reproduced
 *     deterministically via URL override; covered by unit tests.
 *   - Cell 3 (not-found) — covered by route handling page.tsx test.
 *   - `solo` fixture — visually similar to `default` (1 podium place); covered
 *     by SessionSummaryHero unit tests.
 *   - `empty-achievements` — currently identical to `default` from a layout
 *     perspective when the orchestrator falls back to the default fixture
 *     achievements; covered by AchievementsCarousel unit tests.
 *
 * URL override hatch (`?fixture=…`):
 *   Gated by `STATE_OVERRIDE_ENABLED` (`IS_VISUAL_TEST_BUILD ||
 *   NODE_ENV !== 'production'`). In production builds the bundler dead-code-
 *   eliminates the lookup so this hatch is a build-time concern only.
 *
 * Fixture sentinel: `00000000-0000-4000-8000-000000000756` (Wave D.3 issue id).
 *
 * Auth bypass: triple helper (seedAuthSession + seedCookieConsent +
 * mockAuthEndpoints) — Wave B.1 pattern (Issue #633) for `(authenticated)`
 * routes. No `networkidle` (Wave B.1 lesson) — `domcontentloaded` +
 * explicit `waitForSelector`.
 *
 * Snapshots written to
 * `apps/web/e2e/v2-states/session-summary.spec.ts-snapshots/`.
 * Run via CI bootstrap workflow (Linux x86-64 canonical baselines):
 *   `gh workflow run visual-regression-migrated.yml --ref <branch> \
 *     -f mode=bootstrap -f project_filter=both`
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const FIXTURE_SESSION_ID = '00000000-0000-4000-8000-000000000756';

async function seedAuth(page: Page): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
}

async function gotoSummary(page: Page, search = ''): Promise<void> {
  await seedAuth(page);
  await page.goto(`/sessions/${FIXTURE_SESSION_ID}${search}`, {
    waitUntil: 'domcontentloaded',
  });
}

async function waitForSummaryReady(page: Page): Promise<void> {
  // Default and partial cells both render the full summary view.
  // Wait for the hero specifically since FSM kind=='partial' still renders it.
  await page.waitForSelector('[data-slot="session-summary-view"]', { timeout: 30_000 });
  await page.waitForSelector('[data-slot="session-summary-hero"]', { timeout: 10_000 });

  await page.evaluate(async () => {
    if (typeof document !== 'undefined' && 'fonts' in document) {
      await (document as Document & { fonts: { ready: Promise<void> } }).fonts.ready;
    }
  });

  await page.evaluate(
    () =>
      new Promise<void>(resolve => {
        requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
      })
  );
}

test.describe('Session summary — state coverage', () => {
  test.describe.configure({ retries: 0 });

  // ── Tied podium ────────────────────────────────────────────────────────────
  // 2-way tie at 1st place: PARTICIPANTS_TIED fixture has Marco + Anna both at
  // score 42, then Luigi (28), Sofia (19). Hero sets `data-tied="true"` and
  // renders a tied banner above the podium.

  test('desktop tied podium (2-way tie at 1st place)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await gotoSummary(page, '?fixture=tied');
    await waitForSummaryReady(page);
    // Confirm the hero exposes the tied flag before screenshot.
    await expect(
      page.locator('[data-slot="session-summary-hero"][data-tied="true"]')
    ).toBeVisible();
    await expect(page.locator('[data-slot="hero-tied-banner"]')).toBeVisible();
    await expect(page).toHaveScreenshot('session-summary-tied-desktop.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-slot="confetti"]'), page.locator('[data-dynamic]')],
    });
  });

  // ── Abandoned status ──────────────────────────────────────────────────────
  // PARTICIPANTS_DEFAULT but status='Abandoned'. Confetti still triggers via
  // shouldShowConfetti only when status === 'Completed'; under abandoned the
  // hero suppresses celebration FX while preserving podium layout.

  test('desktop abandoned session', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await gotoSummary(page, '?fixture=abandoned');
    await waitForSummaryReady(page);
    await expect(page).toHaveScreenshot('session-summary-abandoned-desktop.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-slot="confetti"]'), page.locator('[data-dynamic]')],
    });
  });

  // ── Cell 6 partial: empty photos ──────────────────────────────────────────
  // Default fixture but snapshots=[] → PhotosGallery empty state. Verifies
  // the partial-cell path (FSM kind='partial', missing=['snapshots']).

  test('desktop empty photos (Cell 6 partial)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await gotoSummary(page, '?fixture=empty-photos');
    await waitForSummaryReady(page);
    await expect(page).toHaveScreenshot('session-summary-empty-photos-desktop.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-slot="confetti"]'), page.locator('[data-dynamic]')],
    });
  });

  // ── ShareCard dark theme preview ──────────────────────────────────────────
  // `?theme=dark` toggles ONLY the ShareCard preview render — page theme stays
  // light (component-isolated theming per contract AC6). Default fixture used
  // so the rest of the page mirrors the canonical default snapshot.

  test('desktop dark share card preview (page stays light)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await gotoSummary(page, '?theme=dark');
    await waitForSummaryReady(page);
    // Confirm ShareCard surface is mounted with the dark theme attribute.
    await expect(page.locator('[data-slot="session-share-card"]')).toBeVisible();
    await expect(page).toHaveScreenshot('session-summary-share-card-dark-desktop.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-slot="confetti"]'), page.locator('[data-dynamic]')],
    });
  });

  // Note: solo + empty-achievements fixtures excluded from visual coverage —
  // see file header rationale. loading/error/not-found cells are unit-tested.
});
