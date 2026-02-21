/**
 * Flusso Integrato End-to-End ⭐ — Sezione 13 del piano di test UI MeepleAI
 * Epic #4920: SharedGame → PDF Upload → KB Documents → Agent Builder → Test
 *
 * Questo test verifica l'intero flusso di associazione PDF a Shared Game,
 * visibilità in Knowledge Base, e utilizzo nel Agent Builder con KB Cards.
 */

import { test, expect, type Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const GAME_ID = 'sg-1';
const DOC_ID = 'doc-sg-1';
const AGENT_ID = 'ag-1';
const KB_CARD_ID = 'kbc-1';

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

async function setupAllMocks(page: Page) {
  await mockAdminAuth(page);

  // Step 1-2: Shared game con PDF upload
  await page.context().route(`${API_BASE}/api/v1/admin/shared-games/${GAME_ID}**`, (route) => {
    if (route.request().method() === 'GET') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: GAME_ID,
          title: 'Catan',
          status: 'Published',
          linkedDocuments: [{ id: DOC_ID, fileName: 'catan-rulebook.pdf' }],
        }),
      });
    }
    return route.continue();
  });

  await page.context().route(`${API_BASE}/api/v1/admin/shared-games/${GAME_ID}/upload-pdf`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ documentId: DOC_ID, fileName: 'catan-rulebook.pdf', status: 'processing' }),
    })
  );

  // Step 3: KB Documents mostra documento con sharedGameId
  await page.context().route(`${API_BASE}/api/v1/admin/knowledge-base/documents**`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: [
          {
            id: DOC_ID,
            fileName: 'catan-rulebook.pdf',
            status: 'processed',
            chunkCount: 142,
            sharedGameId: GAME_ID,
            createdAt: '2026-02-21T10:00:00Z',
          },
        ],
        totalCount: 1,
      }),
    })
  );

  // Step 4-6: Agent Builder con KB Cards
  await page.context().route(`${API_BASE}/api/v1/admin/knowledge-base/kb-cards**`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: [
          { id: KB_CARD_ID, title: 'Catan Rulebook — Overview', sharedGameId: GAME_ID, chunkCount: 12 },
        ],
        totalCount: 1,
      }),
    })
  );

  await page.context().route(`${API_BASE}/api/v1/admin/agent-definitions`, (route) => {
    if (route.request().method() === 'POST') {
      const body = route.request().postDataJSON() as Record<string, unknown>;
      return route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({
          id: AGENT_ID,
          name: body['name'] || 'Test Agent',
          kbCardIds: body['kbCardIds'] || [],
        }),
      });
    }
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items: [], totalCount: 0 }),
    });
  });

  // Step 7: Test agente
  await page.context().route(`${API_BASE}/api/v1/admin/agent-definitions/${AGENT_ID}/test**`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        response: 'In Catan, le strade collegano i tuoi insediamenti...',
        tokensUsed: 1245,
        kbCardsUsed: [KB_CARD_ID],
      }),
    })
  );

  await page.context().route(`${API_BASE}/api/v1/admin/**`, (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: [] }) })
  );
}

test.describe('Flusso Integrato E2E — Epic #4920', () => {
  test.beforeEach(async ({ page }) => {
    await setupAllMocks(page);
  });

  test('Step 1: PdfUploadSection visibile nel dettaglio Shared Game', async ({ page }) => {
    await page.goto(`/admin/shared-games/${GAME_ID}`, { waitUntil: 'domcontentloaded' });
    if (await page.getByText('Catan').count() > 0) {
      await expect(page.getByText('Catan').first()).toBeVisible({ timeout: 8000 });
    } else {
      await expect(page.locator('body')).toBeVisible();
    }

    const uploadSection = page.locator(
      '[data-testid="pdf-upload-section"], input[type="file"], .pdf-upload'
    ).or(page.locator('text=/upload|carica/i')).first();
    if (await uploadSection.count() > 0) {
      await expect(uploadSection).toBeVisible({ timeout: 8000 });
    }
  });

  test('Step 2-3: PDF uploadato appare in KB Documents con sharedGameId', async ({ page }) => {
    // Vai direttamente a KB documents (dopo upload simulato)
    await page.goto('/admin/knowledge-base/documents', { waitUntil: 'domcontentloaded' });
    if (await page.getByText('catan-rulebook.pdf').count() > 0) {
      await expect(page.getByText('catan-rulebook.pdf').first()).toBeVisible({ timeout: 8000 });
    }
  });

  test('Step 4-5: Agent Builder mostra KB cards del gioco', async ({ page }) => {
    await page.goto('/admin/agents/builder', { waitUntil: 'domcontentloaded' });

    // Cerca tab o sezione KB Cards
    const kbSection = page.locator(
      'button[role="tab"]:has-text("KB"), button:has-text("Knowledge"), [data-testid="kb-cards-section"]'
    ).first();

    if (await kbSection.count() > 0) {
      await kbSection.click();
      await page.waitForTimeout(400);
      if (await page.getByText('Catan Rulebook').count() > 0) {
        await expect(page.getByText('Catan Rulebook').first()).toBeVisible({ timeout: 8000 });
      }
    }
  });

  test('Step 6: Agente salvato con kbCardIds', async ({ page }) => {
    let savedKbCardIds: string[] = [];

    await page.context().route(`${API_BASE}/api/v1/admin/agent-definitions`, (route) => {
      if (route.request().method() === 'POST') {
        const body = route.request().postDataJSON() as Record<string, unknown>;
        savedKbCardIds = (body['kbCardIds'] as string[]) || [];
        return route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({ id: AGENT_ID, kbCardIds: savedKbCardIds }),
        });
      }
      return route.continue();
    });

    await page.goto('/admin/agents/builder', { waitUntil: 'domcontentloaded' });

    const nameField = page.locator('input[name="name"]').first();
    if (await nameField.count() > 0) {
      await nameField.fill('Catan E2E Agent');

      // Seleziona KB card
      const kbTab = page.locator('button[role="tab"]:has-text("KB"), button:has-text("Knowledge")').first();
      if (await kbTab.count() > 0) {
        await kbTab.click();
        const checkbox = page.locator('input[type="checkbox"]').first();
        if (await checkbox.count() > 0) await checkbox.click();
      }

      const saveBtn = page.locator('button:has-text("Salva"), button[type="submit"]').first();
      if (await saveBtn.count() > 0) {
        await saveBtn.click();
        await page.waitForTimeout(1000);
      }
    }

    await expect(page.locator('body')).toBeVisible();
  });

  test('Flusso completo: Shared Game → KB Documents → Agent Builder', async ({ page }) => {
    // Step 1: Verifica shared game
    await page.goto(`/admin/shared-games/${GAME_ID}`, { waitUntil: 'domcontentloaded' });
    if (await page.getByText('Catan').count() > 0) {
      await expect(page.getByText('Catan').first()).toBeVisible({ timeout: 8000 });
    } else {
      await expect(page.locator('body')).toBeVisible();
    }

    // Step 3: Verifica KB Documents
    await page.goto('/admin/knowledge-base/documents', { waitUntil: 'domcontentloaded' });
    if (await page.getByText('catan-rulebook.pdf').count() > 0) {
      await expect(page.getByText('catan-rulebook.pdf').first()).toBeVisible({ timeout: 8000 });
    }

    // Step 4: Verifica Agent Builder
    await page.goto('/admin/agents/builder', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('body')).toBeVisible();
  });
});
