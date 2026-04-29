/**
 * Visual contract — /games?tab=library route migrata vs mockup baseline.
 *
 * Issue #633 (Wave B.1 Games library migration) · umbrella #580
 * V2 Migration Phase 1 (Wave B kickoff).
 *
 * Strategia (TDD red→green):
 *   - **Red** (corrente, pre-bootstrap): la baseline PNG non esiste ancora.
 *   - **Green**: route v2 attiva (`(authenticated)/games/_components/
 *     GamesLibraryView.tsx`) con hero + filters inline + results grid.
 *
 * Bootstrap baseline (one-time, post-migration):
 *   `gh workflow run 266963272 --ref feature/issue-633-games-fe-v2 \
 *     -f mode=bootstrap -f project_filter=both`
 *   Il workflow setta `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` prima del
 *   build, così il fixture deterministico (`FIXTURE_DEFAULT`, 5 entries) viene
 *   short-circuitato dal hook `useLibrary` senza richiedere il backend API.
 *
 * Strategia ID (visual-test fixture, NOT a backend fetch):
 *   - Il GamesLibraryView orchestrator legge `IS_VISUAL_TEST_BUILD` da
 *     `@/lib/games/visual-test-fixture` e sostituisce `libraryQuery.data` con
 *     entries deterministiche (Catan, Terraforming Mars, Wingspan, Azul,
 *     Carcassonne).
 *   - In production deploy il fixture è dead code (constant-fold) — NON
 *     espone alcun shape pubblico.
 *
 * Hybrid masking:
 *   Le zone marcate `data-dynamic` sono mascherate per evitare flake.
 *
 * Auth bypass:
 *   La route `/games?tab=library` è sotto `(authenticated)/` ma il layout
 *   non gate-keepa server-side; `PLAYWRIGHT_AUTH_BYPASS=true` settato in
 *   `playwright.config.ts:434` (webServer env) garantisce navigazione
 *   senza session cookie.
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const SLUG = 'sp4-games-index';

async function waitForLibraryReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-slot="games-library-view"]', { timeout: 30_000 });
  // Entries dal fixture sono 5: aspetta che il results grid abbia almeno il
  // primo card link prima di catturare lo screenshot. In default state la
  // grid è popolata (no skeleton, no empty-state).
  await page.waitForSelector('[data-slot="games-results-grid-link"]', { timeout: 10_000 });

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

test.describe('V2 Visual Migrated — /games?tab=library matches mockup baseline', () => {
  test.describe.configure({ retries: 0 });

  test('Games library default tab matches sp4-games-index mockup', async ({ page }) => {
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/games?tab=library', { waitUntil: 'networkidle' });
    await waitForLibraryReady(page);

    await expect(page).toHaveScreenshot(`${SLUG}.png`, {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
