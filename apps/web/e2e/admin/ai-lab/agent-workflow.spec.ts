/**
 * AI Lab — flusso di creazione di un Agent Definition.
 *
 * #4098 — riscritta. La versione precedente non era mai stata eseguita (viveva in una
 * directory che nessuna config Playwright raccoglieva, #4092) e aveva **due** difetti
 * indipendenti, nessuno dei quali era «selettori vecchi»:
 *
 *   1. Era ANONIMA. `proxy.ts` rimanda `/admin/**` a `/login`, quindi tutte le asserzioni
 *      cadevano sulla pagina di login. Il sintomo (`toBeVisible` falisce) somiglia a una
 *      deriva di selettori, e misurando l'istantanea di errore si vede invece
 *      `login?from=%2Fadmin%2F...`.
 *   2. Puntava a rotte che non esistono più: `/admin/agent-definitions*` e
 *      `/admin/strategies` sono diventate `/admin/agents/definitions*` e
 *      `/admin/agents/strategy` (che a sua volta reindirizza a `/admin/agents/config`).
 *
 * Verità a terra dei selettori:
 *   lista      → `src/app/admin/(dashboard)/agents/definitions/page.tsx` — h1 `Agent
 *                Definitions`, pulsante `Create Agent`
 *   creazione  → `.../definitions/create/page.tsx` — h1 `Create Agent Definition`, form
 *                `AgentBuilderForm` con `FormLabel` `Agent Name` / `Description` e submit
 *                `Save` (NON `input[name=...]`: è react-hook-form + shadcn, i campi non
 *                hanno attributo `name` nel DOM); al successo
 *                `router.push('/admin/agents/definitions')`
 *   playground → `.../definitions/playground/page.tsx` (NON `/admin/agents/playground`,
 *                che è una pagina diversa)
 *   strategy   → `/admin/agents/strategy` fa `redirect('/admin/agents/config')`, dove la
 *                tab `Strategy` è il vero editor
 *
 * L'intestazione originale citava «Issue #3819 (Epic #3687)»: su GitHub #3819 è una PR di
 * release di osservabilità, non questo lavoro. Numeri di uno schema precedente, non
 * ripropagati qui.
 *
 * @see docs/for-developers/audits/2026-10-06-orphan-e2e-specs-classification.md
 */

import { test, expect } from '@playwright/test';

import {
  hasRealAdminCredentials,
  loginAsRealAdmin,
  MISSING_CREDENTIALS_REASON,
} from '../../_helpers/realAdminAuth';

const DEFINITIONS_URL = '/admin/agents/definitions';

test.describe('AI Lab - Agent Workflow', () => {
  test.beforeEach(async ({ page }) => {
    test.skip(!hasRealAdminCredentials, MISSING_CREDENTIALS_REASON);
    await loginAsRealAdmin(page);
  });

  /**
   * SALTATO per un difetto del PRODOTTO, non del test — e il test lo ha trovato.
   *
   * Il corpo qui sotto è corretto contro la UI reale (ruolo `link` per `Create Agent`,
   * `combobox` per `Model`, submit `Save Agent`), ma la submit riceve sempre `422`:
   *   - `createAgentDefinitionSchema` non ha il campo `type`, che
   *     `CreateAgentDefinitionCommandValidator` (#3708) impone con `NotEmpty` e un elenco
   *     chiuso di valori; il form non ha alcun controllo per sceglierlo;
   *   - dei 5 modelli cablati in `AgentBuilderForm.MODELS`, 4 sono id nudi di provider
   *     cloud che il backend rifiuta come «not routable». Solo `deepseek-chat` passa.
   *
   * Provato con POST dirette: `type=Conversation` + `deepseek-chat` crea l'agente, mentre
   * `type=Conversation` + `gpt-4` (un'opzione della tendina) dà 422. Il backend funziona.
   *
   * Quando #4102 chiude, togli la riga `test.skip` e il test deve passare così com'è.
   */
  test('should create new agent definition', async ({ page }) => {
    test.skip(
      true,
      'DIFETTO: #4102 — il form di creazione non invia `type` (obbligatorio lato backend) e offre 4 modelli su 5 non instradabili: ogni submit riceve 422'
    );

    await page.goto(DEFINITIONS_URL);
    await expect(page.getByRole('heading', { name: 'Agent Definitions' })).toBeVisible();

    await page.getByRole('link', { name: 'Create Agent' }).click();
    await expect(page).toHaveURL(/\/admin\/agents\/definitions\/create/);

    const name = `E2E Agent ${process.env.TEST_WORKER_INDEX ?? '0'}-${Math.floor(performance.now())}`;
    await page.getByLabel('Agent Name').fill(name);
    await page.getByLabel('Description').fill('Created by the #4098 AI Lab E2E flow.');

    // `model` è obbligatorio nello schema zod (`z.string().min(1, 'Model required')`) e non
    // ha valore predefinito: senza questa selezione la submit non parte e la pagina resta
    // su /create — è esattamente così che questa riscrittura ha fallito la prima volta.
    // Il trigger è un `SelectTrigger` Radix (un <button> con il placeholder), quindi
    // `selectOption` della versione precedente non poteva funzionare: non è un <select>.
    await page.getByRole('combobox', { name: 'Model' }).click();
    const modelOptions = page.getByRole('option');
    // Le opzioni arrivano da `/api/v1/admin/ai-models`: se la lista è vuota il test deve
    // dirlo qui, non fallire più tardi su un redirect che non avviene.
    await expect(modelOptions.first()).toBeVisible({ timeout: 15_000 });
    await modelOptions.first().click();

    await page.getByRole('button', { name: 'Save Agent' }).click();

    // Il redirect alla lista è l'unica prova che la POST è riuscita: in caso di errore la
    // pagina resta su /create e mostra un toast.
    await expect(page).toHaveURL(new RegExp(`${DEFINITIONS_URL}$`), { timeout: 30_000 });
    await expect(page.getByText(name)).toBeVisible();
  });

  test('should navigate to playground', async ({ page }) => {
    await page.goto(`${DEFINITIONS_URL}/playground`);
    await expect(page.getByRole('heading', { name: /Agent Playground/i })).toBeVisible();
  });

  test('should load strategy editor', async ({ page }) => {
    await page.goto('/admin/agents/strategy');

    // La rotta vecchia sopravvive come redirect: asserirlo documenta il contratto, così
    // un giorno che il redirect sparisse il test lo direbbe.
    await expect(page).toHaveURL(/\/admin\/agents\/config/, { timeout: 30_000 });
    await expect(page.getByText('Strategy', { exact: true }).first()).toBeVisible();
  });
});
