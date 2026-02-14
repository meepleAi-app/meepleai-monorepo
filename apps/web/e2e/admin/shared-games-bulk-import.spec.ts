/**
 * ISSUE #4176: Bulk Import JSON E2E Tests
 *
 * Tests the complete bulk import flow:
 * - Upload JSON → Preview → Import → Results
 * - Duplicate handling (within batch)
 * - Validation errors display
 * - API error handling
 * - Cancel / Reset flows
 *
 * Uses mock auth + API route interception (no real backend needed).
 *
 * Run: pnpm test:e2e apps/web/e2e/admin/shared-games-bulk-import.spec.ts
 */

import { test, expect, setupMockAuth } from '../fixtures/auth';

// ─── Test Data ──────────────────────────────────────────────────

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE ||
  process.env.NEXT_PUBLIC_API_BASE ||
  'http://localhost:8080';

const VALID_JSON = JSON.stringify([
  { bggId: 174430, name: 'Gloomhaven' },
  { bggId: 167791, name: 'Terraforming Mars' },
  { bggId: 169786, name: 'Scythe' },
]);

const DUPLICATE_JSON = JSON.stringify([
  { bggId: 174430, name: 'Gloomhaven' },
  { bggId: 167791, name: 'Terraforming Mars' },
  { bggId: 174430, name: 'Gloomhaven (duplicate)' },
]);

const INVALID_ENTRIES_JSON = JSON.stringify([
  { bggId: 174430, name: 'Gloomhaven' },
  { bggId: -1, name: 'Bad ID Game' },
  { bggId: 167791, name: '' },
]);

const FULL_SUCCESS_RESPONSE = {
  total: 3,
  enqueued: 3,
  skipped: 0,
  failed: 0,
  errors: [],
};

const PARTIAL_SUCCESS_RESPONSE = {
  total: 3,
  enqueued: 1,
  skipped: 1,
  failed: 1,
  errors: [
    {
      bggId: 167791,
      gameName: 'Terraforming Mars',
      reason: 'Already exists in catalog',
      errorType: 'Duplicate',
    },
    {
      bggId: 169786,
      gameName: 'Scythe',
      reason: 'BGG API timeout',
      errorType: 'ApiError',
    },
  ],
};

// ─── Helper: Setup bulk import API mock ─────────────────────────

async function setupBulkImportMock(
  page: import('@playwright/test').Page,
  response: object,
  statusCode = 200
) {
  // Register AFTER setupMockAuth so this takes precedence over admin/** catch-all
  await page.route(
    `${API_BASE}/api/v1/admin/games/bulk-import`,
    async (route) => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: statusCode,
          contentType: 'application/json',
          body: JSON.stringify(response),
        });
      } else {
        await route.continue();
      }
    }
  );
}

// ─── Tests ──────────────────────────────────────────────────────

test.describe('Bulk Import JSON - E2E', () => {
  test.beforeEach(async ({ page }) => {
    // Setup mock auth as Admin (skip navigation so we can add more mocks)
    await setupMockAuth(page, 'Admin');
  });

  // ─── Happy Path ─────────────────────────────────────────────

  test.describe('Happy Path: Upload → Preview → Import → Results', () => {
    test('should complete full import flow with all successes', async ({ page }) => {
      await setupBulkImportMock(page, FULL_SUCCESS_RESPONSE);
      await page.goto('/admin/games/import/bulk');

      // Wait for auth check + page load
      await expect(page.locator('[data-testid="bulk-import-uploader"]')).toBeVisible();

      // Step 1: Paste valid JSON
      await page.fill('[data-testid="json-textarea"]', VALID_JSON);

      // Verify entry count badge appears
      await expect(page.locator('text=3 entries')).toBeVisible();

      // Step 2: Click Preview
      await page.click('[data-testid="preview-button"]');

      // Step 3: Verify Preview
      await expect(page.locator('[data-testid="bulk-import-preview"]')).toBeVisible();
      await expect(page.locator('[data-testid="total-count"]')).toHaveText('3');
      await expect(page.locator('[data-testid="valid-count"]')).toHaveText('3');
      await expect(page.locator('[data-testid="duplicate-count"]')).toHaveText('0');
      await expect(page.locator('[data-testid="invalid-count"]')).toHaveText('0');

      // Verify "All entries are valid" alert
      await expect(page.locator('[data-testid="all-valid-alert"]')).toBeVisible();

      // Verify preview table shows entries
      await expect(page.locator('[data-testid="preview-table"]')).toBeVisible();
      await expect(page.locator('text=Gloomhaven')).toBeVisible();
      await expect(page.locator('text=Terraforming Mars')).toBeVisible();
      await expect(page.locator('text=Scythe')).toBeVisible();

      // Step 4: Confirm Import
      await page.click('[data-testid="confirm-import"]');

      // Step 5: Verify Results
      await expect(page.locator('[data-testid="bulk-import-results"]')).toBeVisible();
      await expect(page.locator('text=Import Complete')).toBeVisible();
      await expect(page.locator('[data-testid="result-stat-total"]')).toHaveText(/3/);
      await expect(page.locator('[data-testid="result-stat-enqueued"]')).toHaveText(/3/);
      await expect(page.locator('[data-testid="result-stat-skipped"]')).toHaveText(/0/);
      await expect(page.locator('[data-testid="result-stat-failed"]')).toHaveText(/0/);

      // Verify action buttons
      await expect(page.locator('[data-testid="download-csv-button"]')).toBeVisible();
      await expect(page.locator('[data-testid="new-import-button"]')).toBeVisible();

      // No errors section for full success
      await expect(page.locator('[data-testid="errors-section"]')).not.toBeVisible();
    });

    test('should reset to upload view when clicking Import Another Batch', async ({ page }) => {
      await setupBulkImportMock(page, FULL_SUCCESS_RESPONSE);
      await page.goto('/admin/games/import/bulk');
      await expect(page.locator('[data-testid="bulk-import-uploader"]')).toBeVisible();

      // Complete the flow
      await page.fill('[data-testid="json-textarea"]', VALID_JSON);
      await page.click('[data-testid="preview-button"]');
      await page.click('[data-testid="confirm-import"]');

      // Verify results
      await expect(page.locator('[data-testid="bulk-import-results"]')).toBeVisible();

      // Click "Import Another Batch"
      await page.click('[data-testid="new-import-button"]');

      // Should be back to upload view
      await expect(page.locator('[data-testid="json-textarea"]')).toBeVisible();
      await expect(page.locator('[data-testid="bulk-import-results"]')).not.toBeVisible();
    });
  });

  // ─── Partial Success & Errors ───────────────────────────────

  test.describe('Partial Success with Errors', () => {
    test('should display errors table for partial success', async ({ page }) => {
      await setupBulkImportMock(page, PARTIAL_SUCCESS_RESPONSE);
      await page.goto('/admin/games/import/bulk');
      await expect(page.locator('[data-testid="bulk-import-uploader"]')).toBeVisible();

      // Import
      await page.fill('[data-testid="json-textarea"]', VALID_JSON);
      await page.click('[data-testid="preview-button"]');
      await page.click('[data-testid="confirm-import"]');

      // Verify results with errors
      await expect(page.locator('[data-testid="bulk-import-results"]')).toBeVisible();
      await expect(page.locator('[data-testid="result-stat-total"]')).toHaveText(/3/);
      await expect(page.locator('[data-testid="result-stat-enqueued"]')).toHaveText(/1/);
      await expect(page.locator('[data-testid="result-stat-skipped"]')).toHaveText(/1/);
      await expect(page.locator('[data-testid="result-stat-failed"]')).toHaveText(/1/);

      // Verify errors section visible
      await expect(page.locator('[data-testid="errors-section"]')).toBeVisible();

      // Verify error details
      await expect(page.locator('text=Terraforming Mars')).toBeVisible();
      await expect(page.locator('text=Already exists in catalog')).toBeVisible();
      await expect(page.locator('text=Duplicate')).toBeVisible();
      await expect(page.locator('text=BGG API timeout')).toBeVisible();
      await expect(page.locator('text=ApiError')).toBeVisible();
    });
  });

  // ─── Duplicate Detection (Preview) ─────────────────────────

  test.describe('Duplicate Detection in Preview', () => {
    test('should flag duplicates within batch in preview', async ({ page }) => {
      await page.goto('/admin/games/import/bulk');
      await expect(page.locator('[data-testid="bulk-import-uploader"]')).toBeVisible();

      // Paste JSON with duplicates
      await page.fill('[data-testid="json-textarea"]', DUPLICATE_JSON);
      await page.click('[data-testid="preview-button"]');

      // Verify preview shows duplicates
      await expect(page.locator('[data-testid="bulk-import-preview"]')).toBeVisible();
      await expect(page.locator('[data-testid="total-count"]')).toHaveText('3');
      await expect(page.locator('[data-testid="valid-count"]')).toHaveText('2');
      await expect(page.locator('[data-testid="duplicate-count"]')).toHaveText('1');

      // Verify "Some entries have issues" alert
      await expect(page.locator('[data-testid="has-errors-alert"]')).toBeVisible();
      await expect(page.locator('text=1 duplicate')).toBeVisible();

      // Verify Duplicate badge in preview table
      await expect(page.locator('text=Duplicate').first()).toBeVisible();
    });
  });

  // ─── Validation Errors ──────────────────────────────────────

  test.describe('Validation Errors', () => {
    test('should show validation error for invalid JSON syntax', async ({ page }) => {
      await page.goto('/admin/games/import/bulk');
      await expect(page.locator('[data-testid="bulk-import-uploader"]')).toBeVisible();

      // Paste invalid JSON
      await page.fill('[data-testid="json-textarea"]', '{ invalid json !!!');
      await page.click('[data-testid="preview-button"]');

      // Verify validation error
      await expect(page.locator('[data-testid="validation-errors"]')).toBeVisible();
      await expect(page.locator('text=Invalid JSON format')).toBeVisible();
    });

    test('should show validation error for empty JSON array', async ({ page }) => {
      await page.goto('/admin/games/import/bulk');
      await expect(page.locator('[data-testid="bulk-import-uploader"]')).toBeVisible();

      await page.fill('[data-testid="json-textarea"]', '[]');
      await page.click('[data-testid="preview-button"]');

      await expect(page.locator('[data-testid="validation-errors"]')).toBeVisible();
      await expect(page.locator('text=JSON array is empty')).toBeVisible();
    });

    test('should show validation error for non-array JSON', async ({ page }) => {
      await page.goto('/admin/games/import/bulk');
      await expect(page.locator('[data-testid="bulk-import-uploader"]')).toBeVisible();

      await page.fill('[data-testid="json-textarea"]', '{"bggId": 1, "name": "test"}');
      await page.click('[data-testid="preview-button"]');

      await expect(page.locator('[data-testid="validation-errors"]')).toBeVisible();
      await expect(page.locator('text=JSON must be an array')).toBeVisible();
    });

    test('should flag invalid entries in preview', async ({ page }) => {
      await page.goto('/admin/games/import/bulk');
      await expect(page.locator('[data-testid="bulk-import-uploader"]')).toBeVisible();

      await page.fill('[data-testid="json-textarea"]', INVALID_ENTRIES_JSON);
      await page.click('[data-testid="preview-button"]');

      // Preview should show invalid entries
      await expect(page.locator('[data-testid="bulk-import-preview"]')).toBeVisible();
      await expect(page.locator('[data-testid="valid-count"]')).toHaveText('1');
      await expect(page.locator('[data-testid="invalid-count"]')).toHaveText('2');

      // Error details section should be visible
      await expect(page.locator('[data-testid="error-details"]')).toBeVisible();
    });
  });

  // ─── API Error Handling ─────────────────────────────────────

  test.describe('API Error Handling', () => {
    test('should display error alert when API returns 500', async ({ page }) => {
      await setupBulkImportMock(
        page,
        { error: 'Internal Server Error', message: 'Import service unavailable' },
        500
      );
      await page.goto('/admin/games/import/bulk');
      await expect(page.locator('[data-testid="bulk-import-uploader"]')).toBeVisible();

      // Complete flow to trigger API call
      await page.fill('[data-testid="json-textarea"]', VALID_JSON);
      await page.click('[data-testid="preview-button"]');
      await page.click('[data-testid="confirm-import"]');

      // Verify API error alert
      await expect(page.locator('[data-testid="api-error"]')).toBeVisible();
      await expect(page.locator('text=Import Failed')).toBeVisible();
    });
  });

  // ─── Reset / Clear Flow ─────────────────────────────────────

  test.describe('Reset and Navigation', () => {
    test('should clear textarea when clicking Clear button', async ({ page }) => {
      await page.goto('/admin/games/import/bulk');
      await expect(page.locator('[data-testid="bulk-import-uploader"]')).toBeVisible();

      // Paste content
      await page.fill('[data-testid="json-textarea"]', VALID_JSON);
      await expect(page.locator('text=3 entries')).toBeVisible();

      // Click Clear
      await page.click('[data-testid="reset-button"]');

      // Textarea should be empty
      const textareaValue = await page.locator('[data-testid="json-textarea"]').inputValue();
      expect(textareaValue).toBe('');
    });

    test('should go back to edit from preview', async ({ page }) => {
      await page.goto('/admin/games/import/bulk');
      await expect(page.locator('[data-testid="bulk-import-uploader"]')).toBeVisible();

      // Go to preview
      await page.fill('[data-testid="json-textarea"]', VALID_JSON);
      await page.click('[data-testid="preview-button"]');
      await expect(page.locator('[data-testid="bulk-import-preview"]')).toBeVisible();

      // Click "Back to Edit"
      await page.click('[data-testid="preview-back"]');

      // Should be back to upload
      await expect(page.locator('[data-testid="json-textarea"]')).toBeVisible();
      await expect(page.locator('[data-testid="bulk-import-preview"]')).not.toBeVisible();
    });

    test('should go back to edit via Cancel button in preview', async ({ page }) => {
      await page.goto('/admin/games/import/bulk');
      await expect(page.locator('[data-testid="bulk-import-uploader"]')).toBeVisible();

      await page.fill('[data-testid="json-textarea"]', VALID_JSON);
      await page.click('[data-testid="preview-button"]');
      await expect(page.locator('[data-testid="bulk-import-preview"]')).toBeVisible();

      // Click Cancel
      await page.click('[data-testid="preview-cancel"]');

      // Should return to upload
      await expect(page.locator('[data-testid="json-textarea"]')).toBeVisible();
    });
  });

  // ─── UI Elements ────────────────────────────────────────────

  test.describe('UI Elements', () => {
    test('should display page header and format example', async ({ page }) => {
      await page.goto('/admin/games/import/bulk');
      await expect(page.locator('[data-testid="bulk-import-uploader"]')).toBeVisible();

      // Header
      await expect(page.locator('text=Bulk Import Games from JSON')).toBeVisible();

      // JSON format example card
      await expect(page.locator('[data-testid="json-format-example"]')).toBeVisible();
      await expect(page.locator('text=Expected JSON Format')).toBeVisible();
    });

    test('should show drop zone for file upload', async ({ page }) => {
      await page.goto('/admin/games/import/bulk');
      await expect(page.locator('[data-testid="bulk-import-uploader"]')).toBeVisible();

      await expect(page.locator('[data-testid="drop-zone"]')).toBeVisible();
      await expect(page.locator('text=Drag & drop a .json file here')).toBeVisible();
    });

    test('should disable Preview button when textarea is empty', async ({ page }) => {
      await page.goto('/admin/games/import/bulk');
      await expect(page.locator('[data-testid="bulk-import-uploader"]')).toBeVisible();

      // Preview button should be disabled initially
      await expect(page.locator('[data-testid="preview-button"]')).toBeDisabled();

      // After typing, it should be enabled
      await page.fill('[data-testid="json-textarea"]', VALID_JSON);
      await expect(page.locator('[data-testid="preview-button"]')).toBeEnabled();
    });

    test('should show CSV download button in results', async ({ page }) => {
      await setupBulkImportMock(page, FULL_SUCCESS_RESPONSE);
      await page.goto('/admin/games/import/bulk');
      await expect(page.locator('[data-testid="bulk-import-uploader"]')).toBeVisible();

      await page.fill('[data-testid="json-textarea"]', VALID_JSON);
      await page.click('[data-testid="preview-button"]');
      await page.click('[data-testid="confirm-import"]');

      await expect(page.locator('[data-testid="download-csv-button"]')).toBeVisible();
      await expect(page.locator('text=Download CSV Report')).toBeVisible();
    });
  });
});
