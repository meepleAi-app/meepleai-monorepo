/**
 * Scheda di un gioco della libreria personale — `/library/[gameId]`.
 *
 * #4106 — riscritta da zero. La versione precedente non era mai stata eseguita (viveva sotto
 * `__tests__/e2e/`, che nessuna config Playwright raccoglieva — #4092) e aveva **tre** difetti
 * indipendenti, in ordine di quanto impedivano l'esecuzione:
 *
 *   1. **Non autenticava.** `/library/**` è protetta: 13 fallimenti su 13 cadevano sulla pagina
 *      di login. Il sintomo (`toBeVisible` che non trova nulla) è indistinguibile da una deriva
 *      di selettori, ed è il motivo per cui si stabilisce la causa guardando l'`# Page snapshot`
 *      di `error-context.md`, non la riga `Error:` — lezione di #4098.
 *   2. **La rotta non esiste.** Puntava a `/library/games/<id>`, che risponde **404**: la rotta
 *      reale è `/library/[gameId]`. `next.config.js` ha dieci redirect per i *figli* di
 *      `/library/games/:id` e nessuno per la forma nuda — difetto di prodotto, #4105.
 *   3. **L'id era inventato.** `game-123`, col commento «assuming game-123 exists in test DB».
 *      Non esiste. Qui il gioco si semina col pattern di casa (`seedEntities`), e `afterEach` lo
 *      rimuove.
 *
 * ## Cosa è stato eliminato, e perché
 *
 * La pagina che quella spec descriveva **non esiste più**. L'intestazione di
 * `src/app/(authenticated)/library/[gameId]/page.tsx` lo dichiara: *«S4 (library-to-game epic):
 * migrated desktop path from GameTableLayout to GameDetailDesktop with 5 tabs»*. Misurato in
 * `src`, escludendo `__tests__`:
 *
 *   - `[aria-label*="Carta del gioco"]` (flip della carta: 2 test + 1 responsive) → **0 occorrenze**
 *   - tab `Knowledge Base` / `Social Links` (1 test + 1 responsive) → sostituiti dai cinque tab
 *     di S4: `Info · Agente · Toolkit · House Rules · Partite`
 *   - toast `aggiunto ai preferiti` → **0 occorrenze**
 *   - `Stato aggiornato`, `Modifica note` → esistono, ma in `components/library/game-table/`,
 *     cioè il `GameTableLayout` che S4 ha smesso di rendere su questa pagina
 *   - modale upload PDF, dialogo di rimozione → non in `GameDetailDesktop`, che rende
 *     `GameHero` + `GameTabsPanel` + `SessionContributorsStrip`
 *
 * Eliminato anche `displays play statistics when available`: il corpo era
 * `if (await statsSection.isVisible()) { … }`, quindi non poteva fallire per un difetto del
 * prodotto.
 *
 * ## Verità a terra usata qui
 *
 *   desktop    → `data-testid="game-detail-desktop"`, titolo in un `<h1>` di `GameHero`
 *   mobile     → `data-testid="game-detail-mobile"`; i due layout coesistono nel DOM e si
 *                alternano via CSS (`lg:hidden` / `hidden lg:block`), non per ramo di render
 *   non trovato→ `data-testid="not-found-state"`, `Gioco non trovato`, CTA `Torna alla Libreria`
 *   errore     → `data-testid="error-state"`, `Errore di caricamento`, CTA `Riprova`
 *   tab        → cinque id in `components/game-detail/tabs/types.ts`, deep-link via `?tab=`
 *
 * @see docs/for-developers/audits/2026-10-06-orphan-e2e-specs-classification.md
 * @see docs/for-developers/testing/e2e-entity-seeding.md
 */

import { test, expect, type Page } from '@playwright/test';

import {
  hasRealAdminCredentials,
  loginAsRealAdmin,
  MISSING_CREDENTIALS_REASON,
  REAL_ADMIN_EMAIL,
} from '../_helpers/realAdminAuth';
import { cleanupTestEntities, newTestRunId, seedLibraryGame } from '../_helpers/seedEntities';

/** Un id valido per forma ma assente dalla libreria: esercita il ramo «non trovato». */
const ABSENT_GAME_ID = '00000000-0000-4000-8000-0000000040ff';

interface SeededGame {
  gameId: string;
  title: string;
  testRunId: string;
}

/**
 * Semina un gioco nella libreria dell'utente con cui si fa login.
 *
 * `ownerEmail` qui è **lookup-or-create**: passando l'admin reale, l'handler riusa quella riga
 * invece di crearne una nuova, e — verificato nel sorgente — stampa il `TestRunId` **solo** se
 * l'utente va creato. Per questo `cleanupTestEntities`, che cancella gli utenti filtrando su
 * `TestRunId`, non può toccare l'account con cui stiamo guidando il browser.
 */
async function seedGameInOwnLibrary(page: Page, testId: string): Promise<SeededGame> {
  const testRunId = newTestRunId(testId);
  const title = `E2E Library Detail ${testRunId.slice(-8)}`;
  const seeded = await seedLibraryGame(page, {
    testRunId,
    ownerEmail: REAL_ADMIN_EMAIL as string,
    title,
    minPlayers: 2,
    maxPlayers: 4,
  });
  return { gameId: seeded.gameId, title, testRunId };
}

test.describe('Scheda gioco della libreria', () => {
  const seededRuns: string[] = [];

  test.beforeEach(async ({ page }) => {
    test.skip(!hasRealAdminCredentials, MISSING_CREDENTIALS_REASON);
    await loginAsRealAdmin(page);
  });

  test.afterEach(async ({ page }) => {
    // Senza questo, ogni esecuzione lascia un SharedGame e una voce di libreria: la pulizia è
    // parte del contratto di `seedEntities`, non un'accortezza.
    for (const testRunId of seededRuns.splice(0)) {
      await cleanupTestEntities(page, { testRunId });
    }
  });

  test('carica la scheda e mostra il titolo del gioco', async ({ page }, testInfo) => {
    const game = await seedGameInOwnLibrary(page, testInfo.testId);
    seededRuns.push(game.testRunId);

    await page.goto(`/library/${game.gameId}`);

    await expect(page.getByTestId('game-detail-desktop')).toBeVisible({ timeout: 30_000 });
    await expect(page.getByRole('heading', { level: 1 })).toContainText(game.title);
  });

  test('i cinque tab di S4 sono presenti e il deep link ?tab= ne apre uno', async ({
    page,
  }, testInfo) => {
    const game = await seedGameInOwnLibrary(page, testInfo.testId);
    seededRuns.push(game.testRunId);

    await page.goto(`/library/${game.gameId}`);
    await expect(page.getByTestId('game-detail-desktop')).toBeVisible({ timeout: 30_000 });

    // Le etichette, non gli id: sono ciò che l'utente vede, e sono il contratto che la vecchia
    // spec aveva perso (cercava `Knowledge Base` e `Social Links`).
    for (const label of ['Info', 'Agente', 'Toolkit', 'House Rules', 'Partite']) {
      await expect(page.getByRole('tab', { name: new RegExp(label) })).toBeVisible();
    }

    // Deep link: `?tab=toolbox` deve aprire Toolkit, non Info.
    await page.goto(`/library/${game.gameId}?tab=toolbox`);
    await expect(page.getByRole('tab', { name: /Toolkit/ })).toHaveAttribute(
      'aria-selected',
      'true'
    );
  });

  test('un gioco assente dalla libreria mostra lo stato «non trovato»', async ({ page }) => {
    await page.goto(`/library/${ABSENT_GAME_ID}`);

    await expect(page.getByTestId('not-found-state')).toBeVisible({ timeout: 30_000 });
    await expect(page.getByText('Gioco non trovato')).toBeVisible();
  });

  test('dallo stato «non trovato» si torna alla libreria', async ({ page }) => {
    await page.goto(`/library/${ABSENT_GAME_ID}`);
    await expect(page.getByTestId('not-found-state')).toBeVisible({ timeout: 30_000 });

    await page.getByRole('button', { name: /Torna alla Libreria/ }).click();

    await expect(page).toHaveURL(/\/library$/, { timeout: 30_000 });
  });

  test('a larghezza mobile rende il layout mobile, non quello desktop', async ({
    page,
  }, testInfo) => {
    const game = await seedGameInOwnLibrary(page, testInfo.testId);
    seededRuns.push(game.testRunId);

    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto(`/library/${game.gameId}`);

    // I due layout coesistono nel DOM: l'asserzione utile è quale dei due è VISIBILE. Asserire
    // solo la presenza del mobile passerebbe anche a 1920px.
    await expect(page.getByTestId('game-detail-mobile')).toBeVisible({ timeout: 30_000 });
    await expect(page.getByTestId('game-detail-desktop')).toBeHidden();
  });
});
