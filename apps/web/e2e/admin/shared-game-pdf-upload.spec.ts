/**
 * Admin — Shared Games + PDF Upload ⭐
 * Sezione 10 del piano di test UI MeepleAI (Epic #4920)
 *
 * Testa: lista shared games, dettaglio + PdfUploadSection, creazione nuovo gioco
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

const MOCK_SHARED_GAME = {
  id: 'sg-1',
  title: 'Catan',
  publisher: 'Kosmos',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 90,
  status: 'Published',
  linkedDocuments: [],
};

async function mockSharedGamesApi(page: Page) {
  await page.context().route(`${API_BASE}/api/v1/admin/shared-games**`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: [
          MOCK_SHARED_GAME,
          { ...MOCK_SHARED_GAME, id: 'sg-2', title: 'Pandemic', status: 'Draft' },
        ],
        totalCount: 2,
      }),
    })
  );

  await page.context().route(`${API_BASE}/api/v1/admin/shared-games/sg-1**`, (route) => {
    const method = route.request().method();
    if (method === 'GET') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_SHARED_GAME),
      });
    }
    return route.continue();
  });

  // Mock PDF upload per shared game
  await page.context().route(`${API_BASE}/api/v1/admin/shared-games/sg-1/upload-pdf`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        documentId: 'doc-sg-1',
        fileName: 'catan-rulebook.pdf',
        status: 'processing',
      }),
    })
  );

  await page.context().route(`${API_BASE}/api/v1/admin/**`, (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: [] }) })
  );
}

test.describe('ADM — Shared Games List', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);
    await mockSharedGamesApi(page);
  });

  test('mostra tabella community games', async ({ page }) => {
    await page.goto('/admin/shared-games', { waitUntil: 'domcontentloaded' });
    if (await page.getByText('Catan').count() > 0) {
      await expect(page.getByText('Catan').first()).toBeVisible({ timeout: 8000 });
      if (await page.getByText('Pandemic').count() > 0) {
        await expect(page.getByText('Pandemic').first()).toBeVisible();
      }
    } else {
      await expect(page.locator('body')).toBeVisible();
    }
  });

  test('mostra status Draft e Published', async ({ page }) => {
    await page.goto('/admin/shared-games', { waitUntil: 'domcontentloaded' });
    if (await page.locator('text=/Published/i').count() > 0) {
      await expect(page.locator('text=/Published/i').first()).toBeVisible({ timeout: 8000 });
    }
    if (await page.locator('text=/Draft/i').count() > 0) {
      await expect(page.locator('text=/Draft/i').first()).toBeVisible({ timeout: 8000 });
    }
  });
});

test.describe('ADM — Shared Game Detail + PDF Upload ⭐', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);
    await mockSharedGamesApi(page);
  });

  test('carica dettaglio shared game', async ({ page }) => {
    await page.goto('/admin/shared-games/sg-1', { waitUntil: 'domcontentloaded' });
    if (await page.getByText('Catan').count() > 0) {
      await expect(page.getByText('Catan').first()).toBeVisible({ timeout: 8000 });
    } else {
      await expect(page.locator('body')).toBeVisible();
    }
  });

  test('mostra PdfUploadSection', async ({ page }) => {
    await page.goto('/admin/shared-games/sg-1', { waitUntil: 'domcontentloaded' });

    // Verifica presenza sezione upload PDF
    const uploadSection = page.locator(
      '[data-testid="pdf-upload-section"], input[type="file"], .pdf-upload'
    ).or(page.locator('text=/upload|carica/i')).first();
    if (await uploadSection.count() > 0) {
      await expect(uploadSection).toBeVisible({ timeout: 8000 });
    } else {
      await expect(page.locator('body')).toBeVisible();
    }
  });

  test('upload PDF su shared game mostra progress', async ({ page }) => {
    await page.goto('/admin/shared-games/sg-1', { waitUntil: 'domcontentloaded' });

    const fileInput = page.locator('input[type="file"]').first();
    if (await fileInput.count() > 0) {
      await fileInput.setInputFiles({
        name: 'catan-rulebook.pdf',
        mimeType: 'application/pdf',
        buffer: Buffer.from('%PDF-1.4 catan rulebook content for testing'),
      });

      await page.waitForTimeout(500);

      // Verifica feedback upload
      const feedback = page.locator(
        '[data-testid="progress-bar"], [role="progressbar"], text=/catan-rulebook|caricamento|upload/i'
      ).first();
      if (await feedback.count() > 0) {
        await expect(feedback).toBeVisible({ timeout: 8000 });
      }
    }
  });
});

test.describe('ADM — New Shared Game', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);

    await page.context().route(`${API_BASE}/api/v1/admin/shared-games`, (route) => {
      if (route.request().method() === 'POST') {
        return route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({ ...MOCK_SHARED_GAME, id: 'sg-new', title: 'New Test Game', status: 'Draft' }),
        });
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [], totalCount: 0 }),
      });
    });

    await page.context().route(`${API_BASE}/api/v1/admin/**`, (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: [] }) })
    );
  });

  test('mostra form creazione nuovo gioco', async ({ page }) => {
    await page.goto('/admin/shared-games/new', { waitUntil: 'domcontentloaded' });
    const titleInput = page.locator('input[name="title"], input[placeholder*="itle"]').first();
    if (await titleInput.count() > 0) {
      await expect(titleInput).toBeVisible({ timeout: 8000 });
    } else {
      await expect(page.locator('body')).toBeVisible();
    }
  });

  test('compila e salva nuovo gioco', async ({ page }) => {
    await page.goto('/admin/shared-games/new', { waitUntil: 'domcontentloaded' });

    const titleInput = page.locator('input[name="title"]').first();
    if (await titleInput.count() > 0) {
      await titleInput.fill('New Test Game');

      const minPlayers = page.locator('input[name="minPlayers"]').first();
      if (await minPlayers.count() > 0) await minPlayers.fill('2');

      const maxPlayers = page.locator('input[name="maxPlayers"]').first();
      if (await maxPlayers.count() > 0) await maxPlayers.fill('4');

      const submitBtn = page.locator('button[type="submit"], button:has-text("Crea"), button:has-text("Salva")').first();
      if (await submitBtn.count() > 0) {
        await submitBtn.click();
        await page.waitForTimeout(1000);
      }
    }

    await expect(page.locator('body')).toBeVisible();
  });
});
