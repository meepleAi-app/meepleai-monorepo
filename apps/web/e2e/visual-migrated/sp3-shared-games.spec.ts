/**
 * Visual contract — /shared-games route migrata vs mockup baseline.
 *
 * Issue #596 (Wave A.3b Shared Games migration) · umbrella #579 V2 Migration Phase 1.
 *
 * Strategia (TDD red→green):
 *   - **Red** (corrente, pre-bootstrap): la baseline PNG non esiste ancora.
 *   - **Green**: route v2 attiva (`(public)/shared-games/page-client.tsx`)
 *     con hero + filtri + grid 3/2/1 col + sidebar contributors desktop.
 *
 * Bootstrap baseline (one-time, post-migration):
 *   `gh workflow run 266963272 --ref feature/issue-596-shared-games-fe-v2 \
 *     -f mode=bootstrap -f project_filter=both`
 *   Il runner Linux genera PNG canonical viewport reali (375×812 mobile,
 *   1440×900 desktop) e le carica come artifact. Scaricare con:
 *   `gh run download <run-id> -n visual-migrated-baselines-<n>` e committare.
 *
 * Hybrid masking:
 *   Le zone marcate `data-dynamic` (rating live, contatori) sono mascherate
 *   per evitare flake. Al primo render con SSR seed la grid è statica.
 */
import { test, expect, type Page } from '@playwright/test';

const SLUG = 'sp3-shared-games';

/**
 * Wait per assicurare che la route /shared-games sia completamente renderizzata:
 *   1. Page sentinel mounted (`[data-testid="shared-games-page"]`).
 *   2. Fonts caricati (Quicksand / Nunito / JetBrains Mono).
 *   3. Doppio RAF per flush layout/paint dopo font swap.
 */
async function waitForSharedGamesReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-testid="shared-games-page"]', { timeout: 30_000 });

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

test.describe('V2 Visual Migrated — /shared-games matches mockup baseline', () => {
  test.describe.configure({ retries: 0 });

  test('Shared games default grid matches sp3-shared-games mockup', async ({ page }) => {
    await page.goto('/shared-games', { waitUntil: 'networkidle' });
    await waitForSharedGamesReady(page);

    await expect(page).toHaveScreenshot(`${SLUG}.png`, {
      fullPage: true,
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
