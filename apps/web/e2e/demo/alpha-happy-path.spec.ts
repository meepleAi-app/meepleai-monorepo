/**
 * Alpha Happy Path Demo — Mobile (Pixel 5)
 *
 * Esegue il flusso utente Alpha completo (Auth → Library → Add Game → Upload PDF → Chat)
 * raccogliendo screenshot key frame in `test-results/demo-alpha/{local|staging}/`.
 *
 * Run:
 *   # Locale
 *   pnpm exec playwright test e2e/demo/alpha-happy-path.spec.ts --project=demo-mobile-local --headed
 *   # Staging (richiede CF_ACCESS_CLIENT_ID + CF_ACCESS_CLIENT_SECRET)
 *   DEMO_TARGET=staging pnpm exec playwright test e2e/demo/alpha-happy-path.spec.ts --project=demo-mobile-staging --headed
 */
import fs from 'node:fs';
import path from 'node:path';

import { test, expect, type Page } from '@playwright/test';

const TARGET = (process.env.DEMO_TARGET ?? 'local') as 'local' | 'staging';
const SCREENSHOT_DIR = path.resolve(__dirname, '../../test-results/demo-alpha', TARGET);
const PDF_PATH = path.resolve(__dirname, '../../../../data/rulebook/catan_en_rulebook.pdf');

const CREDS = {
  email: process.env.DEMO_EMAIL ?? 'badsworm@gmail.com',
  // Local: hash computed manually & UPDATE'd in DB (seeder bug bypass — see issue #888)
  // Staging: must match SEED_BADSWORM_PASSWORD on staging server (likely TestNanolith2026!)
  password: process.env.DEMO_PASSWORD ?? 'TestDemo2026Pass',
};

const QUERIES = [
  'Spiegami il setup del gioco',
  'Cosa si fa durante il turno?',
  'Quanti giocatori servono?',
  'Come si vince la partita?',
  'Cosa succede quando esce un 7?',
  'Come funziona il commercio tra giocatori?',
  'Quali sono i costi per costruire una città?',
  'Cosa fa la carta cavaliere?',
  'Posso costruire una strada se non ho legno?',
  'Qual è il limite di carte risorsa in mano?',
];

test.describe.configure({ mode: 'serial' });

test.beforeAll(() => {
  fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
  if (!fs.existsSync(PDF_PATH)) {
    throw new Error(`PDF demo non trovato: ${PDF_PATH}`);
  }
});

async function shot(page: Page, name: string) {
  await page.screenshot({
    path: path.join(SCREENSHOT_DIR, `${name}.png`),
    fullPage: true,
  });
}

test('Alpha demo — happy path mobile', async ({ page }) => {
  test.setTimeout(15 * 60_000); // 15 min: chat 10 query + upload PDF + indexing

  // ─── 01: Landing ──────────────────────────────────────────────────────────
  await page.goto('/');
  await page.waitForLoadState('domcontentloaded');
  await shot(page, '01-landing');

  // ─── Cleanup pre-test (idempotent run) ─────────────────────────────────────
  // Use a fresh browser context for cleanup to avoid setting cookies that auto-login the page.
  const cleanupCtx = await page.context().browser()!.newContext();
  try {
    const apiLogin = await cleanupCtx.request.post('http://localhost:8080/api/v1/auth/login', {
      data: { email: CREDS.email, password: CREDS.password },
    });
    if (apiLogin.ok()) {
      console.log('[cleanup] API login OK');
      const libResp = await cleanupCtx.request.get('http://localhost:8080/api/v1/library');
      if (libResp.ok()) {
        const lib = await libResp.json();
        const items = lib.items ?? lib.games ?? lib.entries ?? [];
        for (const it of items) {
          const name = (it.gameTitle ?? it.name ?? it.title ?? it.gameName ?? '').toString();
          const gid = it.gameId ?? it.id;
          if (/catan/i.test(name) && gid) {
            const del = await cleanupCtx.request.delete(
              `http://localhost:8080/api/v1/library/games/${gid}`
            );

            console.log(`[cleanup] DELETE Catan ${gid} → ${del.status()}`);
          }
        }
      }
    }
  } finally {
    await cleanupCtx.close();
  }

  // ─── 02-04: Login ─────────────────────────────────────────────────────────
  await page.goto('/login');
  await page.waitForLoadState('domcontentloaded');
  await shot(page, '02-login-empty');

  await page.getByLabel(/email/i).fill(CREDS.email);
  await page
    .getByLabel(/password/i)
    .first()
    .fill(CREDS.password);
  await shot(page, '03-login-filled');

  await page.getByRole('button', { name: /accedi|sign in|log\s*in/i }).click();
  await page.waitForURL(/\/(library|dashboard|onboarding|setup|admin)/, { timeout: 30_000 });
  await shot(page, '04-post-login');

  // ─── 05: Library iniziale ─────────────────────────────────────────────────
  await page.goto('/library');
  await page.waitForLoadState('domcontentloaded');
  await shot(page, '05-library-initial');

  // ─── 06-07: Apri AddGameDrawer + scegli Catalogo ──────────────────────────
  await page.goto('/library?action=add');
  await expect(page.getByTestId('add-game-drawer')).toBeVisible({ timeout: 10_000 });
  await expect(page.getByTestId('add-game-step-choice')).toBeVisible();
  await shot(page, '06-drawer-choice');

  await page.getByTestId('add-game-choice-catalog').click();
  await expect(page.getByTestId('add-game-step-catalog')).toBeVisible();
  await shot(page, '07-catalog-search');

  // ─── 08-09: Cerca Catan + select ──────────────────────────────────────────
  await page.getByTestId('catalog-search-input').fill('Catan');
  await expect(page.getByTestId('catalog-search-grid')).toBeVisible({ timeout: 10_000 });
  // Wait for at least one card to appear
  const firstCard = page
    .locator('[data-testid^="catalog-card-"][data-testid$="-select-btn"]')
    .first();
  await expect(firstCard).toBeVisible({ timeout: 10_000 });
  await shot(page, '08-catalog-results');

  await firstCard.click();
  await expect(page.getByTestId('add-game-step-catalog-pdf')).toBeVisible({ timeout: 10_000 });
  await shot(page, '09-pdf-step');

  // ─── 10-11: Upload PDF Catan ──────────────────────────────────────────────
  const fileInput = page.locator('input[type="file"][accept="application/pdf"]');
  await fileInput.setInputFiles(PDF_PATH);
  await shot(page, '10-pdf-selected');

  // Click upload button + wait for upload completion (button text changes from "Caricamento...")
  const uploadBtn = page.getByRole('button', { name: /carica|upload/i }).first();
  await uploadBtn.click();

  // Wait until upload progresses past 0% then completes (wizard auto-advances to agent step
  // OR the upload button disabled/changed state)
  await page
    .waitForFunction(
      () => {
        // Check 1: wizard advanced to agent step
        const agentStep = document.querySelector('[data-testid="add-game-step-catalog-pdf"]');
        if (!agentStep) return true;
        // Check 2: button text no longer "Caricamento..."
        const btn = Array.from(document.querySelectorAll('button')).find(b =>
          /caricamento/i.test(b.textContent ?? '')
        );
        return !btn;
      },
      { timeout: 4 * 60_000 }
    )
    .catch(() => {});
  await page.waitForLoadState('domcontentloaded');
  await shot(page, '11-pdf-uploaded');

  // ─── 12: Wizard agent step → Salta Agente ─────────────────────────────────
  // The wizard auto-advances to ConfigAgentStep after PDF upload.
  // We skip agent creation in wizard and use API later for cleaner flow.
  const skipAgentBtn = page.getByRole('button', { name: /salta agente/i });
  if (await skipAgentBtn.isVisible({ timeout: 8_000 }).catch(() => false)) {
    await shot(page, '12-agent-step');
    await skipAgentBtn.click();
  }

  // Wait drawer to close (catalog flow redirects to /library/games/{id})
  await page.waitForURL(/\/library/, { timeout: 15_000 }).catch(() => {});
  await page.waitForLoadState('domcontentloaded');

  // ─── 13: Library con Catan aggiunto ───────────────────────────────────────
  await page.goto('/library');
  await page.waitForLoadState('domcontentloaded');
  await expect(page.getByText(/catan/i).first()).toBeVisible({ timeout: 15_000 });
  await shot(page, '13-library-with-catan');

  // ─── 14: Quick-create agent + wait indexing ───────────────────────────────
  // Identify the user's library Catan game ID via API (cookies inherited from page).
  const libraryResp = await page.request.get('/api/v1/library');
  const libraryJson = await libraryResp.json();
  // Fallback shape: { items: [...] } or { games: [...] } or { entries: [...] }
  const items = libraryJson.items ?? libraryJson.games ?? libraryJson.entries ?? [];
  const catanEntry = items.find(
    (g: { gameTitle?: string; name?: string; title?: string; gameName?: string }) =>
      /catan/i.test(g.gameTitle ?? g.name ?? g.title ?? g.gameName ?? '')
  );
  if (!catanEntry) {
    throw new Error(`Catan not found in user library. Response keys: ${Object.keys(libraryJson)}`);
  }
  const userGameId = catanEntry.gameId ?? catanEntry.id;

  console.log(`User game ID: ${userGameId}`);

  // Poll library until KB ready (hasKb=true means PDF indexed)
  const indexingStart = Date.now();
  let kbReady = false;
  while (Date.now() - indexingStart < 8 * 60_000) {
    const libR = await page.request.get('/api/v1/library');
    if (libR.ok()) {
      const lib = await libR.json();
      const entry = (lib.items ?? []).find((g: { gameTitle?: string }) =>
        /catan/i.test(g.gameTitle ?? '')
      );
      if (entry?.hasKb === true && (entry.kbIndexedCount ?? 0) > 0) {
        kbReady = true;
        break;
      }
    }
    await page.waitForTimeout(5_000);
  }

  console.log(`KB ready: ${kbReady} (${Math.round((Date.now() - indexingStart) / 1000)}s)`);

  // Find first agent definition (e.g. "Rules Expert") — quick-create AgentType=Tutor not seeded locally
  const defsR = await page.request.get('/api/v1/agents');
  const defsJson = await defsR.json();
  const firstDef = (defsJson.agents ?? [])[0];
  if (!firstDef) throw new Error('No agent definitions found in DB');

  console.log(`Using agentDefinitionId: ${firstDef.id} (${firstDef.name})`);

  // Skip library agent bind (500 error in current API) — use definition id directly for chat
  const agentId = firstDef.id;

  console.log(`Using agentId for chat: ${agentId}`);

  // ─── 15: Agents list ──────────────────────────────────────────────────────
  await page.goto('/agents');
  await page.waitForLoadState('domcontentloaded');
  // Wait for any agent text to be visible (agent names vary)
  await page
    .getByText(new RegExp(firstDef.name.split(' ')[0] ?? 'agent', 'i'))
    .first()
    .waitFor({ timeout: 15_000 })
    .catch(() => {});
  await shot(page, '15-agents-list');

  // ─── 16: Open chat ────────────────────────────────────────────────────────
  // Dismiss cookie banner once (covers all subsequent screenshots)
  const cookieBtn = page.getByRole('button', { name: /essential only|accept all/i }).first();
  if (await cookieBtn.isVisible().catch(() => false)) {
    await cookieBtn.click();
    await page.waitForTimeout(500);
  }

  await page.goto(`/chat/new?game=${userGameId}`);
  await page.waitForLoadState('domcontentloaded');
  // Wait for "Preparazione..." spinner to finish (NewChatView dynamic load + thread init)
  await page
    .waitForFunction(() => !document.body.innerText.toLowerCase().includes('preparazione'), {
      timeout: 60_000,
    })
    .catch(() => {});
  await page.waitForTimeout(2_000);
  await shot(page, '16-chat-empty');

  // ─── 17+: Chat 10 query Catan (best-effort) ──────────────────────────────
  // Wait up to 30s for chat textbox to appear; if not, capture state and skip queries.
  const chatInput = page.getByRole('textbox').last();
  const chatReady = await chatInput.isVisible({ timeout: 30_000 }).catch(() => false);
  if (!chatReady) {
    console.log('Chat input not ready — skipping queries, capturing state only');
    await shot(page, '17-chat-not-ready');
  } else {
    for (const [i, q] of QUERIES.entries()) {
      const idx = String(i + 1).padStart(2, '0');
      try {
        await chatInput.click();
        await chatInput.fill(q);
        const sendBtn = page.getByRole('button', { name: /invia|send|chiedi/i }).last();
        if (await sendBtn.isVisible().catch(() => false)) {
          await sendBtn.click();
        } else {
          await page.keyboard.press('Enter');
        }
        await page
          .waitForResponse(
            r =>
              (r.url().includes('/chat') ||
                r.url().includes('/qa') ||
                r.url().includes('/agents/')) &&
              r.request().method() === 'POST' &&
              r.status() === 200,
            { timeout: 120_000 }
          )
          .catch(() => {});
        await page.waitForTimeout(2_000);
      } catch (err) {
        console.log(`Query ${idx} failed: ${(err as Error).message.slice(0, 100)}`);
      }
      await shot(page, `17-chat-q${idx}`);
    }
  }

  // ─── 99: Library finale ───────────────────────────────────────────────────
  await page.goto('/library');
  await page.waitForLoadState('domcontentloaded');
  await shot(page, '99-library-final');
});
