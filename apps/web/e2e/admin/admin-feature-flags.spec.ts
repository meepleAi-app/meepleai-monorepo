/**
 * Admin Feature Flags — Toggle, Tier Enable, Critical Confirmation
 *
 * Tests the feature flags tab on /admin/config?tab=flags.
 * Uses page.context().route() for API mocking (no real backend needed).
 */

import { test, expect, type Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

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

const PDF_UPLOAD_ID = '11111111-1111-1111-1111-111111111111';
const GAME_NIGHT_ID = '22222222-2222-2222-2222-222222222222';
const RAG_CACHING_ID = '33333333-3333-3333-3333-333333333333';

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
    // PUT handled per-test via more specific routes
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
// Tests
// ---------------------------------------------------------------------------

test.describe('Feature Flags — Admin Config Tab', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);
    await mockConfigurationsEndpoint(page);
    await mockCatchAllAdmin(page);
  });

  test('FF-01: Toggle global feature flag (active → inactive)', async ({ page }) => {
    // Intercept PUT for PdfUpload and capture request body
    let capturedBody: Record<string, unknown> | null = null;

    await page
      .context()
      .route(`${API_BASE}/api/v1/admin/configurations/${PDF_UPLOAD_ID}`, async route => {
        if (route.request().method() === 'PUT') {
          capturedBody = await route.request().postDataJSON();
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ ...buildMockFlags()[0], value: 'false' }),
          });
        }
        return route.continue();
      });

    await page.goto('/admin/config?tab=flags', { waitUntil: 'domcontentloaded' });

    // Verify "Enabled" badge is visible on PdfUpload row
    const pdfRow = page.locator('tr', { hasText: 'PdfUpload' });
    await expect(pdfRow.getByText('Enabled')).toBeVisible({ timeout: 8000 });

    // Click the global toggle for PdfUpload
    const globalToggle = page.locator('[aria-label="Toggle Features:PdfUpload"]');
    await expect(globalToggle).toBeVisible({ timeout: 5000 });
    await globalToggle.click();

    // Wait for the PUT request to complete and verify body
    await expect.poll(() => capturedBody, { timeout: 5000 }).toBeTruthy();
    expect(capturedBody).toMatchObject({ value: 'false' });

    // Verify toast appears with "disabled"
    await expect(page.getByText(/disabled/i)).toBeVisible({ timeout: 5000 });
  });

  test('FF-03: Enable feature for Free tier', async ({ page }) => {
    // Intercept POST to tier enable endpoint (glob to handle URL-encoded colon)
    let tierEnableCalled = false;

    await page
      .context()
      .route(`${API_BASE}/api/v1/admin/feature-flags/*/tier/free/enable`, async route => {
        if (route.request().method() === 'POST') {
          tierEnableCalled = true;
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ success: true }),
          });
        }
        return route.continue();
      });

    await page.goto('/admin/config?tab=flags', { waitUntil: 'domcontentloaded' });

    // Find the Free tier toggle for GameNightV2
    const freeTierToggle = page.locator('[aria-label="Toggle Free tier for Features:GameNightV2"]');
    await expect(freeTierToggle).toBeVisible({ timeout: 8000 });

    // Verify it starts unchecked
    await expect(freeTierToggle).toHaveAttribute('data-state', 'unchecked');

    // Click to enable
    await freeTierToggle.click();

    // Verify the POST was called
    await expect.poll(() => tierEnableCalled, { timeout: 5000 }).toBe(true);

    // Verify toast with "Free tier enabled" appears
    await expect(page.getByText(/Free tier enabled/i)).toBeVisible({ timeout: 5000 });
  });

  test('FF-06: Critical flag confirmation dialog — cancel prevents API call', async ({ page }) => {
    // Track whether PUT is called
    let putCalled = false;

    await page
      .context()
      .route(`${API_BASE}/api/v1/admin/configurations/${RAG_CACHING_ID}`, async route => {
        if (route.request().method() === 'PUT') {
          putCalled = true;
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ ...buildMockFlags()[2], value: 'false' }),
          });
        }
        return route.continue();
      });

    // Register dialog handler BEFORE navigation — dismiss (cancel) the dialog
    let dialogMessage = '';
    let dialogAppeared = false;

    page.on('dialog', async dialog => {
      dialogMessage = dialog.message();
      dialogAppeared = true;
      await dialog.dismiss(); // cancel
    });

    await page.goto('/admin/config?tab=flags', { waitUntil: 'domcontentloaded' });

    // Verify RagCaching row shows the "⚠️ Restart" badge
    const ragRow = page.locator('tr', { hasText: 'RagCaching' });
    await expect(ragRow).toBeVisible({ timeout: 8000 });
    await expect(ragRow.getByText('⚠️ Restart')).toBeVisible();

    // Click the global toggle for RagCaching (critical flag)
    const ragToggle = page.locator('[aria-label="Toggle Features:RagCaching"]');
    await expect(ragToggle).toBeVisible({ timeout: 5000 });
    await ragToggle.click();

    // Wait for the dialog event to fire
    await expect.poll(() => dialogAppeared, { timeout: 5000 }).toBe(true);
    expect(dialogMessage).toContain('RagCaching');
    expect(dialogMessage).toContain('Are you sure');

    // Assert the PUT API was NOT called because user cancelled
    expect(putCalled).toBe(false);
  });
});
