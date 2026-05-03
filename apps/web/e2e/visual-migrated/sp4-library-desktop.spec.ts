/**
 * Visual contract — /library desktop route migrata vs mockup baseline.
 *
 * Issue #574 (Wave B.3 Library desktop migration) · umbrella #580
 * V2 Migration Phase 1 (Wave B.3 brownfield big-bang).
 *
 * Strategia (TDD red→green):
 *   - **Red** (corrente, pre-bootstrap): la baseline PNG non esiste ancora.
 *   - **Green**: route v2 attiva (`(authenticated)/library/_components/
 *     LibraryHubV2.tsx`) con hero desktop + tabs (all/kb/loaned) + toolbar
 *     (search + sort + view toggle) + LibraryHybridGrid + RecentActivityRail.
 *
 * Bootstrap baseline (one-time, post-migration):
 *   `gh workflow run visual-regression-migrated.yml --ref feature/issue-574-library-fe-v2 \
 *     -f mode=bootstrap -f project_filter=both`
 *   Il workflow setta `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` prima del
 *   build, così il fixture deterministico (12 entries: hero stats + 3-tab grid
 *   baseline) viene short-circuitato dal `LibraryHubV2` orchestrator senza
 *   richiedere il backend API.
 *
 * Strategia ID (visual-test fixture, NOT a backend fetch):
 *   - Il `LibraryHubV2` orchestrator legge `IS_VISUAL_TEST_BUILD` da
 *     `@/lib/library/visual-test-fixture` e sostituisce `libraryQuery.data`
 *     con entries deterministiche (12 mixed: 7 Owned, 2 Wishlist, 2
 *     InPrestito, 1 Nuovo; 5 hasKb + 1 kb-in-flight per coverage).
 *   - In production deploy il fixture è dead code (constant-fold) — NON
 *     espone alcun shape pubblico.
 *
 * Hybrid masking:
 *   Le zone marcate `data-dynamic` sono mascherate per evitare flake.
 *
 * Auth bypass:
 *   La route `/library` è sotto `(authenticated)/` ma il layout non gate-keepa
 *   server-side; `PLAYWRIGHT_AUTH_BYPASS=true` settato in
 *   `playwright.config.ts:434` (webServer env) garantisce navigazione senza
 *   session cookie. `seedAuthSession` + `seedCookieConsent` + `mockAuthEndpoints`
 *   soddisfano il `proxy.ts` middleware gate + i flussi React-side `AuthProvider`
 *   / `useSessionCheck` (Wave B.1/B.2 lesson learned, triple-helper pattern).
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const SLUG = 'sp4-library-desktop';

async function waitForLibraryReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-slot="library-hub-v2"]', { timeout: 30_000 });
  // Default state expects grid populato (12 entries dal fixture). Aspetta
  // almeno la prima card prima di catturare lo screenshot.
  await page.waitForSelector('[data-slot="library-grid-card"]', { timeout: 10_000 });

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

test.describe('V2 Visual Migrated — /library matches mockup baseline', () => {
  test.describe.configure({ retries: 0 });

  test('Library desktop default state matches sp4-library-desktop mockup', async ({ page }) => {
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/library', { waitUntil: 'domcontentloaded' });
    await waitForLibraryReady(page);

    await expect(page).toHaveScreenshot(`${SLUG}.png`, {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
