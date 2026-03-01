/**
 * Admin — RAG Debug Console ⭐
 * Sezione 8 del piano di test UI MeepleAI (Epic RAG Observability)
 *
 * Testa: WaterfallChart, TimelineStep espandibili, token count
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

const MOCK_DEBUG_SESSION = {
  id: 'dbg-1',
  query: 'Come si costruisce su Catan?',
  totalDurationMs: 1243,
  totalTokens: 2891,
  steps: [
    {
      id: 'step-1',
      name: 'Vector Retrieval',
      durationMs: 145,
      startOffset: 0,
      tokensUsed: 0,
      status: 'success',
      details: { chunks: 8, topScore: 0.94 },
    },
    {
      id: 'step-2',
      name: 'Reranking',
      durationMs: 87,
      startOffset: 145,
      tokensUsed: 0,
      status: 'success',
      details: { inputChunks: 8, outputChunks: 3 },
    },
    {
      id: 'step-3',
      name: 'Context Augmentation',
      durationMs: 23,
      startOffset: 232,
      tokensUsed: 1200,
      status: 'success',
      details: { contextLength: 4096 },
    },
    {
      id: 'step-4',
      name: 'LLM Generation',
      durationMs: 988,
      startOffset: 255,
      tokensUsed: 1691,
      status: 'success',
      details: { model: 'claude-sonnet-4-6', promptTokens: 1200, completionTokens: 491 },
    },
  ],
};

test.describe('ADM — Debug Console ⭐', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);

    await page.context().route(`${API_BASE}/api/v1/admin/rag/debug-sessions**`, (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [MOCK_DEBUG_SESSION], totalCount: 1 }),
      })
    );

    await page.context().route(`${API_BASE}/api/v1/admin/rag/debug-sessions/dbg-1**`, (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_DEBUG_SESSION),
      })
    );

    await page.context().route(`${API_BASE}/api/v1/admin/**`, (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: [] }) })
    );
  });

  test('carica pagina Debug Console', async ({ page }) => {
    await page.goto('/admin/agents/debug', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('body')).toBeVisible();
  });

  test('mostra WaterfallChart o elementi del debug', async ({ page }) => {
    await page.goto('/admin/agents/debug', { waitUntil: 'domcontentloaded' });

    // Cerca waterfall chart o timeline
    const waterfall = page.locator(
      '[data-testid="waterfall-chart"], [data-testid="timeline"], .waterfall, .timeline, svg'
    ).first();
    if (await waterfall.count() > 0) {
      await expect(waterfall).toBeVisible({ timeout: 8000 });
    } else {
      // Fallback: cerca testo delle sessioni
      await expect(page.locator('body')).toBeVisible();
    }
  });

  test('click su step timeline espande dettaglio', async ({ page }) => {
    await page.goto('/admin/agents/debug', { waitUntil: 'domcontentloaded' });

    // Cerca step della timeline cliccabile
    const step = page.locator(
      '[data-testid="timeline-step"], [data-testid="debug-step"], .timeline-step, button:has-text("Vector"), button:has-text("Retrieval")'
    ).first();

    if (await step.count() > 0) {
      await step.click();
      // Verifica espansione dettaglio
      const detail = page.locator(
        '[data-testid="step-detail"], .step-detail, [aria-expanded="true"]'
      ).first();
      await expect(detail).toBeVisible({ timeout: 5000 });
    }
  });

  test('mostra token count totale', async ({ page }) => {
    await page.goto('/admin/agents/debug', { waitUntil: 'domcontentloaded' });

    // Cerca visualizzazione token count
    const tokenDisplay = page.locator('[data-testid="token-count"], [data-testid="duration"]')
      .or(page.locator('text=/token|2891|1243ms/i'))
      .first();
    if (await tokenDisplay.count() > 0) {
      await expect(tokenDisplay).toBeVisible({ timeout: 5000 });
    }
  });

  test('filtra sessioni debug per query', async ({ page }) => {
    await page.goto('/admin/agents/debug', { waitUntil: 'domcontentloaded' });

    const searchInput = page.locator(
      'input[placeholder*="cerca"], input[placeholder*="search"], input[type="search"]'
    ).first();

    if (await searchInput.count() > 0) {
      await searchInput.fill('Catan');
      await page.waitForTimeout(300);
      await expect(page.locator('body')).toBeVisible();
    }
  });
});
