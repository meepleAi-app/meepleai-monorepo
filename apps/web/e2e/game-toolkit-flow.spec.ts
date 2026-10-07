/**
 * Toolkit per-gioco — `/library/[gameId]/toolkit` e la sessione che ne nasce.
 *
 * #4106 — riscritta. La versione precedente non era mai stata eseguita (viveva sotto
 * `__tests__/e2e/`, che nessuna config Playwright raccoglieva — #4092). Dieci fallimenti su
 * dieci cadevano sulla pagina di login, ma — a differenza di `library/game-detail.spec.ts` —
 * **i suoi selettori erano in gran parte giusti**: `Player 1`, `Add Player`, `Start Session`,
 * `Scoring Categories`, `Scoring Rules`, `requires at least` esistono tutti nella pagina
 * reale. È il caso in cui la diagnosi «deriva di selettori» era la più sbagliata possibile.
 *
 * I difetti veri erano tre:
 *
 *   1. **Non autenticava.** `/library/**` è protetta.
 *   2. **Id inventati**: `7-wonders-id`, `splendor-id`, e un `testGameId = 'test-game-id'` col
 *      commento «Replace with actual game ID in test env». Qui i giochi si seminano.
 *   3. **La URL sbagliata, in un modo che il 308 nasconde.** `/library/games/:id/toolkit`
 *      **reindirizza** a `/library/:id?tab=toolkit`, cioè alla **tab** dentro la scheda — non
 *      alla pagina autonoma `/library/:id/toolkit`, che è dove vivono i campi dei giocatori.
 *      Seguire il redirect portava su una superficie diversa da quella sotto test.
 *
 * ## Il template si risolve per NOME, non per id
 *
 * `getGameTemplateByName(details.name)` normalizza il nome e cerca fra sei template cablati
 * (`7 Wonders`, `Splendor`, `Catan`, `Ticket to Ride`, `Wingspan`, `Azul`), con un fallback
 * «contains». Per questo il gioco di prova si chiama `7 Wonders E2E …`: il nome contiene
 * `7-wonders` normalizzato, quindi il template si attiva e le sue categorie — `Military`,
 * `Science`, `Commerce` — sono asseribili. Un titolo qualunque non avrebbe template, e le
 * schede `Scoring Categories` / `Rounds` non comparirebbero affatto.
 *
 * ⚠️ La validazione del numero di giocatori usa `game.playerCount` **del gioco** (dall'API),
 * non quello del template: il seed passa `minPlayers: 3` perché il messaggio atteso è
 * `requires at least 3 players`.
 *
 * ## Cosa è stato eliminato
 *
 *   - `should show Toolkit button on game cards` e `should navigate to game toolkit landing
 *     from card`: cercavano `[data-testid="game-card"]`, che ha **0 occorrenze** in `src`, e
 *     un link `Toolkit` sulle schede di `/library` — che rende `LibraryHub`, non la
 *     `game-table` dove quel link esiste. L'ingresso al toolkit oggi è dalla scheda del gioco.
 *
 * @see docs/for-developers/audits/2026-10-06-orphan-e2e-specs-classification.md
 */

import { test, expect, type Page } from '@playwright/test';

import {
  hasRealAdminCredentials,
  loginAsRealAdmin,
  MISSING_CREDENTIALS_REASON,
  REAL_ADMIN_EMAIL,
} from './_helpers/realAdminAuth';
import { cleanupTestEntities, newTestRunId, seedLibraryGame } from './_helpers/seedEntities';

/**
 * Il pulsante di avvio NON si chiama «Start Session»: il suo testo e'
 * `Start ${game.name} Session` (`page.tsx:280`), quindi include il titolo del gioco — che qui
 * e' generato per run. La versione precedente cercava `/start.*session/i`, che funzionava per
 * caso; questa forma dice perche'.
 */
const START_SESSION = /^Start .+ Session$/;

interface SeededGame {
  gameId: string;
  title: string;
  testRunId: string;
}

/** Semina un gioco il cui nome fa scattare il template «7 Wonders». */
async function seedSevenWonders(page: Page, testId: string): Promise<SeededGame> {
  const testRunId = newTestRunId(testId);
  const title = `7 Wonders E2E ${testRunId.slice(-8)}`;
  const seeded = await seedLibraryGame(page, {
    testRunId,
    ownerEmail: REAL_ADMIN_EMAIL as string,
    title,
    minPlayers: 3,
    maxPlayers: 7,
  });
  return { gameId: seeded.gameId, title, testRunId };
}

test.describe('Game-Specific Toolkit', () => {
  const seededRuns: string[] = [];
  let game: SeededGame;

  test.beforeEach(async ({ page }, testInfo) => {
    test.skip(!hasRealAdminCredentials, MISSING_CREDENTIALS_REASON);
    await loginAsRealAdmin(page);

    game = await seedSevenWonders(page, testInfo.testId);
    seededRuns.push(game.testRunId);

    // La pagina AUTONOMA, non `?tab=toolkit`: vedi il punto 3 dell'intestazione.
    await page.goto(`/library/${game.gameId}/toolkit`);
    await expect(page.getByRole('heading', { name: `${game.title} Toolkit` })).toBeVisible({
      timeout: 30_000,
    });
  });

  test.afterEach(async ({ page }) => {
    for (const testRunId of seededRuns.splice(0)) {
      await cleanupTestEntities(page, { testRunId });
    }
  });

  test('should display game template preview', async ({ page }) => {
    await expect(page.getByText('Scoring Categories')).toBeVisible();
    await expect(page.getByText('Rounds', { exact: true })).toBeVisible();
  });

  test('should pre-fill categories from template', async ({ page }) => {
    // Le categorie di `7 Wonders` nel template cablato. Se il lookup per nome smettesse di
    // agganciare, queste tre spariscono insieme — ed è il difetto che conta.
    for (const category of ['Military', 'Science', 'Commerce']) {
      await expect(page.getByText(category, { exact: true })).toBeVisible();
    }
  });

  test('should display scoring rules in sidebar', async ({ page }) => {
    await expect(page.getByText('Scoring Rules')).toBeVisible();
    await expect(page.getByText(/Final score = sum of all categories/)).toBeVisible();
  });

  test('should validate player count against game rules', async ({ page }) => {
    // Un solo giocatore contro `minPlayers: 3` del gioco seminato.
    await page.getByPlaceholder('Player 1').fill('Solo');
    await page.getByRole('button', { name: START_SESSION }).click();

    await expect(page.getByText(/requires at least 3 players/)).toBeVisible({ timeout: 15_000 });
    // La prova che la validazione ha BLOCCATO: senza questa, il test passerebbe anche se la
    // sessione partisse mostrando il toast.
    await expect(page).toHaveURL(new RegExp(`/library/${game.gameId}/toolkit$`));
  });

  /**
   * SALTATO per un difetto del PRODOTTO, non del test — e il test lo ha trovato.
   *
   * `handleStartSession` aggiunge i giocatori in ciclo **senza colore**, e
   * `LiveGameSession.AddPlayer` impone l'unicità del colore: dal secondo giocatore in poi
   * l'API risponde `400 domain_error` con «Color Red is already taken by another player».
   * Riprodotto anche via API, senza browser. Con `minPlayers: 3` il flusso non ha un solo
   * caso che riesca, quindi la navigazione a `/toolkit/<sessionId>` non avviene mai.
   *
   * ⚠️ Quando #4107 chiude, togliere la riga `test.skip` **non basta**: appena questi test
   * creeranno davvero una sessione, l'`afterEach` incontrerà #4109 — `seed/cleanup` risponde
   * 500 perché la riga in `session_tracking_sessions` nata dal percorso toolkit resta fuori
   * dallo scope del `TestRunId` e il suo FK blocca la cancellazione dello `shared_games`.
   * Le due issue vanno chiuse insieme perché questo test torni verde.
   */
  test('should start game session with template', async ({ page }) => {
    test.skip(
      true,
      'DIFETTO: #4107 — `handleStartSession` non assegna un colore: dal secondo giocatore l API risponde 400 «Color Red is already taken», e la sessione non parte mai'
    );

    await page.getByPlaceholder('Player 1').fill('Alice');
    await page.getByRole('button', { name: /Add Player/i }).click();
    await page.getByPlaceholder('Player 2').fill('Bob');
    await page.getByRole('button', { name: /Add Player/i }).click();
    await page.getByPlaceholder('Player 3').fill('Carol');

    await page.getByRole('button', { name: START_SESSION }).click();

    await expect(page).toHaveURL(new RegExp(`/library/${game.gameId}/toolkit/[0-9a-f-]{36}$`), {
      timeout: 30_000,
    });
    await expect(page.getByText('Participants')).toBeVisible();
  });

  /**
   * SALTATO per un difetto del PRODOTTO, non del test — e il test lo ha trovato.
   *
   * `handleStartSession` aggiunge i giocatori in ciclo **senza colore**, e
   * `LiveGameSession.AddPlayer` impone l'unicità del colore: dal secondo giocatore in poi
   * l'API risponde `400 domain_error` con «Color Red is already taken by another player».
   * Riprodotto anche via API, senza browser. Con `minPlayers: 3` il flusso non ha un solo
   * caso che riesca, quindi la navigazione a `/toolkit/<sessionId>` non avviene mai.
   *
   * ⚠️ Quando #4107 chiude, togliere la riga `test.skip` **non basta**: appena questi test
   * creeranno davvero una sessione, l'`afterEach` incontrerà #4109 — `seed/cleanup` risponde
   * 500 perché la riga in `session_tracking_sessions` nata dal percorso toolkit resta fuori
   * dallo scope del `TestRunId` e il suo FK blocca la cancellazione dello `shared_games`.
   * Le due issue vanno chiuse insieme perché questo test torni verde.
   */
  test('should finalize and return to game detail page', async ({ page }) => {
    test.skip(
      true,
      'DIFETTO: #4107 — `handleStartSession` non assegna un colore: dal secondo giocatore l API risponde 400 «Color Red is already taken», e la sessione non parte mai'
    );

    await page.getByPlaceholder('Player 1').fill('Winner');
    await page.getByRole('button', { name: /Add Player/i }).click();
    await page.getByPlaceholder('Player 2').fill('Second');
    await page.getByRole('button', { name: /Add Player/i }).click();
    await page.getByPlaceholder('Player 3').fill('Third');
    await page.getByRole('button', { name: START_SESSION }).click();
    await expect(page).toHaveURL(new RegExp(`/library/${game.gameId}/toolkit/[0-9a-f-]{36}$`), {
      timeout: 30_000,
    });

    await page
      .getByRole('button', { name: /Finalize/i })
      .first()
      .click();

    // La versione precedente attendeva `/library/games/<id>` — la forma che risponde **404**
    // (#4105). La destinazione reale è `router.push('/library/<gameId>')`.
    await expect(page).toHaveURL(new RegExp(`/library/${game.gameId}$`), { timeout: 30_000 });
  });

  test('should be mobile responsive', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto(`/library/${game.gameId}/toolkit`);

    await expect(page.getByRole('heading', { name: `${game.title} Toolkit` })).toBeVisible();
    await expect(page.getByPlaceholder('Player 1')).toBeVisible();
  });

  test('should work in dark mode', async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'dark' });
    await page.goto(`/library/${game.gameId}/toolkit`);

    await expect(page.getByRole('heading', { name: `${game.title} Toolkit` })).toBeVisible();
  });
});
