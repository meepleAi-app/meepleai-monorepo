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
   * Il corpo è corretto contro la UI reale (ruolo `link` per `Create Agent`, `combobox` per
   * `Model`, submit `Save Agent`). Restava saltato con `DIFETTO: #4102` perché ogni submit
   * riceveva `422` per due cause indipendenti, entrambe ora chiuse:
   *   - `CreateAgentDefinitionCommandValidator` imponeva un campo `type` che il form non
   *     aveva. #4138 ha ritirato `AgentDefinition.Type`, quindi non c'è più nulla da inviare
   *     e la regola del validator è uscita con il campo;
   *   - dei 5 modelli cablati in `AgentBuilderForm`, 4 erano id nudi di provider cloud che il
   *     backend rifiuta come «not routable». Le opzioni ora arrivano da
   *     `/api/v1/admin/ai-models`, filtrate da `isRoutableModelId` — lo stesso predicato del
   *     validator — e `AgentBuilderForm.models.test.tsx` lo asserisce sul form, non solo sul
   *     predicato.
   *
   * ⚠️ Lo skip è stato togliato senza poter eseguire questa spec contro uno stack vivo: serve
   * `hasRealAdminCredentials`. Se torna rossa, la causa NON è più una di quelle due.
   */
  test('should create new agent definition', async ({ page }) => {
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
