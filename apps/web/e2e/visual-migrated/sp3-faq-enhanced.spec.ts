/**
 * Visual contract — /faq route migrata vs mockup baseline.
 *
 * Issue #583 (Wave A.1 FAQ pilot) · umbrella #578 V2 Migration Phase 1.
 *
 * Strategia (TDD red→green):
 *   - **Red** (corrente): la route `/faq` è ancora v1; lo screenshot non
 *     matcha la PNG mockup baseline → test fallisce.
 *   - **Green** (post-Task #16): route migrata a v2 con componenti, i18n,
 *     dati stub equivalenti a `data.js` del mockup → snapshot matcha.
 *
 * Bootstrap baseline (one-time, post-migration):
 *   La PNG attesa è la stessa committata in Phase 0 PR #575 a:
 *     apps/web/e2e/visual-mockups/baseline.spec.ts-snapshots/sp3-faq-enhanced-mockup-baseline-{desktop,mobile}-linux.png
 *   Per riusarla qui senza duplicarla, eseguire:
 *     pnpm exec node scripts/sync-migrated-baselines.cjs sp3-faq-enhanced \
 *       --secondary sp3-faq-enhanced-search-password
 *   Lo script copia le PNG mockup → snapshot dir di questa spec con il
 *   nome che Playwright si aspetta (`<arg>-visual-migrated-<project>-<platform>.png`).
 *   Il flag `--secondary` clona la stessa PNG su un nome aggiuntivo: il diff
 *   atteso fra default e search-results e' interno alla lista FAQ (mascherata
 *   via `data-dynamic`), non nel layout hero/header.
 *
 * Hybrid masking:
 *   Le zone marcate `data-dynamic` (search input value, contatori live)
 *   sono mascherate per evitare flake da rumore non visivo.
 */
import { test, expect, type Page } from '@playwright/test';

const FAQ_SLUG = 'sp3-faq-enhanced';

/**
 * Wait per assicurare che la route /faq sia completamente renderizzata:
 *   1. Hero, search bar, accordion items presenti.
 *   2. Fonts caricati (Quicksand / Nunito / JetBrains Mono).
 *   3. Doppio RAF per flush layout/paint dopo font swap.
 */
async function waitForFaqReady(page: Page): Promise<void> {
  // (1) Wait for FAQ hero (sentinel testid emesso da page v2).
  await page.waitForSelector('[data-testid="faq-hero"]', { timeout: 30_000 });

  // (2) Fonts ready.
  await page.evaluate(async () => {
    if (typeof document !== 'undefined' && 'fonts' in document) {
      await (document as Document & { fonts: { ready: Promise<void> } }).fonts.ready;
    }
  });

  // (3) Two RAFs to flush layout.
  await page.evaluate(
    () =>
      new Promise<void>(resolve => {
        requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
      })
  );
}

test.describe('V2 Visual Migrated — /faq matches mockup baseline', () => {
  test.describe.configure({ retries: 0 });

  test('FAQ default state matches sp3-faq-enhanced mockup', async ({ page }) => {
    await page.goto('/faq', { waitUntil: 'networkidle' });
    await waitForFaqReady(page);

    await expect(page).toHaveScreenshot(`${FAQ_SLUG}.png`, {
      fullPage: true,
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('FAQ search-results state (#q=password)', async ({ page }) => {
    await page.goto('/faq#q=password', { waitUntil: 'networkidle' });
    await waitForFaqReady(page);
    // Wait per debounce useFaqHashQuery (250ms) + RAF.
    await page.waitForTimeout(400);

    await expect(page).toHaveScreenshot(`${FAQ_SLUG}-search-password.png`, {
      fullPage: true,
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
