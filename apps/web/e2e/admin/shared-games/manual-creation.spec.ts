/**
 * SharedGame Manual Creation E2E Tests (Issue #4231)
 *
 * #4098 — riscritta contro la UI che esiste davvero. La versione precedente non era mai stata
 * eseguita (viveva sotto `tests/e2e/`, che nessuna config raccoglieva — vedi #4092) e presupponeva
 * un form con etichette in italiano (`titolo`, `anno di pubblicazione`, `url thumbnail`) e una
 * intestazione «nuovo gioco». La pagina reale è in inglese, non ha un campo thumbnail, e il
 * pulsante di conferma si chiama «Create Game».
 *
 * Verità a terra usata per i selettori, letta da
 * `src/app/admin/(dashboard)/shared-games/new/client.tsx`:
 *   campi      → id/label `Title *`, `Description *`, `Year Published *`, `Playing Time (min) *`,
 *                `Min Players *`, `Max Players *`, `Min Age`, `Image URL (optional)`
 *   thumbnail  → NON è un campo del form: al submit vale `thumbnailUrl || imageUrl ||
 *                'https://placehold.co/150x150?text=No+Image'` (client.tsx:153-154)
 *   submit     → `Create Game`, poi `router.push('/admin/shared-games/<id>')`
 *   cancel     → `Cancel`, poi `router.push('/admin/shared-games/all')`
 *   validation → messaggi inglesi dallo schema zod: `Title is required`, `Description is required`
 *
 * @see apps/web/src/app/admin/(dashboard)/shared-games/new/client.tsx
 * @see docs/for-developers/audits/2026-10-06-orphan-e2e-specs-classification.md
 */

import { test, expect, type Page } from '@playwright/test';

import { isBackendReachable } from '../../_helpers/backendGuard';

// ========== Credenziali ==========

/**
 * #4098: la versione precedente aveva `admin@test.com` / `Admin123!` cablati, utenti che non
 * esistono in nessun ambiente. Qui le credenziali arrivano dall'ambiente e, se mancano, il test
 * SALTA dichiarando come abilitarlo — invece di fallire su un login impossibile.
 *
 * In locale: `E2E_ADMIN_EMAIL=e2e-admin@meepleai.test` e `E2E_ADMIN_PASSWORD` dal valore di
 * `SEED_TEST_PASSWORD` in `infra/secrets/admin.secret`.
 */
const ADMIN_EMAIL = process.env.E2E_ADMIN_EMAIL;
const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD;

interface TestGameData {
  title: string;
  yearPublished: number;
  description: string;
  minPlayers: number;
  maxPlayers: number;
  playingTimeMinutes: number;
  minAge: number;
  imageUrl: string;
}

function createTestGameData(overrides: Partial<TestGameData> = {}): TestGameData {
  // Il titolo deve essere unico fra le esecuzioni: la pagina non ha rilevamento duplicati, ma un
  // titolo ripetuto renderebbe ambigue le asserzioni sulla lista.
  const unique = `${process.env.TEST_WORKER_INDEX ?? '0'}-${Math.floor(performance.now())}`;
  return {
    title: `E2E Manual Game ${unique}`,
    yearPublished: 2024,
    description: 'A test board game created by the #4231 manual-creation E2E flow.',
    minPlayers: 2,
    maxPlayers: 4,
    playingTimeMinutes: 60,
    minAge: 12,
    imageUrl: 'https://cdn.example.com/e2e-cover.webp',
    ...overrides,
  };
}

async function loginAsAdmin(page: Page) {
  await page.goto('/login');
  await page
    .locator('input[type="email"]')
    .first()
    .fill(ADMIN_EMAIL as string);
  await page
    .locator('input[type="password"]')
    .first()
    .fill(ADMIN_PASSWORD as string);
  await page.locator('button[type="submit"]').first().click();
  // Un errore in-page lascia la URL invariata: attendere «qualcosa» lo scambierebbe per successo.
  await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });
}

async function fillGameForm(page: Page, data: TestGameData) {
  await page.getByLabel(/^Title/).fill(data.title);
  await page.getByLabel(/^Description/).fill(data.description);
  await page.getByLabel(/^Year Published/).fill(String(data.yearPublished));
  await page.getByLabel(/^Playing Time/).fill(String(data.playingTimeMinutes));
  await page.getByLabel(/^Min Players/).fill(String(data.minPlayers));
  await page.getByLabel(/^Max Players/).fill(String(data.maxPlayers));
  await page.getByLabel(/^Min Age/).fill(String(data.minAge));
  await page.getByLabel(/^Image URL/).fill(data.imageUrl);
}

const DETAIL_URL = /\/admin\/shared-games\/[0-9a-f-]{36}/;

// ========== Test Suite ==========

test.describe('SharedGame Manual Creation Flow', () => {
  test.beforeEach(async ({ page }) => {
    test.skip(
      !ADMIN_EMAIL || !ADMIN_PASSWORD,
      'PREVISTO: imposta E2E_ADMIN_EMAIL e E2E_ADMIN_PASSWORD (in locale `e2e-admin@meepleai.test` + SEED_TEST_PASSWORD da infra/secrets/admin.secret)'
    );
    test.skip(
      !(await isBackendReachable(page)),
      'PREVISTO: questo flusso crea un gioco reale e richiede l API su :8080 (`cd infra && make dev-core`)'
    );

    await loginAsAdmin(page);
    await page.goto('/admin/shared-games/new');
    await expect(page.getByRole('heading', { name: 'Add New Game' })).toBeVisible();
  });

  test('Admin creates SharedGame manually - complete flow', async ({ page }) => {
    const testGame = createTestGameData();

    await fillGameForm(page, testGame);
    await page.getByRole('button', { name: 'Create Game' }).click();

    // Il redirect alla scheda è l'unica prova che la creazione è andata a buon fine: la pagina
    // resta su /new quando il POST falisce.
    await expect(page).toHaveURL(DETAIL_URL, { timeout: 30_000 });
    await expect(page.getByRole('heading', { name: testGame.title })).toBeVisible();
    await expect(page.getByText(testGame.description)).toBeVisible();
  });

  test('Form validates required fields correctly', async ({ page }) => {
    // Submit a vuoto: i messaggi sono quelli dello schema zod in client.tsx, in inglese.
    await page.getByRole('button', { name: 'Create Game' }).click();

    await expect(page.getByText('Title is required')).toBeVisible();
    await expect(page.getByText('Description is required')).toBeVisible();
    await expect(page).toHaveURL(/\/admin\/shared-games\/new/);

    // Compilato il minimo, lo stesso submit deve riuscire.
    await fillGameForm(page, createTestGameData());
    await page.getByRole('button', { name: 'Create Game' }).click();

    await expect(page).toHaveURL(DETAIL_URL, { timeout: 30_000 });
  });

  /**
   * #4098 — SALTATO: la funzionalità non esiste.
   *
   * Lo scenario 3 originale attendeva un avviso di duplicato con pulsanti «procedi»/«annulla».
   * `grep -cE "duplicate|duplicato|già esiste|check-duplicate"` su
   * `shared-games/new/client.tsx` dà **0**: la creazione manuale non confronta il titolo con il
   * catalogo. Il rilevamento duplicati esiste solo sul percorso di import BGG
   * (`/admin/shared-games/bgg/check-duplicate/{bggId}`), che è un flusso diverso.
   *
   * Non lo elimino perché descrive un comportamento desiderabile: resta come specifica eseguibile
   * del giorno in cui qualcuno la implementerà.
   */
  test.skip('Duplicate warning prevents conflicts', async () => {
    // Intenzionalmente vuoto: vedi il commento sopra.
  });

  test('User can cancel and return to list', async ({ page }) => {
    const partialGame = createTestGameData();
    await page.getByLabel(/^Title/).fill(partialGame.title);
    await page.getByLabel(/^Year Published/).fill(String(partialGame.yearPublished));

    await page.getByRole('button', { name: 'Cancel' }).click();

    // La destinazione è `/all`, non `/admin/shared-games` (client.tsx:452).
    await expect(page).toHaveURL(/\/admin\/shared-games\/all/, { timeout: 30_000 });
    await expect(page.getByText(partialGame.title)).toHaveCount(0);
  });

  test('Admin can edit created game', async ({ page }) => {
    const originalGame = createTestGameData();
    await fillGameForm(page, originalGame);
    await page.getByRole('button', { name: 'Create Game' }).click();
    await expect(page).toHaveURL(DETAIL_URL, { timeout: 30_000 });

    // La modifica avviene in un drawer (EditGameDrawer), non su una pagina a sé.
    await page
      .getByRole('button', { name: /modifica|edit/i })
      .first()
      .click();
    await expect(page.getByTestId('edit-game-drawer')).toBeVisible();

    // Le etichette del drawer sono in italiano, a differenza del form di creazione.
    const updatedTitle = `${originalGame.title} - Updated`;
    await page.getByLabel(/^Titolo/).fill(updatedTitle);

    // Il pulsante è `disabled={!isDirty}`: senza una modifica reale il click non fa nulla, e un
    // test che non sporcasse il form passerebbe senza salvare.
    const submit = page.getByTestId('edit-game-submit');
    await expect(submit).toBeEnabled();
    await submit.click();

    await expect(page.getByRole('heading', { name: updatedTitle })).toBeVisible({
      timeout: 30_000,
    });
  });
});
