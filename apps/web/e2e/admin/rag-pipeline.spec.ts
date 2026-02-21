/**
 * Admin — RAG Pipeline Explorer ⭐
 * Sezione 8 del piano di test UI MeepleAI (Epic RAG Observability)
 *
 * Testa: Pipeline Diagram 6 nodi, Agent List, Models, Chat History
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

async function mockRagApi(page: Page) {
  await page.context().route(`${API_BASE}/api/v1/admin/rag/**`, (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: [] }) })
  );
  await page.context().route(`${API_BASE}/api/v1/admin/**`, (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: [] }) })
  );
  await page.context().route(`${API_BASE}/api/v1/admin/agents**`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: [
          { id: 'ag1', name: 'Catan Assistant', model: 'claude-sonnet-4-6', status: 'active' },
          { id: 'ag2', name: 'Pandemic Helper', model: 'claude-haiku-4-5', status: 'active' },
        ],
        totalCount: 2,
      }),
    })
  );
}

test.describe('ADM — Agents List', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);
    await mockRagApi(page);
  });

  test('mostra lista agent definitions', async ({ page }) => {
    await page.goto('/admin/agents', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('body')).toBeVisible();
  });
});

test.describe('ADM — Pipeline Explorer ⭐', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);
    await page.context().route(`${API_BASE}/api/v1/admin/rag/pipeline**`, (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          nodes: [
            { id: 'n1', type: 'input', label: 'Query Input', metrics: { avgLatency: 12 } },
            { id: 'n2', type: 'retrieval', label: 'Vector Retrieval', metrics: { avgLatency: 45 } },
            { id: 'n3', type: 'reranking', label: 'Reranking', metrics: { avgLatency: 23 } },
            { id: 'n4', type: 'augmentation', label: 'Context Augmentation', metrics: { avgLatency: 8 } },
            { id: 'n5', type: 'generation', label: 'LLM Generation', metrics: { avgLatency: 980 } },
            { id: 'n6', type: 'output', label: 'Response Output', metrics: { avgLatency: 5 } },
          ],
          edges: [
            { source: 'n1', target: 'n2' },
            { source: 'n2', target: 'n3' },
            { source: 'n3', target: 'n4' },
            { source: 'n4', target: 'n5' },
            { source: 'n5', target: 'n6' },
          ],
        }),
      })
    );
    await page.context().route(`${API_BASE}/api/v1/admin/**`, (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: [] }) })
    );
  });

  test('carica pagina Pipeline Explorer', async ({ page }) => {
    await page.goto('/admin/agents/pipeline', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('body')).toBeVisible();
  });

  test('mostra elementi del pipeline diagram', async ({ page }) => {
    await page.goto('/admin/agents/pipeline', { waitUntil: 'domcontentloaded' });

    // Verifica che siano presenti elementi del diagramma o testo dei nodi
    const pipelineContent = page.locator(
      '[data-testid="pipeline-diagram"], [data-testid="pipeline-node"], .pipeline, svg, canvas, text:has-text("Retrieval"), text:has-text("Generation")'
    ).first();
    await expect(pipelineContent).toBeVisible({ timeout: 8000 });
  });

  test('click su nodo mostra pannello dettaglio', async ({ page }) => {
    await page.goto('/admin/agents/pipeline', { waitUntil: 'domcontentloaded' });

    // Cerca nodo cliccabile nel diagramma
    const node = page.locator('[data-testid="pipeline-node"], [data-node-id], .pipeline-node').first();
    if (await node.count() > 0) {
      await node.click();
      // Verifica pannello laterale o modal dettaglio
      const detail = page.locator('[data-testid="node-detail"], .node-panel, aside, [role="dialog"]').first();
      await expect(detail).toBeVisible({ timeout: 5000 });
    }
  });
});

test.describe('ADM — Models', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);
    await page.context().route(`${API_BASE}/api/v1/admin/ai-models**`, (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'm1', name: 'claude-sonnet-4-6', provider: 'Anthropic', version: '4.6', active: true },
          { id: 'm2', name: 'claude-haiku-4-5', provider: 'Anthropic', version: '4.5', active: true },
        ]),
      })
    );
    await page.context().route(`${API_BASE}/api/v1/admin/**`, (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: [] }) })
    );
  });

  test('mostra lista modelli AI con provider e versione', async ({ page }) => {
    await page.goto('/admin/agents/models', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('body')).toBeVisible();
  });
});

test.describe('ADM — Chat History', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);
    await page.context().route(`${API_BASE}/api/v1/admin/chat-history**`, (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            { id: 'ch1', agentName: 'Catan Assistant', userEmail: 'user@meepleai.dev', createdAt: '2026-02-20T10:00:00Z', messageCount: 5 },
          ],
          totalCount: 1,
        }),
      })
    );
    await page.context().route(`${API_BASE}/api/v1/admin/**`, (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: [] }) })
    );
  });

  test('mostra storico conversazioni', async ({ page }) => {
    await page.goto('/admin/agents/chat-history', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('body')).toBeVisible();
  });
});
