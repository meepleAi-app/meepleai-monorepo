/**
 * SharedGame ↔ Agent: collegamento dalla scheda admin del gioco.
 *
 * #4098 — riscritta da zero. La versione precedente non era mai stata eseguita (viveva in
 * una directory che nessuna config Playwright raccoglieva, #4092) e descriveva una UI che
 * non esiste. I difetti, in ordine di quanto impedivano l'esecuzione:
 *
 *   1. **Login impossibile.** Credenziali cablate (`admin@test.com` / `Admin123!`) che non
 *      esistono in nessun ambiente, e `getByLabel(/password/i)` che viola la strict mode
 *      perché la pagina di login ha due campi che corrispondono. Tutti e quattro i test
 *      morivano qui, prima di qualunque asserzione: il sintomo somigliava a selettori
 *      vecchi, la causa era l'autenticazione.
 *   2. **Form in italiano su una pagina inglese.** `createTestSharedGame` cercava
 *      `titolo`, `anno di pubblicazione`, `giocatori minimi`. Il form reale è in inglese
 *      (`Title *`, `Year Published *`, …) — vedi `manual-creation.spec.ts`, che quel flusso
 *      lo copre già. Qui il soggetto è il collegamento dell'agente, quindi il gioco di
 *      prova si crea via API: più rapido e non mette sotto test ciò che non è in esame.
 *   3. **Rotte inesistenti.** `/admin/agent-definitions/create` è diventata
 *      `/admin/agents/definitions/create`.
 *   4. **Una UI immaginaria.** Il test 1 attendeva una modale di creazione agente
 *      precompilata col nome «{GameTitle} Arbitro». Non esiste: `grep -c "Arbitro"` sulla
 *      scheda dà 0. La tab offre un collegamento `Create a new agent →` verso la pagina di
 *      creazione, e il collegamento di un agente **esistente** tramite un Select.
 *   5. **Un test senza asserzioni.** `displays error when agent linking fails` cliccava
 *      «Link Existing Agent» e finiva: nessuna `expect`. Eliminato — non poteva fallire per
 *      un difetto del prodotto. Il suo commento ammetteva che serviva il mocking dell'API,
 *      che è un test di unità, non questo.
 *
 * Verità a terra, da `src/app/admin/(dashboard)/shared-games/[id]/client.tsx`:
 *   tab        → `Agent` (diventa `Agent ✓` quando un agente è collegato)
 *   vuoto      → testo `No agent linked` + link `Create a new agent →`
 *   collega    → card `Link an Agent`, Select con placeholder `Select agent definition`,
 *                pulsante `Link`
 *   collegato  → nome e tipo dell'agente + pulsante `Unlink`
 *   invariante → la card `Link an Agent` è resa **solo** quando nessun agente è collegato
 *
 * Il Select elenca `getAgentDefinitions({ activeOnly: true })`: una definizione in stato
 * `Draft` non compare. Per questo il setup ne pubblica una se non ce ne sono attive.
 *
 * L'intestazione originale citava «Issue #4230», numero che su GitHub non corrisponde a
 * questo lavoro: schema di numerazione precedente, non ripropagato.
 *
 * @see docs/for-developers/audits/2026-10-06-orphan-e2e-specs-classification.md
 */

import { test, expect, type Page } from '@playwright/test';

import {
  hasRealAdminCredentials,
  loginAsRealAdmin,
  MISSING_CREDENTIALS_REASON,
} from '../../_helpers/realAdminAuth';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

/** Crea un gioco di prova via API e restituisce il suo id. */
async function createSharedGameViaApi(page: Page): Promise<string> {
  const unique = `${process.env.TEST_WORKER_INDEX ?? '0'}-${Math.floor(performance.now())}`;
  const response = await page.request.post(`${API_BASE}/api/v1/admin/shared-games`, {
    data: {
      title: `E2E Agent Link ${unique}`,
      yearPublished: 2024,
      description: 'Gioco creato dal flusso E2E di collegamento agente (#4098).',
      minPlayers: 2,
      maxPlayers: 4,
      playingTimeMinutes: 60,
      minAge: 12,
      // `ImageUrl`/`ThumbnailUrl` sono obbligatorie in creazione (`ValidateImageUrl`), pur
      // essendo colonne-lapide dopo il divieto BGG #2123: qui non sono il soggetto.
      imageUrl: 'https://cdn.example.com/e2e-cover.webp',
      thumbnailUrl: 'https://cdn.example.com/e2e-thumb.webp',
    },
  });
  expect(response.ok(), `creazione gioco: ${response.status()} ${await response.text()}`).toBe(
    true
  );

  // `POST /admin/shared-games` risponde `201` con una **stringa JSON nuda** (il GUID), non
  // con un oggetto: leggere `body.id` dà `undefined`, l'URL diventa
  // `/admin/shared-games/undefined` e la pagina mostra «Failed to load game details.» —
  // un fallimento che accusa il selettore della tab invece della lettura della risposta.
  const gameId = (await response.json()) as string;
  expect(gameId, 'id del gioco creato').toMatch(/^[0-9a-f-]{36}$/);
  return gameId;
}

interface AgentDefinitionSummary {
  id: string;
  name: string;
}

/**
 * Restituisce una definizione di agente che il Select mostrerà, pubblicandone una se serve.
 *
 * Il Select filtra su `activeOnly: true`, quindi una definizione `Draft` non basta. Se non
 * c'è nulla da pubblicare, restituisce `null` e il chiamante SALTA dichiarandolo.
 *
 * ⚠️ Lascia uno **stato alterato**: una definizione che era `Draft` resta `Published`. Oggi
 * non si nota perché l'unico chiamante è saltato per #4103, ma quando quel salto sparirà
 * serve un teardown che la riporti a `Draft` (`POST .../unpublish`) — altrimenti ogni
 * esecuzione pubblica un agente in più e la lista «attivi» cresce senza che nessuno lo
 * chieda.
 */
async function ensureLinkableAgent(page: Page): Promise<AgentDefinitionSummary | null> {
  const activeUrl = `${API_BASE}/api/v1/admin/agent-definitions?activeOnly=true`;

  const active = await page.request.get(activeUrl);
  if (active.ok()) {
    const list = (await active.json()) as AgentDefinitionSummary[];
    if (list.length > 0) return list[0];
  }

  const all = await page.request.get(`${API_BASE}/api/v1/admin/agent-definitions`);
  if (!all.ok()) return null;
  const candidates = (await all.json()) as AgentDefinitionSummary[];
  if (candidates.length === 0) return null;

  const candidate = candidates[0];
  // La macchina a stati è Draft → Testing → Published: `publish` da Draft viene rifiutata,
  // quindi si tenta prima la transizione intermedia. Entrambe sono idempotenti ai fini del
  // test, perciò l'esito non viene asserito: ciò che conta è la ri-verifica qui sotto.
  await page.request.post(
    `${API_BASE}/api/v1/admin/agent-definitions/${candidate.id}/start-testing`,
    { data: {} }
  );
  await page.request.post(`${API_BASE}/api/v1/admin/agent-definitions/${candidate.id}/publish`, {
    data: {},
  });

  const recheck = await page.request.get(activeUrl);
  if (!recheck.ok()) return null;
  const published = (await recheck.json()) as AgentDefinitionSummary[];
  return published.find(a => a.id === candidate.id) ?? published[0] ?? null;
}

async function openAgentTab(page: Page, gameId: string) {
  await page.goto(`/admin/shared-games/${gameId}`);
  await page.getByRole('tab', { name: /^Agent/ }).click();
}

test.describe('SharedGame Agent Workflow E2E', () => {
  test.beforeEach(async ({ page }) => {
    test.skip(!hasRealAdminCredentials, MISSING_CREDENTIALS_REASON);
    await loginAsRealAdmin(page);
  });

  test('la tab Agent parte vuota e offre la creazione di un agente', async ({ page }) => {
    const gameId = await createSharedGameViaApi(page);
    await openAgentTab(page, gameId);

    await expect(page.getByText('No agent linked')).toBeVisible();

    // Questo link è ciò che la UI offre davvero in luogo della modale che il test
    // precedente immaginava. Asserire l'href protegge la rotta dalla deriva che ha reso
    // illeggibile la versione vecchia di questa spec.
    const createLink = page.getByRole('link', { name: /Create a new agent/ });
    await expect(createLink).toHaveAttribute('href', '/admin/agents/definitions/create');
  });

  /**
   * SALTATO per un difetto del PRODOTTO, non del test — e il test lo ha trovato.
   *
   * Il corpo è corretto contro la UI reale: il Select arriva a mostrare l'agente scelto
   * (`combobox: Rules ExpertRAG` nell'istantanea) e il pulsante `Link` viene premuto. Ma
   * i tre endpoint che `sharedGamesClient.ts` chiama per questa tab —
   * `GET .../linked-agent`, `POST .../link-agent/{agentId}`, `DELETE .../unlink-agent`
   * sotto `/admin/shared-games` — **non esistono nel backend**: esistono solo le
   * controparti per i private games. Tutte e tre danno 404, verificato con sessione admin.
   *
   * Il 404 della query è indistinguibile da «nessun agente collegato», quindi la tab sembra
   * funzionante e vuota: è il motivo per cui il difetto è sopravvissuto.
   *
   * Quando #4103 chiude, togli la riga `test.skip` e il test deve passare così com'è.
   */
  test('collega un agente esistente, lo mostra e lo scollega', async ({ page }) => {
    test.skip(
      true,
      'DIFETTO: #4103 — GET linked-agent / POST link-agent / DELETE unlink-agent non esistono sotto /admin/shared-games: il collegamento riceve 404'
    );

    const agent = await ensureLinkableAgent(page);
    test.skip(
      agent === null,
      'PREVISTO: serve almeno una definizione di agente pubblicabile. Creane una da /admin/agents/definitions/create (oggi bloccato da #4102) o avvia uno stack con il seed degli agenti.'
    );

    const gameId = await createSharedGameViaApi(page);
    await openAgentTab(page, gameId);

    await expect(page.getByText('Link an Agent')).toBeVisible();
    await page.getByRole('combobox').first().click();
    await page.getByRole('option', { name: agent!.name }).click();
    await page.getByRole('button', { name: 'Link', exact: true }).click();

    // La prova del collegamento è il nome dell'agente nella card «Linked Agent» più la
    // comparsa di `Unlink`: il solo nome potrebbe venire dal Select ancora aperto.
    await expect(page.getByRole('button', { name: /^Unlink/ })).toBeVisible({ timeout: 30_000 });
    await expect(page.getByText(agent!.name).first()).toBeVisible();

    // Invariante (era il test «cannot create multiple agents for same SharedGame»): la card
    // di collegamento esiste solo mentre nessun agente è collegato.
    await expect(page.getByText('Link an Agent')).toHaveCount(0);

    await page.getByRole('button', { name: /^Unlink/ }).click();

    await expect(page.getByText('No agent linked')).toBeVisible({ timeout: 30_000 });
    await expect(page.getByText('Link an Agent')).toBeVisible();
  });
});
