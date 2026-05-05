/**
 * Visual contract — /agents route migrata vs mockup baseline.
 *
 * Issue #634 (Wave B.2 Agents library migration) · umbrella #580
 * V2 Migration Phase 1 (Wave B.2 brownfield).
 *
 * Strategia (TDD red→green):
 *   - **Red** (corrente, pre-bootstrap): la baseline PNG non esiste ancora.
 *   - **Green**: route v2 attiva (`(authenticated)/agents/_components/
 *     AgentsLibraryView.tsx`) con hero + filters + results grid.
 *
 * Bootstrap baseline (one-time, post-migration):
 *   `gh workflow run visual-regression-migrated.yml --ref feature/issue-634-agents-fe-v2 \
 *     -f mode=bootstrap -f project_filter=both`
 *   Il workflow setta `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` prima del
 *   build, così il fixture deterministico (6 entries: 3 attivo + 2 in-setup +
 *   1 archiviato) viene short-circuitato dal `AgentsLibraryView` orchestrator
 *   senza richiedere il backend API.
 *
 * Strategia ID (visual-test fixture, NOT a backend fetch):
 *   - Il `AgentsLibraryView` orchestrator legge `IS_VISUAL_TEST_BUILD` da
 *     `@/lib/agents/visual-test-fixture` e sostituisce `agentsQuery.data` con
 *     entries deterministiche.
 *   - In production deploy il fixture è dead code (constant-fold) — NON
 *     espone alcun shape pubblico.
 *
 * Hybrid masking:
 *   Le zone marcate `data-dynamic` sono mascherate per evitare flake.
 *
 * Auth bypass:
 *   La route `/agents` è sotto `(authenticated)/` ma il layout non gate-keepa
 *   server-side; `PLAYWRIGHT_AUTH_BYPASS=true` settato in
 *   `playwright.config.ts:434` (webServer env) garantisce navigazione senza
 *   session cookie. `seedAuthSession` + `mockAuthEndpoints` soddisfano il
 *   `proxy.ts` middleware gate + i flussi React-side `AuthProvider` /
 *   `useSessionCheck` (Wave B.1 lesson learned).
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const SLUG = 'sp4-agents-index';

async function waitForLibraryReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-slot="agents-library-view"]', { timeout: 30_000 });
  // Entries dal fixture sono 6: aspetta che il results grid abbia almeno il
  // primo card link prima di catturare lo screenshot. In default state la
  // grid è popolata (no skeleton, no empty-state).
  await page.waitForSelector('[data-slot="agents-results-grid-link"]', { timeout: 10_000 });

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

test.describe('V2 Visual Migrated — /agents matches mockup baseline', () => {
  test.describe.configure({ retries: 0 });

  test('Agents library default state matches sp4-agents-index mockup', async ({ page }) => {
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/agents', { waitUntil: 'domcontentloaded' });
    await waitForLibraryReady(page);

    await expect(page).toHaveScreenshot(`${SLUG}.png`, {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
