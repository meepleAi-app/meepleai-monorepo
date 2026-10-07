/**
 * Storico sessioni del Toolkit — `/toolkit/history` (Issue #3165 · GST-006).
 *
 * #4106 — riscritta. La versione precedente non era mai stata eseguita (viveva sotto
 * `__tests__/e2e/`, che nessuna config Playwright raccoglieva — #4092) e aveva **due** difetti
 * sovrapposti:
 *
 *   1. **Non autenticava.** `/toolkit/history` è protetta: 7 fallimenti su 7 cadevano sulla
 *      pagina di login. È la causa che va esclusa per prima, perché un `toBeVisible` che non
 *      trova nulla su `/login` è indistinguibile da una deriva di selettori (#4098).
 *   2. **Selettori derivati**, una volta autenticata. Qui — a differenza di
 *      `library/game-detail.spec.ts` — la pagina **esiste e funziona**: la deriva è autentica
 *      e riscrivibile, non una superficie sostituita.
 *
 * ## Deriva misurata
 *
 * | la spec cercava | la pagina ha |
 * |---|---|
 * | `heading /filters/i` | nessuna intestazione «filters»: i filtri sono una toolbar |
 * | `getByLabel(/game/i)` | un **pulsante popover** `Games`, non un campo etichettato |
 * | `getByLabel(/start date/i)` | `Start date` esiste, ma **solo dentro** il popover `Date` dopo aver scelto l'intervallo custom |
 * | `button /reset/i` | `Clear all`, e compare solo quando un filtro è attivo |
 * | `no sessions found` | `No sessions yet` |
 * | `start your first session` | `Create first session` |
 *
 * ## 🔴 Perché le asserzioni sono in inglese, su una app con `DEFAULT_LOCALE = it`
 *
 * `IntlProvider` rende con `DEFAULT_LOCALE` ('it') durante l'SSR e **dopo il mount** adotta
 * `window.navigator.language`. La config Playwright non imposta `locale`, quindi il browser
 * manda `en-US` e il client passa all'inglese: `heading "Session history"`,
 * `searchbox "Search by game or winner"` — verificato nell'istantanea di pagina.
 *
 * Conseguenza per chiunque scriva spec su questa app: **asserire l'italiano significa asserire
 * lo stato pre-idratazione**, che è una corsa. Se un giorno la config imposterà `locale: 'it-IT'`,
 * queste stringhe vanno cambiate tutte insieme — non una alla volta quando una fallisce.
 *
 * @see docs/for-developers/audits/2026-10-06-orphan-e2e-specs-classification.md
 */

import { test, expect } from '@playwright/test';

import {
  hasRealAdminCredentials,
  loginAsRealAdmin,
  MISSING_CREDENTIALS_REASON,
} from './_helpers/realAdminAuth';

/** Una ricerca che non può corrispondere a nulla: esercita lo stato «nessun risultato». */
const NONSENSE_QUERY = 'zzz-nessuna-sessione-corrisponde-4106';

/**
 * Il trigger di un filtro, disambiguato sull'attributo.
 *
 * `getByRole('button', { name: 'Date' })` viola la strict mode: ci sono **due** pulsanti con
 * quel nome — il filtro e l'intestazione di colonna ordinabile della tabella. Si distinguono
 * solo per `aria-haspopup="dialog"`, non per il testo.
 */
const filterTrigger = (page: import('@playwright/test').Page, name: string) =>
  page.locator(`button[aria-haspopup="dialog"][aria-label="${name}"]`);

test.describe('Toolkit - Session History', () => {
  test.beforeEach(async ({ page }) => {
    test.skip(!hasRealAdminCredentials, MISSING_CREDENTIALS_REASON);
    await loginAsRealAdmin(page);

    await page.goto('/toolkit/history');
    await expect(page.getByRole('heading', { name: 'Session history' })).toBeVisible({
      timeout: 30_000,
    });
  });

  test('should display history page', async ({ page }) => {
    await expect(page.getByRole('searchbox', { name: 'Search by game or winner' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Export CSV' })).toBeVisible();
  });

  test('should show filters', async ({ page }) => {
    // I tre filtri sono pulsanti che aprono un popover, non campi etichettati: è la differenza
    // che ha reso illeggibile la versione precedente (`getByLabel(/game/i)`).
    await expect(filterTrigger(page, 'Games')).toBeVisible();
    await expect(filterTrigger(page, 'Date')).toBeVisible();
    await expect(filterTrigger(page, 'Winners')).toBeVisible();
    await expect(page.getByRole('combobox', { name: 'Sort by' })).toBeVisible();
  });

  test('il popover Date espone l intervallo personalizzato', async ({ page }) => {
    await filterTrigger(page, 'Date').click();

    // `Start date` / `End date` esistono — ma solo qui dentro, dopo aver scelto le date custom.
    // La versione precedente li cercava sulla pagina e andava in timeout su `fill`.
    // Le voci del popover sono un `radiogroup`, non delle `option`: un Select Radix e un
    // gruppo di radio si somigliano a vedersi e hanno ruoli diversi.
    await page.getByRole('radio', { name: /Custom dates/ }).click();

    await expect(page.getByLabel('Start date')).toBeVisible();
    await expect(page.getByLabel('End date')).toBeVisible();
  });

  test('una ricerca senza risultati mostra lo stato filtrato vuoto', async ({ page }) => {
    await page.getByRole('searchbox', { name: 'Search by game or winner' }).fill(NONSENSE_QUERY);

    // `No sessions yet` (stato vuoto assoluto) NON è asseribile su un account che ha sessioni:
    // lo stato raggiungibile per un test è quello **filtrato**, e ha un testo diverso.
    await expect(page.getByText('No sessions match the filters')).toBeVisible({ timeout: 30_000 });
    await expect(page.getByRole('button', { name: 'Clear filters' })).toBeVisible();
  });

  test('«Clear filters» riporta la lista completa', async ({ page }) => {
    const search = page.getByRole('searchbox', { name: 'Search by game or winner' });
    await search.fill(NONSENSE_QUERY);
    await expect(page.getByText('No sessions match the filters')).toBeVisible({ timeout: 30_000 });

    await page.getByRole('button', { name: 'Clear filters' }).click();

    // Il campo svuotato è la prova che il filtro è caduto; senza questa asserzione il test
    // passerebbe anche se il pulsante nascondesse solo il messaggio.
    await expect(search).toHaveValue('');
    await expect(page.getByText('No sessions match the filters')).toHaveCount(0);
  });

  test('should be mobile responsive', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/toolkit/history');

    await expect(page.getByRole('heading', { name: 'Session history' })).toBeVisible();
  });

  test('should work in dark mode', async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'dark' });
    await page.goto('/toolkit/history');

    await expect(page.getByRole('heading', { name: 'Session history' })).toBeVisible();
  });
});
