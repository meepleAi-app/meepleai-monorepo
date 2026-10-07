/**
 * Custom Strategy Builder (Issue #3412) — il pannello che si apre da
 * `/admin/agents/definitions`.
 *
 * #4106 — riscritta. La versione precedente non era mai stata eseguita (viveva sotto
 * `__tests__/e2e/`, che nessuna config Playwright raccoglieva — #4092) e aveva tre difetti:
 *
 *   1. **L'autenticazione era commentata.** Letteralmente: quattro righe `// await page.goto
 *      ('/login') … // await page.click('[type="submit"]')` sotto il commento «assuming admin
 *      auth is set up». Anonima su `/admin/**`, quindi ogni asserzione cadeva su `/login`.
 *   2. **La rotta non esiste.** `/admin/rag/strategy-builder` risponde **404** (sonda con
 *      sessione admin). Il builder non è una pagina: è una `Sheet` che si apre dal pulsante
 *      `Strategy Builder` su `/admin/agents/definitions` (`definitions/page.tsx:80,107`).
 *   3. **Due test non potevano fallire.** `can toggle panels` e `can select template` avevano
 *      il corpo dentro `if ((await x.count()) > 0) { … }`: nessuna `expect`, e un ramo vuoto
 *      quando l'elemento non c'è. Eliminati — un test che passa sia con la funzione sia senza
 *      non misura la funzione.
 *
 * Verità a terra (`src/components/rag-dashboard/builder/`):
 *   apertura   → pulsante `Strategy Builder` su `/admin/agents/definitions`, poi una `Sheet`
 *                con `SheetTitle` «Strategy Builder» (sr-only) che monta `BuilderClient`
 *   canvas     → `data-testid="pipeline-canvas"` (`PipelineCanvas.tsx`)
 *   palette    → `data-testid="block-palette"` (`BlockPalette.tsx`)
 *   azioni     → `data-testid` `save-button`, `test-button`, `reset-button`
 *                (`StrategyBuilder.tsx:338,356,380`)
 *   validazione→ `<h3>Validation</h3>` (`StrategyBuilder.tsx:409`), reso quando
 *                `showValidation` (default `true`)
 *
 * @see docs/for-developers/audits/2026-10-06-orphan-e2e-specs-classification.md
 */

import { test, expect, type Page } from '@playwright/test';

import {
  hasRealAdminCredentials,
  loginAsRealAdmin,
  MISSING_CREDENTIALS_REASON,
} from '../_helpers/realAdminAuth';

/** Apre la Sheet del builder dalla lista delle definizioni. */
async function openStrategyBuilder(page: Page) {
  await page.goto('/admin/agents/definitions');
  await expect(page.getByRole('heading', { name: 'Agent Definitions' })).toBeVisible({
    timeout: 30_000,
  });

  await page.getByRole('button', { name: 'Strategy Builder' }).click();

  // Il canvas è la prova che `BuilderClient` è montato: il titolo della Sheet è `sr-only` e
  // non direbbe nulla sul fatto che il contenuto sia arrivato.
  await expect(page.getByTestId('pipeline-canvas')).toBeVisible({ timeout: 30_000 });
}

test.describe('Custom Strategy Builder', () => {
  test.beforeEach(async ({ page }) => {
    test.skip(!hasRealAdminCredentials, MISSING_CREDENTIALS_REASON);
    await loginAsRealAdmin(page);
    await openStrategyBuilder(page);
  });

  test('loads strategy builder page', async ({ page }) => {
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByTestId('pipeline-canvas')).toBeVisible();
  });

  test('displays canvas and palette', async ({ page }) => {
    await expect(page.getByTestId('pipeline-canvas')).toBeVisible();
    await expect(page.getByTestId('block-palette')).toBeVisible();
  });

  test('displays save and test buttons', async ({ page }) => {
    await expect(page.getByTestId('save-button')).toBeVisible();
    await expect(page.getByTestId('test-button')).toBeVisible();
    await expect(page.getByTestId('reset-button')).toBeVisible();
  });

  test('shows validation panel', async ({ page }) => {
    // `getByText('Validation')` viola la strict mode: ci sono uno `<span>` (con un proprio
    // testid) e l'`<h3>` del pannello. Il ruolo distingue senza inventare un selettore.
    await expect(page.getByRole('heading', { name: 'Validation' })).toBeVisible();
  });
});

test.describe('Strategy Builder - Responsive', () => {
  test.beforeEach(async ({ page }) => {
    test.skip(!hasRealAdminCredentials, MISSING_CREDENTIALS_REASON);
    await loginAsRealAdmin(page);
  });

  // La Sheet è larga 800px fissi (`w-[800px] sm:max-w-[800px]`): sotto quella larghezza
  // occupa lo schermo. Le due misure verificano che il canvas resti raggiungibile, non che
  // il layout cambi.
  for (const [label, width, height] of [
    ['desktop', 1920, 1080],
    ['tablet', 768, 1024],
  ] as const) {
    test(`renders on ${label}`, async ({ page }) => {
      await page.setViewportSize({ width, height });
      await openStrategyBuilder(page);

      await expect(page.getByTestId('block-palette')).toBeVisible();
    });
  }
});
