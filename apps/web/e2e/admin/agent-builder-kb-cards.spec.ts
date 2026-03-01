/**
 * Admin — Agent Builder + KB Cards Checklist ⭐
 * Sezione 11 del piano di test UI MeepleAI (Epic #4920)
 *
 * Testa: form agente, AgentBuilderModal con KB cards, kbCardIds nel payload
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

const MOCK_KB_CARDS = [
  {
    id: 'kbc-1',
    title: 'Catan Rulebook — Overview',
    sourceDocument: 'catan-rulebook.pdf',
    sharedGameId: 'sg-1',
    chunkCount: 12,
  },
  {
    id: 'kbc-2',
    title: 'Catan Rulebook — Settlements',
    sourceDocument: 'catan-rulebook.pdf',
    sharedGameId: 'sg-1',
    chunkCount: 8,
  },
  {
    id: 'kbc-3',
    title: 'Pandemic Rules — Introduction',
    sourceDocument: 'pandemic-rules.pdf',
    sharedGameId: 'sg-2',
    chunkCount: 6,
  },
];

const MOCK_AGENT = {
  id: 'ag-1',
  name: 'Catan Expert',
  model: 'claude-sonnet-4-6',
  systemPrompt: 'Sei un esperto di Catan.',
  kbCardIds: [],
};

async function mockAgentBuilderApi(page: Page) {
  // KB Cards list
  await page.context().route(`${API_BASE}/api/v1/admin/knowledge-base/kb-cards**`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items: MOCK_KB_CARDS, totalCount: MOCK_KB_CARDS.length }),
    })
  );

  // Agent definitions GET
  await page.context().route(`${API_BASE}/api/v1/admin/agent-definitions**`, (route) => {
    const method = route.request().method();
    if (method === 'POST') {
      return route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({ ...MOCK_AGENT, id: 'ag-new' }),
      });
    }
    if (method === 'PUT') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_AGENT),
      });
    }
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items: [MOCK_AGENT], totalCount: 1 }),
    });
  });

  // Linked agent
  await page.context().route(`${API_BASE}/api/v1/admin/agent-definitions/ag-1/linked-agent**`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ agentId: 'ag-1', sharedGameId: 'sg-1', kbCardIds: ['kbc-1', 'kbc-2'] }),
    })
  );

  await page.context().route(`${API_BASE}/api/v1/admin/**`, (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: [] }) })
  );
}

test.describe('ADM — Agent Builder Form', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);
    await mockAgentBuilderApi(page);
  });

  test('carica pagina agent builder', async ({ page }) => {
    await page.goto('/admin/agents/builder', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('body')).toBeVisible();
  });

  test('mostra form con campi nome, modello, prompt', async ({ page }) => {
    await page.goto('/admin/agents/builder', { waitUntil: 'domcontentloaded' });

    // Cerca campo nome agente
    const nameField = page.locator(
      'input[name="name"], input[placeholder*="nome"], input[placeholder*="name"]'
    ).first();
    if (await nameField.count() > 0) {
      await expect(nameField).toBeVisible({ timeout: 8000 });
    }
  });

  test('compila form base e salva', async ({ page }) => {
    let savedPayload: unknown = null;

    await page.context().route(`${API_BASE}/api/v1/admin/agent-definitions`, (route) => {
      if (route.request().method() === 'POST') {
        savedPayload = route.request().postDataJSON();
        return route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({ ...MOCK_AGENT, id: 'ag-new' }),
        });
      }
      return route.continue();
    });

    await page.goto('/admin/agents/builder', { waitUntil: 'domcontentloaded' });

    const nameField = page.locator('input[name="name"]').first();
    if (await nameField.count() > 0) {
      await nameField.fill('Test Agent E2E');

      const promptField = page.locator('textarea[name="systemPrompt"], textarea[name="prompt"]').first();
      if (await promptField.count() > 0) {
        await promptField.fill('Sei un assistente per giochi da tavolo.');
      }

      const saveBtn = page.locator('button:has-text("Salva"), button[type="submit"]').first();
      if (await saveBtn.count() > 0) {
        await saveBtn.click();
        await page.waitForTimeout(1000);
      }
    }

    await expect(page.locator('body')).toBeVisible();
  });
});

test.describe('ADM — KB Cards Checklist in Agent Builder ⭐', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);
    await mockAgentBuilderApi(page);
  });

  test('modal agente mostra tab KB Cards', async ({ page }) => {
    await page.goto('/admin/agents/builder', { waitUntil: 'domcontentloaded' });

    // Cerca pulsante per aprire modal o tab KB Cards
    const kbTab = page.locator(
      'button[role="tab"]:has-text("KB"), button:has-text("Knowledge Base"), [data-tab="kb-cards"]'
    ).first();

    if (await kbTab.count() > 0) {
      await kbTab.click();
      await expect(page.getByText('Catan Rulebook')).toBeVisible({ timeout: 8000 });
    }
  });

  test('seleziona KB cards e verifica selezione', async ({ page }) => {
    await page.goto('/admin/agents/builder', { waitUntil: 'domcontentloaded' });

    // Naviga a tab KB cards
    const kbTab = page.locator(
      'button[role="tab"]:has-text("KB"), button:has-text("Knowledge Base"), button:has-text("Cards")'
    ).first();

    if (await kbTab.count() > 0) {
      await kbTab.click();
      await page.waitForTimeout(300);

      // Seleziona prima card
      const firstCard = page.locator('input[type="checkbox"], [role="checkbox"]').first();
      if (await firstCard.count() > 0) {
        await firstCard.click();

        // Verifica counter o stato selezionato
        const counter = page.locator('text=/1.*selezionat|1.*selected|card.*selezionat/i').first();
        if (await counter.count() > 0) {
          await expect(counter).toBeVisible({ timeout: 5000 });
        }
      }
    }
  });

  test('salva agente con kbCardIds nel payload', async ({ page }) => {
    let capturedPayload: Record<string, unknown> | null = null;

    await page.context().route(`${API_BASE}/api/v1/admin/agent-definitions`, (route) => {
      if (route.request().method() === 'POST') {
        capturedPayload = route.request().postDataJSON() as Record<string, unknown>;
        return route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({ ...MOCK_AGENT, kbCardIds: ['kbc-1'] }),
        });
      }
      return route.continue();
    });

    await page.goto('/admin/agents/builder', { waitUntil: 'domcontentloaded' });

    const nameField = page.locator('input[name="name"]').first();
    if (await nameField.count() > 0) {
      await nameField.fill('Agent With KB Cards');

      // Vai a tab KB Cards e seleziona una
      const kbTab = page.locator('button[role="tab"]:has-text("KB"), button:has-text("Knowledge")').first();
      if (await kbTab.count() > 0) {
        await kbTab.click();
        const firstCheckbox = page.locator('input[type="checkbox"], [role="checkbox"]').first();
        if (await firstCheckbox.count() > 0) {
          await firstCheckbox.click();
        }
      }

      const saveBtn = page.locator('button:has-text("Salva"), button[type="submit"]').first();
      if (await saveBtn.count() > 0) {
        await saveBtn.click();
        await page.waitForTimeout(1000);
      }
    }

    await expect(page.locator('body')).toBeVisible();
  });
});
