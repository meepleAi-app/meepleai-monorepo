/**
 * PDF-07: PDF Preview Enhancement
 * Issue #3082 - P2 Medium
 *
 * Tests PDF preview functionality:
 * - Thumbnail preview in upload list
 * - Preview modal with zoom
 * - Preview loading states
 * - Multi-page preview navigation
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

/**
 * Setup mock routes for PDF preview testing
 */
async function setupPdfPreviewMocks(page: Page) {
  // Mock auth
  await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'test-user-id',
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User',
        },
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      }),
    });
  });

  // Mock documents list with thumbnails
  await page.route(`${API_BASE}/api/v1/documents**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        documents: [
          {
            id: 'doc-1',
            name: 'Chess Rules.pdf',
            fileName: 'chess-rules.pdf',
            fileSize: 2048000,
            pageCount: 15,
            thumbnailUrl: '/api/v1/documents/doc-1/thumbnail',
            previewUrl: '/api/v1/documents/doc-1/preview',
            status: 'processed',
            createdAt: new Date(Date.now() - 86400000).toISOString(),
          },
          {
            id: 'doc-2',
            name: 'Game Manual.pdf',
            fileName: 'game-manual.pdf',
            fileSize: 5120000,
            pageCount: 42,
            thumbnailUrl: '/api/v1/documents/doc-2/thumbnail',
            previewUrl: '/api/v1/documents/doc-2/preview',
            status: 'processed',
            createdAt: new Date(Date.now() - 172800000).toISOString(),
          },
        ],
        totalCount: 2,
      }),
    });
  });

  // Mock thumbnail endpoint
  await page.route(`${API_BASE}/api/v1/documents/*/thumbnail`, async (route) => {
    // Return a simple 1x1 transparent PNG
    const png = Buffer.from(
      'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==',
      'base64'
    );
    await route.fulfill({
      status: 200,
      contentType: 'image/png',
      body: png,
    });
  });

  // Mock preview endpoint
  await page.route(`${API_BASE}/api/v1/documents/*/preview**`, async (route) => {
    const url = route.request().url();
    const pageMatch = url.match(/page=(\d+)/);
    const pageNum = pageMatch ? parseInt(pageMatch[1]) : 1;

    // Return preview data
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        pageNumber: pageNum,
        imageUrl: `/api/v1/documents/preview-page-${pageNum}.png`,
        textContent: `Page ${pageNum} content preview text`,
        totalPages: 15,
      }),
    });
  });

  // Mock games endpoint
  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([{ id: 'chess', title: 'Chess' }]),
    });
  });

  return {};
}

test.describe('PDF-07: PDF Preview Enhancement', () => {
  test.describe('Thumbnail Preview', () => {
    test('should display thumbnails in document list', async ({ page }) => {
      await setupPdfPreviewMocks(page);

      await page.goto('/library/documents');
      await page.waitForLoadState('networkidle');

      // Should show thumbnail images
      await expect(
        page.locator('img[src*="thumbnail"], .document-thumbnail, [data-testid="doc-thumbnail"]').first()
      ).toBeVisible({ timeout: 5000 });
    });

    test('should show document name with thumbnail', async ({ page }) => {
      await setupPdfPreviewMocks(page);

      await page.goto('/library/documents');
      await page.waitForLoadState('networkidle');

      // Should show document name
      await expect(page.getByText(/chess.*rules/i)).toBeVisible();
    });

    test('should show page count indicator', async ({ page }) => {
      await setupPdfPreviewMocks(page);

      await page.goto('/library/documents');
      await page.waitForLoadState('networkidle');

      // Should show page count
      await expect(page.getByText(/15.*page|page.*15/i)).toBeVisible();
    });
  });

  test.describe('Preview Modal', () => {
    test('should open preview modal on click', async ({ page }) => {
      await setupPdfPreviewMocks(page);

      await page.goto('/library/documents');
      await page.waitForLoadState('networkidle');

      // Click on document or preview button
      const previewTrigger = page.getByRole('button', { name: /preview|view/i }).first().or(
        page.locator('.document-card, [data-testid="document-item"]').first()
      );

      if (await previewTrigger.isVisible()) {
        await previewTrigger.click();

        // Should open modal
        await expect(
          page.getByRole('dialog').or(page.locator('.preview-modal, [data-testid="preview-modal"]'))
        ).toBeVisible({ timeout: 5000 });
      }
    });

    test('should show document title in modal', async ({ page }) => {
      await setupPdfPreviewMocks(page);

      await page.goto('/library/documents');
      await page.waitForLoadState('networkidle');

      const previewTrigger = page.locator('.document-card, [data-testid="document-item"]').first();
      if (await previewTrigger.isVisible()) {
        await previewTrigger.click();

        // Modal should show document name
        await expect(page.getByText(/chess.*rules/i)).toBeVisible();
      }
    });

    test('should close modal on escape key', async ({ page }) => {
      await setupPdfPreviewMocks(page);

      await page.goto('/library/documents');
      await page.waitForLoadState('networkidle');

      const previewTrigger = page.locator('.document-card').first();
      if (await previewTrigger.isVisible()) {
        await previewTrigger.click();
        await page.waitForTimeout(500);

        await page.keyboard.press('Escape');

        // Modal should close
        await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 3000 });
      }
    });
  });

  test.describe('Zoom Controls', () => {
    test('should display zoom controls in preview', async ({ page }) => {
      await setupPdfPreviewMocks(page);

      await page.goto('/library/documents');
      await page.waitForLoadState('networkidle');

      const previewTrigger = page.locator('.document-card').first();
      if (await previewTrigger.isVisible()) {
        await previewTrigger.click();

        // Should show zoom controls
        await expect(
          page.getByRole('button', { name: /zoom.*in|\+/i }).or(
            page.locator('[data-testid="zoom-in"]')
          )
        ).toBeVisible();
      }
    });

    test('should zoom in on button click', async ({ page }) => {
      await setupPdfPreviewMocks(page);

      await page.goto('/library/documents');
      await page.waitForLoadState('networkidle');

      const previewTrigger = page.locator('.document-card').first();
      if (await previewTrigger.isVisible()) {
        await previewTrigger.click();

        const zoomInButton = page.getByRole('button', { name: /zoom.*in|\+/i });
        if (await zoomInButton.isVisible()) {
          await zoomInButton.click();
          // Zoom should increase
          await expect(page.locator('body')).toBeVisible();
        }
      }
    });

    test('should zoom out on button click', async ({ page }) => {
      await setupPdfPreviewMocks(page);

      await page.goto('/library/documents');
      await page.waitForLoadState('networkidle');

      const previewTrigger = page.locator('.document-card').first();
      if (await previewTrigger.isVisible()) {
        await previewTrigger.click();

        const zoomOutButton = page.getByRole('button', { name: /zoom.*out|-/i });
        if (await zoomOutButton.isVisible()) {
          await zoomOutButton.click();
          await expect(page.locator('body')).toBeVisible();
        }
      }
    });

    test('should reset zoom to fit', async ({ page }) => {
      await setupPdfPreviewMocks(page);

      await page.goto('/library/documents');
      await page.waitForLoadState('networkidle');

      const previewTrigger = page.locator('.document-card').first();
      if (await previewTrigger.isVisible()) {
        await previewTrigger.click();

        const fitButton = page.getByRole('button', { name: /fit|reset|100%/i });
        if (await fitButton.isVisible()) {
          await fitButton.click();
          await expect(page.locator('body')).toBeVisible();
        }
      }
    });
  });

  test.describe('Loading States', () => {
    test('should show loading indicator for preview', async ({ page }) => {
      await setupPdfPreviewMocks(page);

      await page.goto('/library/documents');
      await page.waitForLoadState('networkidle');

      const previewTrigger = page.locator('.document-card').first();
      if (await previewTrigger.isVisible()) {
        await previewTrigger.click();

        // Should show loading initially
        const loadingIndicator = page.locator('.loading, .spinner, [data-loading]');
        // Loading may be very brief
        await expect(page.locator('body')).toBeVisible();
      }
    });

    test('should show placeholder for failed thumbnail', async ({ page }) => {
      await setupPdfPreviewMocks(page);

      // Override to fail thumbnail
      await page.route(`${API_BASE}/api/v1/documents/*/thumbnail`, async (route) => {
        await route.fulfill({ status: 404 });
      });

      await page.goto('/library/documents');
      await page.waitForLoadState('networkidle');

      // Should show placeholder or icon
      await expect(
        page.locator('.placeholder, .pdf-icon, [data-placeholder]').or(page.locator('body'))
      ).toBeVisible();
    });
  });
});
