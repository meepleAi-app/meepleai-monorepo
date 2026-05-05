/**
 * Visual contract — /join route migrata vs mockup baseline.
 *
 * Issue #589 (Wave A.2 Join migration) · umbrella #579 V2 Migration Phase 1.
 *
 * Strategia (TDD red→green):
 *   - **Red** (corrente): la route `/join` non esiste ancora oppure non matcha
 *     il mockup `sp3-join` → snapshot fallisce.
 *   - **Green**: route migrata a v2 (`(public)/join/page-client.tsx`) con
 *     hero centrato + form card mobile, hero sx + form card sticky desktop.
 *
 * Bootstrap baseline (one-time, post-migration):
 *   La PNG attesa è la stessa committata in Phase 0 PR #575 a:
 *     apps/web/e2e/visual-mockups/baseline.spec.ts-snapshots/sp3-join-mockup-baseline-{desktop,mobile}-linux.png
 *   Per riusarla qui senza duplicarla:
 *     pnpm exec node scripts/sync-migrated-baselines.cjs sp3-join
 *   Lo script copia le PNG mockup → snapshot dir di questa spec con il
 *   nome che Playwright si aspetta (`sp3-join-visual-migrated-<project>-<platform>.png`).
 *
 * Hybrid masking:
 *   Le zone marcate `data-dynamic` (timestamps, contatori live, ecc) sono
 *   mascherate per evitare flake. La form al primo render è statica — niente
 *   timer/contatori — quindi la copertura `[data-dynamic]` è preventiva.
 */
import { test, expect, type Page } from '@playwright/test';

const JOIN_SLUG = 'sp3-join';

/**
 * Wait per assicurare che la route /join sia completamente renderizzata:
 *   1. Form card mounted (testid sentinella).
 *   2. Fonts caricati (Quicksand / Nunito / JetBrains Mono).
 *   3. Doppio RAF per flush layout/paint dopo font swap.
 */
async function waitForJoinReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-testid="join-form-card"]', { timeout: 30_000 });

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

test.describe('V2 Visual Migrated — /join matches mockup baseline', () => {
  test.describe.configure({ retries: 0 });

  test('Join default state matches sp3-join mockup', async ({ page }) => {
    await page.goto('/join', { waitUntil: 'networkidle' });
    await waitForJoinReady(page);

    await expect(page).toHaveScreenshot(`${JOIN_SLUG}.png`, {
      fullPage: true,
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
