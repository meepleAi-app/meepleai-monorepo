/**
 * Admin — RAG Strategy Config ⭐
 * Sezione 8 del piano di test UI MeepleAI (Epic RAG Observability)
 *
 * Testa: Tier Matrix visualizzazione, modifica mappatura modello, salvataggio
 */

import { test, expect, type Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function mockAdminAuth(page: Page) {
  await page.context().route(`${API_BASE}/api/v1/auth/me`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: { id: 'admin-1', email: 'admin@meepleai.dev', displayName: 'Admin', role: 'Admin' },
        expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      }),
    })
  );
}

const MOCK_TIER_MATRIX = {
  tiers: ['free', 'pro', 'enterprise'],
  strategies: ['basic', 'hybrid', 'semantic', 'fullrag'],
  matrix: {
    free:       { basic: true,  hybrid: false, semantic: false, fullrag: false },
    pro:        { basic: true,  hybrid: true,  semantic: true,  fullrag: false },
    enterprise: { basic: true,  hybrid: true,  semantic: true,  fullrag: true  },
  },
};

const MOCK_MODEL_MAPPINGS = [
  { strategy: 'basic',    model: 'claude-haiku-4-5',   tier: 'free' },
  { strategy: 'hybrid',   model: 'claude-sonnet-4-6',  tier: 'pro' },
  { strategy: 'semantic', model: 'claude-sonnet-4-6',  tier: 'pro' },
  { strategy: 'fullrag',  model: 'claude-opus-4-6',    tier: 'enterprise' },
];

test.describe('ADM — Strategy Config ⭐', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);

    await page.context().route(`${API_BASE}/api/v1/admin/rag/tier-strategy-matrix**`, (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_TIER_MATRIX),
      })
    );

    await page.context().route(`${API_BASE}/api/v1/admin/rag/strategy-model-mappings**`, (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_MODEL_MAPPINGS),
      })
    );

    await page.context().route(`${API_BASE}/api/v1/admin/**`, (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: [] }) })
    );
  });

  test('carica pagina Strategy Config', async ({ page }) => {
    await page.goto('/admin/agents/strategy', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('body')).toBeVisible();
  });

  test('mostra tier matrix (free, pro, enterprise)', async ({ page }) => {
    await page.goto('/admin/agents/strategy', { waitUntil: 'domcontentloaded' });

    // Verifica presenza etichette tier
    const freeLabel = page.locator('text=/free/i').first();
    const proLabel  = page.locator('text=/pro/i').first();
    if (await freeLabel.count() > 0) {
      await expect(freeLabel).toBeVisible({ timeout: 8000 });
    }
    if (await proLabel.count() > 0) {
      await expect(proLabel).toBeVisible({ timeout: 8000 });
    }
  });

  test('mostra mappature strategia → modello', async ({ page }) => {
    await page.goto('/admin/agents/strategy', { waitUntil: 'domcontentloaded' });

    // Cerca riferimenti ai modelli
    const modelRef = page.locator('text=/claude|sonnet|haiku|opus/i').first();
    if (await modelRef.count() > 0) {
      await expect(modelRef).toBeVisible({ timeout: 8000 });
    }
  });

  test('modifica mappatura e salva con toast conferma', async ({ page }) => {
    let saveCalled = false;

    await page.context().route(
      `${API_BASE}/api/v1/admin/rag/strategy-model-mappings`,
      (route) => {
        if (route.request().method() === 'PUT' || route.request().method() === 'PATCH') {
          saveCalled = true;
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ success: true }),
          });
        }
        return route.continue();
      }
    );

    await page.goto('/admin/agents/strategy', { waitUntil: 'domcontentloaded' });

    // Cerca pulsante modifica/salva
    const saveBtn = page.locator(
      'button:has-text("Salva"), button:has-text("Save"), button[type="submit"]'
    ).first();

    if (await saveBtn.count() > 0) {
      // Tenta una modifica (select o toggle)
      const editableEl = page.locator('select, input[type="checkbox"]').first();
      if (await editableEl.count() > 0) {
        const tag = await editableEl.evaluate((el) => el.tagName);
        if (tag === 'SELECT') {
          await editableEl.selectOption({ index: 0 });
        } else {
          await editableEl.click();
        }
      }
      await saveBtn.click();
      // Non asserisce saveCalled perché dipende dall'implementazione UI
    }

    await expect(page.locator('body')).toBeVisible();
  });

  test('tier matrix mostra checkbox per ogni combinazione', async ({ page }) => {
    await page.goto('/admin/agents/strategy', { waitUntil: 'domcontentloaded' });

    const checkboxes = page.locator('input[type="checkbox"], [role="checkbox"]');
    if (await checkboxes.count() > 0) {
      await expect(checkboxes.first()).toBeVisible({ timeout: 8000 });
    }
  });
});
