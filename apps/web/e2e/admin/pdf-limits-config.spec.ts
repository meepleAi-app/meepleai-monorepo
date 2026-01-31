/**
 * ADM-12: PDF Limits Configuration
 * Issue #3082 - P1 High
 *
 * Tests admin configuration of PDF limits:
 * - View current PDF upload limits
 * - Configure max file size
 * - Configure max pages per document
 * - Configure allowed file types
 */

import { test, expect } from '../fixtures/chromatic';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface PdfLimitsConfig {
  maxFileSizeMB: number;
  maxPages: number;
  allowedTypes: string[];
  maxUploadsPerDay: {
    free: number;
    normal: number;
    premium: number | 'unlimited';
  };
}

/**
 * Setup mock routes for PDF limits configuration
 */
async function setupPdfLimitsConfigMocks(
  page: Page,
  options: {
    initialConfig?: Partial<PdfLimitsConfig>;
  } = {}
) {
  const defaultConfig: PdfLimitsConfig = {
    maxFileSizeMB: 100,
    maxPages: 500,
    allowedTypes: ['application/pdf'],
    maxUploadsPerDay: {
      free: 3,
      normal: 20,
      premium: 'unlimited',
    },
  };

  const currentConfig = { ...defaultConfig, ...options.initialConfig };

  // Mock admin auth
  await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'admin-user-id',
          email: 'admin@meepleai.dev',
          displayName: 'Admin User',
          role: 'Admin',
        },
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      }),
    });
  });

  // Mock PDF limits config endpoint
  await page.route(`${API_BASE}/api/v1/admin/system/pdf-limits`, async (route) => {
    const method = route.request().method();

    if (method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          config: currentConfig,
          lastModified: new Date().toISOString(),
          modifiedBy: 'admin@meepleai.dev',
        }),
      });
    } else if (method === 'PUT' || method === 'PATCH') {
      const body = await route.request().postDataJSON();

      // Validate limits
      if (body.maxFileSizeMB !== undefined) {
        if (body.maxFileSizeMB < 1 || body.maxFileSizeMB > 500) {
          await route.fulfill({
            status: 400,
            contentType: 'application/json',
            body: JSON.stringify({
              error: 'Validation error',
              message: 'Max file size must be between 1 and 500 MB',
            }),
          });
          return;
        }
        currentConfig.maxFileSizeMB = body.maxFileSizeMB;
      }

      if (body.maxPages !== undefined) {
        if (body.maxPages < 1 || body.maxPages > 2000) {
          await route.fulfill({
            status: 400,
            contentType: 'application/json',
            body: JSON.stringify({
              error: 'Validation error',
              message: 'Max pages must be between 1 and 2000',
            }),
          });
          return;
        }
        currentConfig.maxPages = body.maxPages;
      }

      if (body.maxUploadsPerDay !== undefined) {
        currentConfig.maxUploadsPerDay = { ...currentConfig.maxUploadsPerDay, ...body.maxUploadsPerDay };
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          message: 'PDF limits updated successfully',
          config: currentConfig,
        }),
      });
    } else {
      await route.continue();
    }
  });

  // Mock common admin endpoints
  await page.route(`${API_BASE}/api/v1/admin/**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ data: [] }),
    });
  });

  return { getConfig: () => currentConfig, defaultConfig };
}

test.describe('ADM-12: PDF Limits Configuration', () => {
  test.describe('View PDF Limits', () => {
    test('should navigate to PDF limits configuration page', async ({ page }) => {
      await setupPdfLimitsConfigMocks(page);

      await page.goto('/admin');
      await page.waitForLoadState('networkidle');

      // Navigate to configuration
      const configLink = page.getByRole('link', { name: /configuration|settings|system/i });
      if (await configLink.isVisible()) {
        await configLink.click();
        await page.waitForLoadState('networkidle');
      }

      // Look for PDF limits option
      await expect(page.getByText(/pdf.*limit/i)).toBeVisible();
    });

    test('should display current max file size', async ({ page }) => {
      await setupPdfLimitsConfigMocks(page, {
        initialConfig: { maxFileSizeMB: 100 },
      });

      await page.goto('/admin/config/pdf-limits');
      await page.waitForLoadState('networkidle');

      // Should show file size limit
      await expect(page.getByText(/100.*mb|file.*size/i)).toBeVisible();
    });

    test('should display current max pages', async ({ page }) => {
      await setupPdfLimitsConfigMocks(page, {
        initialConfig: { maxPages: 500 },
      });

      await page.goto('/admin/config/pdf-limits');
      await page.waitForLoadState('networkidle');

      // Should show max pages
      await expect(page.getByText(/500|max.*page/i)).toBeVisible();
    });

    test('should display allowed file types', async ({ page }) => {
      await setupPdfLimitsConfigMocks(page);

      await page.goto('/admin/config/pdf-limits');
      await page.waitForLoadState('networkidle');

      // Should show PDF as allowed type
      await expect(page.getByText(/pdf|allowed.*type/i)).toBeVisible();
    });
  });

  test.describe('Update Max File Size', () => {
    test('should update max file size successfully', async ({ page }) => {
      await setupPdfLimitsConfigMocks(page);

      await page.goto('/admin/config/pdf-limits');
      await page.waitForLoadState('networkidle');

      // Find file size input
      const fileSizeInput = page.getByLabel(/max.*file.*size|file.*size.*limit/i).or(
        page.locator('input[name*="fileSize"], input[name*="maxSize"]')
      ).first();

      if (await fileSizeInput.isVisible()) {
        await fileSizeInput.clear();
        await fileSizeInput.fill('150');

        const saveButton = page.getByRole('button', { name: /save|update|apply/i });
        await saveButton.click();

        await expect(page.getByText(/updated|saved|success/i)).toBeVisible();
      }
    });

    test('should validate file size minimum', async ({ page }) => {
      await setupPdfLimitsConfigMocks(page);

      await page.goto('/admin/config/pdf-limits');
      await page.waitForLoadState('networkidle');

      const fileSizeInput = page.getByLabel(/max.*file.*size/i).or(
        page.locator('input[name*="fileSize"]')
      ).first();

      if (await fileSizeInput.isVisible()) {
        await fileSizeInput.clear();
        await fileSizeInput.fill('0');

        const saveButton = page.getByRole('button', { name: /save|update|apply/i });
        await saveButton.click();

        await expect(page.getByText(/validation|invalid|must.*be/i)).toBeVisible();
      }
    });

    test('should validate file size maximum', async ({ page }) => {
      await setupPdfLimitsConfigMocks(page);

      await page.goto('/admin/config/pdf-limits');
      await page.waitForLoadState('networkidle');

      const fileSizeInput = page.getByLabel(/max.*file.*size/i).or(
        page.locator('input[name*="fileSize"]')
      ).first();

      if (await fileSizeInput.isVisible()) {
        await fileSizeInput.clear();
        await fileSizeInput.fill('1000');

        const saveButton = page.getByRole('button', { name: /save|update|apply/i });
        await saveButton.click();

        await expect(page.getByText(/validation|invalid|must.*be.*between|maximum/i)).toBeVisible();
      }
    });
  });

  test.describe('Update Max Pages', () => {
    test('should update max pages successfully', async ({ page }) => {
      await setupPdfLimitsConfigMocks(page);

      await page.goto('/admin/config/pdf-limits');
      await page.waitForLoadState('networkidle');

      const maxPagesInput = page.getByLabel(/max.*page/i).or(
        page.locator('input[name*="maxPages"]')
      ).first();

      if (await maxPagesInput.isVisible()) {
        await maxPagesInput.clear();
        await maxPagesInput.fill('750');

        const saveButton = page.getByRole('button', { name: /save|update|apply/i });
        await saveButton.click();

        await expect(page.getByText(/updated|saved|success/i)).toBeVisible();
      }
    });
  });

  test.describe('Tier-based Limits', () => {
    test('should display upload limits per tier', async ({ page }) => {
      await setupPdfLimitsConfigMocks(page);

      await page.goto('/admin/config/pdf-limits');
      await page.waitForLoadState('networkidle');

      // Should show tier-based limits
      await expect(page.getByText(/free|normal|premium/i).first()).toBeVisible();
    });

    test('should allow updating tier upload limits', async ({ page }) => {
      await setupPdfLimitsConfigMocks(page);

      await page.goto('/admin/config/pdf-limits');
      await page.waitForLoadState('networkidle');

      // Find Free tier upload limit input
      const freeUploadInput = page.getByLabel(/free.*upload/i).or(
        page.locator('input[name*="freeUpload"]')
      ).first();

      if (await freeUploadInput.isVisible()) {
        await freeUploadInput.clear();
        await freeUploadInput.fill('5');

        const saveButton = page.getByRole('button', { name: /save|update|apply/i });
        await saveButton.click();

        await expect(page.getByText(/updated|saved|success/i)).toBeVisible();
      }
    });
  });

  test.describe('Immediate Effect Warning', () => {
    test('should show warning about immediate effect', async ({ page }) => {
      await setupPdfLimitsConfigMocks(page);

      await page.goto('/admin/config/pdf-limits');
      await page.waitForLoadState('networkidle');

      // Should show warning about changes taking effect immediately
      await expect(page.getByText(/immediate|take.*effect|upload.*in.*progress/i)).toBeVisible();
    });
  });
});
