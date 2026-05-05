/**
 * Visual contract — /players route migrata vs mockup baseline.
 *
 * Issue #682 (Wave 4 D1 Players migration)
 * V2 Migration Phase 1 (Wave 4 D1).
 *
 * Strategia (TDD red→green):
 *   - **Red** (corrente, pre-bootstrap): la baseline PNG non esiste ancora.
 *   - **Green**: route v2 attiva (`(authenticated)/players/_components/
 *     PlayersLibraryView.tsx`) con hero + filters + results grid.
 *
 * Bootstrap baseline (one-time, post-migration):
 *   `gh workflow run visual-regression-migrated.yml --ref feature/issue-682-wave-4-d1-players-fe-v2 \
 *     -f mode=bootstrap -f project_filter=both`
 *   Il workflow setta `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` prima del
 *   build, così il fixture deterministico (5 entries: Wingspan, Azul, Catan,
 *   Terraforming Mars, Splendor) viene short-circuitato dal
 *   `PlayersLibraryView` orchestrator senza richiedere il backend API.
 *
 * Strategia ID (visual-test fixture, NOT a backend fetch):
 *   - Il `PlayersLibraryView` orchestrator legge `IS_VISUAL_TEST_BUILD` da
 *     `@/lib/players/players-visual-test-fixture` e sostituisce
 *     `statsQuery.data` con entries deterministiche.
 *   - In production deploy il fixture è dead code (constant-fold) — NON
 *     espone alcun shape pubblico.
 *
 * Hybrid masking:
 *   Le zone marcate `data-dynamic` sono mascherate per evitare flake.
 *
 * Auth bypass:
 *   La route `/players` è sotto `(authenticated)/` ma il layout non gate-keepa
 *   server-side; `PLAYWRIGHT_AUTH_BYPASS=true` settato in
 *   `playwright.config.ts:434` (webServer env) garantisce navigazione senza
 *   session cookie. `seedAuthSession` + `mockAuthEndpoints` soddisfano il
 *   `proxy.ts` middleware gate + i flussi React-side `AuthProvider` /
 *   `useSessionCheck` (Wave B.1 lesson learned).
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const SLUG = 'sp4-players-index';

async function waitForLibraryReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-slot="players-library-view"]', { timeout: 30_000 });
  // Entries dal fixture sono 5: aspetta che il results grid abbia almeno il
  // primo grid item prima di catturare lo screenshot. In default state la
  // grid è popolata (no skeleton, no empty-state).
  // Players usa <button data-slot="players-results-grid-item"> (non un link).
  await page.waitForSelector('[data-slot="players-results-grid-item"]', { timeout: 10_000 });

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

test.describe('V2 Visual Migrated — /players matches mockup baseline', () => {
  test.describe.configure({ retries: 0 });

  test('Players index desktop 1280x720 — default state matches sp4-players-index mockup', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/players', { waitUntil: 'domcontentloaded' });
    await waitForLibraryReady(page);

    await expect(page).toHaveScreenshot(`${SLUG}-desktop.png`, {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('Players index mobile 375x812 — default state matches sp4-players-index mockup', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/players', { waitUntil: 'domcontentloaded' });
    await waitForLibraryReady(page);

    await expect(page).toHaveScreenshot(`${SLUG}-mobile.png`, {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
