/**
 * Config Import/Export E2E Tests - Issue #2193
 *
 * Tests the system configuration import/export flow:
 * - Export current configuration
 * - Download configuration file
 * - Import configuration from file
 * - Validate import before applying
 *
 * @see apps/api/src/Api/BoundedContexts/SystemConfiguration/Application/
 * @see docs/02-development/testing/test-coverage-gaps.md
 */

import { test, expect } from './fixtures/chromatic';
import { AuthHelper, AdminHelper, USER_FIXTURES } from './pages';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Mock configuration data
const MOCK_CONFIGS = [
  {
    id: 'config-1',
    key: 'Features:TestFlag',
    value: 'true',
    valueType: 'bool',
    category: 'FeatureFlags',
    isActive: true,
    description: 'Test feature flag',
    environment: 'development',
    createdAt: '2025-01-01T10:00:00Z',
    updatedAt: '2025-01-05T10:00:00Z',
  },
  {
    id: 'config-2',
    key: 'RateLimiting:RequestsPerMinute',
    value: '100',
    valueType: 'int',
    category: 'RateLimiting',
    isActive: true,
    description: 'Maximum requests per minute',
    environment: 'development',
    createdAt: '2025-01-02T10:00:00Z',
    updatedAt: null,
  },
  {
    id: 'config-3',
    key: 'AI:DefaultModel',
    value: 'gpt-4-turbo',
    valueType: 'string',
    category: 'AI',
    isActive: true,
    description: 'Default AI model',
    environment: 'development',
    createdAt: '2025-01-03T10:00:00Z',
    updatedAt: null,
  },
];

const MOCK_EXPORT_DATA = {
  exportedAt: '2025-01-10T10:00:00Z',
  environment: 'development',
  version: '1.0.0',
  configurations: MOCK_CONFIGS,
};

/**
 * SKIPPED: Feature not implemented
 *
 * The configuration page (/admin/configuration) currently only has:
 * - Reload button (🔄 Reload)
 * - Clear Cache button (🗑️ Clear Cache)
 *
 * Export/Import functionality is not yet available in the UI.
 * These tests are preserved for when the feature is implemented.
 *
 * @see apps/web/src/app/admin/configuration/client.tsx
 */
test.describe.skip('Config Import/Export Flow - Issue #2193', () => {
  test.beforeEach(async ({ page }) => {
    const adminHelper = new AdminHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Setup admin auth
    await adminHelper.setupAdminAuth(true);

    // Mock configurations list endpoint
    await page.route(`${API_BASE}/api/v1/admin/configurations*`, async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: MOCK_CONFIGS,
            total: MOCK_CONFIGS.length,
            page: 1,
            pageSize: 100,
          }),
        });
      } else {
        await route.continue();
      }
    });

    // Mock categories endpoint
    await page.route(`${API_BASE}/api/v1/admin/configurations/categories`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(['FeatureFlags', 'RateLimiting', 'AI', 'RAG']),
      });
    });

    // Mock export endpoint
    await page.route(`${API_BASE}/api/v1/admin/configurations/export*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_EXPORT_DATA),
      });
    });

    // Mock import endpoint
    await page.route(`${API_BASE}/api/v1/admin/configurations/import`, async route => {
      const body = route.request().postDataJSON() as {
        configurations: Array<{ key: string; value: string }>;
        overwriteExisting: boolean;
      };

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          importedCount: body.configurations?.length || 0,
          message: 'Configurations imported successfully',
        }),
      });
    });
  });

  test('should export configuration to JSON', async ({ page }) => {
    await page.goto('/admin/configuration');
    await page.waitForLoadState('networkidle');

    // Look for export button
    const exportButton = page
      .getByRole('button', { name: /Export|Esporta/i })
      .or(page.locator('[data-testid="export-config-button"]'));

    await expect(exportButton).toBeVisible({ timeout: 10000 });
    await exportButton.click();

    // Verify export dialog or download initiated
    // Option 1: Dialog shown with export options
    const exportDialog = page.locator('[role="dialog"]').filter({ hasText: /export/i });
    if (await exportDialog.isVisible({ timeout: 2000 }).catch(() => false)) {
      // Select environment if needed
      const envSelect = exportDialog.locator('select[name="environment"]');
      if (await envSelect.isVisible()) {
        await envSelect.selectOption('development');
      }

      // Confirm export
      const confirmButton = exportDialog.getByRole('button', { name: /Export|Conferma|Download/i });
      await confirmButton.click();
    }

    // Verify success message or download
    await expect(
      page.locator('text=exported|esportato|download|success', { exact: false })
    ).toBeVisible({ timeout: 5000 });
  });

  test('should download configuration file', async ({ page }) => {
    await page.goto('/admin/configuration');
    await page.waitForLoadState('networkidle');

    // Setup download listener
    const downloadPromise = page.waitForEvent('download', { timeout: 10000 }).catch(() => null);

    // Click export button
    const exportButton = page
      .getByRole('button', { name: /Export|Esporta|Download/i })
      .or(page.locator('[data-testid="export-config-button"]'));
    await exportButton.click();

    // Handle any confirmation dialog
    const confirmButton = page.getByRole('button', { name: /Download|Scarica|Confirm/i });
    if (await confirmButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await confirmButton.click();
    }

    // Verify download was triggered (if UI supports it)
    const download = await downloadPromise;
    if (download) {
      expect(download.suggestedFilename()).toMatch(/config.*\.json$/i);
    }
  });

  test('should import configuration from file upload', async ({ page }) => {
    await page.goto('/admin/configuration');
    await page.waitForLoadState('networkidle');

    // Click import button
    const importButton = page
      .getByRole('button', { name: /Import|Importa/i })
      .or(page.locator('[data-testid="import-config-button"]'));
    await importButton.click();

    // Check for file input or text area
    const fileInput = page.locator('input[type="file"]');
    const jsonInput = page.locator(
      'textarea[name="configJson"], [data-testid="import-json-input"]'
    );

    if (await fileInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      // Create a mock file
      const configContent = JSON.stringify({
        configurations: [
          {
            key: 'NewConfig:Test',
            value: 'test-value',
            valueType: 'string',
            category: 'FeatureFlags',
          },
        ],
        overwriteExisting: false,
      });

      // Note: File upload in Playwright
      await fileInput.setInputFiles({
        name: 'config.json',
        mimeType: 'application/json',
        buffer: Buffer.from(configContent),
      });
    } else if (await jsonInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      // Paste JSON directly
      await jsonInput.fill(
        JSON.stringify({
          configurations: [
            {
              key: 'NewConfig:Test',
              value: 'test-value',
              valueType: 'string',
              category: 'FeatureFlags',
            },
          ],
        })
      );
    }

    // Click import/confirm button
    const confirmImport = page
      .getByRole('button', { name: /Import|Importa|Confirm|Conferma/i })
      .last();
    await confirmImport.click();

    // Verify success
    await expect(
      page.locator('text=imported|importato|success|successo', { exact: false })
    ).toBeVisible({ timeout: 10000 });
  });

  test('should validate configuration before import', async ({ page }) => {
    // Override import to return validation errors
    await page.route(`${API_BASE}/api/v1/admin/configurations/import`, async route => {
      await route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({
          error: 'Validation failed',
          errors: ['Invalid key format: "BadKey"', 'Missing required field: valueType'],
        }),
      });
    });

    await page.goto('/admin/configuration');
    await page.waitForLoadState('networkidle');

    // Click import button
    const importButton = page
      .getByRole('button', { name: /Import|Importa/i })
      .or(page.locator('[data-testid="import-config-button"]'));
    await importButton.click();

    // Enter invalid config
    const jsonInput = page.locator(
      'textarea[name="configJson"], [data-testid="import-json-input"]'
    );
    if (await jsonInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      await jsonInput.fill(
        JSON.stringify({
          configurations: [{ key: 'BadKey' }], // Missing required fields
        })
      );

      const confirmImport = page.getByRole('button', { name: /Import|Importa|Confirm/i }).last();
      await confirmImport.click();

      // Verify validation errors are shown
      await expect(
        page.locator('text=error|errore|invalid|validation', { exact: false })
      ).toBeVisible({ timeout: 5000 });
    }
  });

  test('should show overwrite confirmation for existing configs', async ({ page }) => {
    await page.goto('/admin/configuration');
    await page.waitForLoadState('networkidle');

    // Click import
    const importButton = page
      .getByRole('button', { name: /Import|Importa/i })
      .or(page.locator('[data-testid="import-config-button"]'));
    await importButton.click();

    // Look for overwrite checkbox/toggle
    const overwriteCheckbox = page
      .locator('input[name="overwriteExisting"]')
      .or(page.locator('[data-testid="overwrite-checkbox"]'))
      .or(page.getByLabel(/overwrite|sovrascrivi/i));

    if (await overwriteCheckbox.isVisible({ timeout: 2000 }).catch(() => false)) {
      // Toggle overwrite option
      await overwriteCheckbox.check();

      // Verify warning message appears
      await expect(
        page.locator('text=warning|attenzione|existing|esistenti|overwrite', { exact: false })
      ).toBeVisible({ timeout: 5000 });
    }
  });

  test('should filter export by environment', async ({ page }) => {
    await page.goto('/admin/configuration');
    await page.waitForLoadState('networkidle');

    // Click export
    const exportButton = page
      .getByRole('button', { name: /Export|Esporta/i })
      .or(page.locator('[data-testid="export-config-button"]'));
    await exportButton.click();

    // Look for environment selector
    const envSelect = page
      .locator('select[name="environment"]')
      .or(page.locator('[data-testid="export-environment-select"]'));

    if (await envSelect.isVisible({ timeout: 2000 }).catch(() => false)) {
      // Verify options are available
      const options = await envSelect.locator('option').allTextContents();
      expect(options.length).toBeGreaterThan(0);

      // Select specific environment
      await envSelect.selectOption('development');
    }
  });

  test('should handle export error gracefully', async ({ page }) => {
    // Override export to return error
    await page.route(`${API_BASE}/api/v1/admin/configurations/export*`, async route => {
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Export failed: Database connection error' }),
      });
    });

    await page.goto('/admin/configuration');
    await page.waitForLoadState('networkidle');

    const exportButton = page
      .getByRole('button', { name: /Export|Esporta/i })
      .or(page.locator('[data-testid="export-config-button"]'));
    await exportButton.click();

    // Verify error message
    await expect(page.locator('text=error|errore|failed|fallito', { exact: false })).toBeVisible({
      timeout: 5000,
    });
  });

  test('should require admin access for import/export', async ({ page }) => {
    // Clear admin auth and mock as regular user
    await page.context().clearCookies();
    const authHelper = new AuthHelper(page);
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // Override routes to return 403
    await page.route(`${API_BASE}/api/v1/admin/configurations/export*`, async route => {
      await route.fulfill({
        status: 403,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Admin access required' }),
      });
    });

    await page.route(`${API_BASE}/api/v1/admin/configurations/import`, async route => {
      await route.fulfill({
        status: 403,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Admin access required' }),
      });
    });

    await page.goto('/admin/configuration');

    // Should either redirect or show access denied
    const accessDenied = await page
      .locator('text=403|forbidden|denied|accesso negato', { exact: false })
      .isVisible({ timeout: 5000 })
      .catch(() => false);

    const redirected = page.url().includes('/login') || page.url().includes('/unauthorized');

    expect(accessDenied || redirected).toBeTruthy();
  });

  test('should show import progress for large configs', async ({ page }) => {
    await page.goto('/admin/configuration');
    await page.waitForLoadState('networkidle');

    // Click import
    const importButton = page
      .getByRole('button', { name: /Import|Importa/i })
      .or(page.locator('[data-testid="import-config-button"]'));
    await importButton.click();

    // Enter config with many items
    const jsonInput = page.locator(
      'textarea[name="configJson"], [data-testid="import-json-input"]'
    );
    if (await jsonInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      const manyConfigs = Array.from({ length: 50 }, (_, i) => ({
        key: `Config${i}:Key`,
        value: `value-${i}`,
        valueType: 'string',
        category: 'FeatureFlags',
      }));

      await jsonInput.fill(JSON.stringify({ configurations: manyConfigs }));

      const confirmImport = page.getByRole('button', { name: /Import|Importa|Confirm/i }).last();
      await confirmImport.click();

      // Verify progress or loading indicator
      // This depends on UI implementation
      await expect(
        page.locator('text=importing|importando|progress|caricamento|success', { exact: false })
      ).toBeVisible({ timeout: 10000 });
    }
  });

  test('should display export preview before download', async ({ page }) => {
    await page.goto('/admin/configuration');
    await page.waitForLoadState('networkidle');

    const exportButton = page
      .getByRole('button', { name: /Export|Esporta/i })
      .or(page.locator('[data-testid="export-config-button"]'));
    await exportButton.click();

    // Check if preview is shown
    const previewSection = page.locator('[data-testid="export-preview"], .export-preview');
    if (await previewSection.isVisible({ timeout: 2000 }).catch(() => false)) {
      // Verify config count is shown
      await expect(previewSection.getByText(/3|configurations|configurazioni/i)).toBeVisible();
    }
  });
});
