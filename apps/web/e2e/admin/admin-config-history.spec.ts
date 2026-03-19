/**
 * Admin Config History & Rollback — View timeline, rollback, creation-only
 *
 * Tests the ConfigHistoryDialog on /admin/config?tab=flags.
 * Uses page.context().route() for API mocking (no real backend needed).
 */

import { test, expect, type Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ---------------------------------------------------------------------------
// IDs (same as feature-flags tests)
// ---------------------------------------------------------------------------

const PDF_UPLOAD_ID = '11111111-1111-1111-1111-111111111111';
const GAME_NIGHT_ID = '22222222-2222-2222-2222-222222222222';
const RAG_CACHING_ID = '33333333-3333-3333-3333-333333333333';
const ADMIN_USER_ID = '00000000-0000-0000-0000-000000000001';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function mockAdminAuth(page: Page) {
  await page.context().route(`${API_BASE}/api/v1/auth/me`, route =>
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

function buildMockFlags() {
  const now = new Date().toISOString();
  return [
    {
      id: PDF_UPLOAD_ID,
      key: 'Features:PdfUpload',
      value: 'true',
      valueType: 'bool',
      description: 'Allow PDF uploads',
      category: 'FeatureFlag',
      isActive: true,
      requiresRestart: false,
      environment: 'Development',
      version: 1,
      previousValue: null,
      createdAt: now,
      updatedAt: now,
      createdByUserId: 'admin-1',
      updatedByUserId: null,
      lastToggledAt: null,
      tierFree: true,
      tierNormal: true,
      tierPremium: true,
    },
    {
      id: GAME_NIGHT_ID,
      key: 'Features:GameNightV2',
      value: 'true',
      valueType: 'bool',
      description: 'Game Night V2 experience',
      category: 'FeatureFlag',
      isActive: true,
      requiresRestart: false,
      environment: 'Development',
      version: 1,
      previousValue: null,
      createdAt: now,
      updatedAt: now,
      createdByUserId: 'admin-1',
      updatedByUserId: null,
      lastToggledAt: null,
      tierFree: false,
      tierNormal: true,
      tierPremium: true,
    },
    {
      id: RAG_CACHING_ID,
      key: 'Features:RagCaching',
      value: 'true',
      valueType: 'bool',
      description: 'RAG response caching',
      category: 'FeatureFlag',
      isActive: true,
      requiresRestart: true,
      environment: 'Development',
      version: 2,
      previousValue: 'false',
      createdAt: now,
      updatedAt: now,
      createdByUserId: 'admin-1',
      updatedByUserId: 'admin-1',
      lastToggledAt: now,
      tierFree: true,
      tierNormal: true,
      tierPremium: true,
    },
  ];
}

async function mockConfigurationsEndpoint(page: Page) {
  await page.context().route(`${API_BASE}/api/v1/admin/configurations**`, route => {
    if (route.request().method() === 'GET') {
      const flags = buildMockFlags();
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: flags,
          total: flags.length,
          page: 1,
          pageSize: 100,
        }),
      });
    }
    return route.continue();
  });
}

async function mockCatchAllAdmin(page: Page) {
  await page.context().route(`${API_BASE}/api/v1/admin/**`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ data: [] }),
    })
  );
}

// ---------------------------------------------------------------------------
// History mock data builders
// ---------------------------------------------------------------------------

function buildPdfUploadHistory() {
  const now = new Date();
  const earlier = new Date(now.getTime() - 3_600_000);
  return [
    {
      id: 'aaaa1111-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      configurationId: PDF_UPLOAD_ID,
      key: 'Features:PdfUpload',
      oldValue: 'false',
      newValue: 'true',
      version: 2,
      changedAt: now.toISOString(),
      changedByUserId: ADMIN_USER_ID,
      changeReason: 'Configuration updated',
    },
    {
      id: 'aaaa2222-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      configurationId: PDF_UPLOAD_ID,
      key: 'Features:PdfUpload',
      oldValue: '',
      newValue: 'false',
      version: 1,
      changedAt: earlier.toISOString(),
      changedByUserId: ADMIN_USER_ID,
      changeReason: 'Configuration created',
    },
  ];
}

function buildGameNightHistory() {
  const now = new Date();
  return [
    {
      id: 'bbbb1111-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      configurationId: GAME_NIGHT_ID,
      key: 'Features:GameNightV2',
      oldValue: '',
      newValue: 'true',
      version: 1,
      changedAt: now.toISOString(),
      changedByUserId: ADMIN_USER_ID,
      changeReason: 'Configuration created',
    },
  ];
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

test.describe('Config History & Rollback — Admin Config Tab', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);
    await mockConfigurationsEndpoint(page);
    await mockCatchAllAdmin(page);
  });

  test('CH-01: View configuration history', async ({ page }) => {
    // Mock history endpoint for PdfUpload
    await page
      .context()
      .route(`${API_BASE}/api/v1/admin/configurations/${PDF_UPLOAD_ID}/history**`, route =>
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(buildPdfUploadHistory()),
        })
      );

    await page.goto('/admin/config?tab=flags', { waitUntil: 'domcontentloaded' });

    // Wait for the flags table to render
    const pdfRow = page.locator('tr', { hasText: 'PdfUpload' });
    await expect(pdfRow).toBeVisible({ timeout: 8000 });

    // Click the History button for PdfUpload
    const historyBtn = page.getByTestId(`btn-history-${PDF_UPLOAD_ID}`);
    await expect(historyBtn).toBeVisible({ timeout: 5000 });
    await historyBtn.click();

    // Assert dialog is visible
    const dialog = page.getByTestId('config-history-dialog');
    await expect(dialog).toBeVisible({ timeout: 5000 });

    // Assert title contains the display key (stripped of "Features:" prefix)
    await expect(dialog.getByText('Cronologia: PdfUpload')).toBeVisible();

    // Assert history entries are visible
    const entryV2 = page.getByTestId('history-entry-2');
    await expect(entryV2).toBeVisible();
    await expect(entryV2).toContainText('false');
    await expect(entryV2).toContainText('true');

    const entryV1 = page.getByTestId('history-entry-1');
    await expect(entryV1).toBeVisible();
    await expect(entryV1).toContainText('Configuration created');

    // Assert rollback button is visible (v2 has oldValue)
    const rollbackBtn = page.getByTestId('btn-rollback');
    await expect(rollbackBtn).toBeVisible();
  });

  test('CH-02: Rollback to previous value', async ({ page }) => {
    // Mock history endpoint for PdfUpload
    await page
      .context()
      .route(`${API_BASE}/api/v1/admin/configurations/${PDF_UPLOAD_ID}/history**`, route =>
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(buildPdfUploadHistory()),
        })
      );

    // Mock rollback endpoint
    let rollbackCalled = false;
    await page
      .context()
      .route(`${API_BASE}/api/v1/admin/configurations/${PDF_UPLOAD_ID}/rollback/2`, async route => {
        if (route.request().method() === 'POST') {
          rollbackCalled = true;
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              ...buildMockFlags()[0],
              value: 'false',
              version: 3,
            }),
          });
        }
        return route.continue();
      });

    // Register dialog handler BEFORE navigation — accept the confirm
    let confirmMessage = '';
    let confirmAppeared = false;

    page.on('dialog', async dialog => {
      confirmMessage = dialog.message();
      confirmAppeared = true;
      await dialog.accept();
    });

    await page.goto('/admin/config?tab=flags', { waitUntil: 'domcontentloaded' });

    // Wait for flags table, click History button
    const pdfRow = page.locator('tr', { hasText: 'PdfUpload' });
    await expect(pdfRow).toBeVisible({ timeout: 8000 });

    const historyBtn = page.getByTestId(`btn-history-${PDF_UPLOAD_ID}`);
    await expect(historyBtn).toBeVisible({ timeout: 5000 });
    await historyBtn.click();

    // Wait for dialog to open
    const dialog = page.getByTestId('config-history-dialog');
    await expect(dialog).toBeVisible({ timeout: 5000 });

    // Click rollback button
    const rollbackBtn = page.getByTestId('btn-rollback');
    await expect(rollbackBtn).toBeVisible({ timeout: 5000 });
    await rollbackBtn.click();

    // Assert confirm dialog appeared with expected message
    await expect.poll(() => confirmAppeared, { timeout: 5000 }).toBe(true);
    expect(confirmMessage).toContain('Ripristinare');
    expect(confirmMessage).toContain('false');

    // Assert rollback POST was called
    await expect.poll(() => rollbackCalled, { timeout: 5000 }).toBe(true);

    // Assert success toast appears
    await expect(page.getByText('Configurazione ripristinata')).toBeVisible({ timeout: 5000 });

    // Assert dialog is closed after rollback
    await expect(dialog).not.toBeVisible({ timeout: 5000 });
  });

  test('CH-03: No rollback for creation-only config', async ({ page }) => {
    // Mock history endpoint for GameNight — only 1 creation entry
    await page
      .context()
      .route(`${API_BASE}/api/v1/admin/configurations/${GAME_NIGHT_ID}/history**`, route =>
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(buildGameNightHistory()),
        })
      );

    await page.goto('/admin/config?tab=flags', { waitUntil: 'domcontentloaded' });

    // Wait for the flags table
    const gameNightRow = page.locator('tr', { hasText: 'GameNightV2' });
    await expect(gameNightRow).toBeVisible({ timeout: 8000 });

    // Click the History button for GameNight
    const historyBtn = page.getByTestId(`btn-history-${GAME_NIGHT_ID}`);
    await expect(historyBtn).toBeVisible({ timeout: 5000 });
    await historyBtn.click();

    // Assert dialog opens
    const dialog = page.getByTestId('config-history-dialog');
    await expect(dialog).toBeVisible({ timeout: 5000 });

    // Assert the single creation entry is visible
    const entryV1 = page.getByTestId('history-entry-1');
    await expect(entryV1).toBeVisible();

    // Assert rollback button is NOT visible (no previous value to rollback to)
    const rollbackBtn = page.getByTestId('btn-rollback');
    await expect(rollbackBtn).not.toBeVisible();
  });
});
