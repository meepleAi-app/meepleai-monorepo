import { test, expect, Page } from '@playwright/test';

/**
 * E2E Test: PDF Preview Component
 *
 * Feature: PDF-07 - PDF Preview with PDF.js
 *
 * Scenario: User selects a PDF file and previews it before uploading
 *
 * Given: An authenticated editor user on the upload page
 * When: The user selects a PDF file
 * Then: The PDF preview should automatically render with:
 *   - First page displayed
 *   - Zoom controls (25%, 50%, 100%, 150%, 200%)
 *   - Page navigation (prev/next buttons, jump to page input)
 *   - Thumbnail sidebar with all pages
 *   - Keyboard navigation support
 *
 * Acceptance Criteria:
 * - PDF preview component displays automatically after file selection
 * - User can zoom in/out with button controls
 * - User can navigate between pages using prev/next buttons
 * - User can jump to specific page using input field
 * - User can click thumbnail to navigate to that page
 * - User can use keyboard shortcuts (arrow keys for navigation, +/- for zoom)
 * - Upload button is enabled after successful preview
 */

const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

/**
 * Sets up mock API routes for authentication flow
 */
async function setupAuthRoutes(page: Page) {
  let authenticated = false;

  const userResponse = {
    user: {
      id: 'test-user-1',
      email: 'editor@meepleai.dev',
      displayName: 'Editor User',
      role: 'editor'
    },
    expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString()
  };

  // Mock /auth/me endpoint
  await page.route(`${apiBase}/api/v1/auth/me`, async (route) => {
    if (authenticated) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(userResponse)
      });
    } else {
      await route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Unauthorized' })
      });
    }
  });

  // Mock /auth/login endpoint
  await page.route(`${apiBase}/api/v1/auth/login`, async (route) => {
    if (route.request().method() === 'POST') {
      authenticated = true;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(userResponse)
      });
    }
  });

  return { setAuthenticated: (value: boolean) => { authenticated = value; } };
}

/**
 * Sets up mock API routes for games management
 */
async function setupGamesRoutes(page: Page) {
  const games = [
    {
      id: 'game-1',
      name: 'Test Game',
      description: 'A test game for PDF preview',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    }
  ];

  // Mock GET /api/v1/games endpoint
  await page.route(`${apiBase}/api/v1/games`, async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(games)
      });
    }
  });

  // Mock GET /api/v1/games/{gameId}/pdfs endpoint
  await page.route(`${apiBase}/api/v1/games/game-1/pdfs`, async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ pdfs: [] })
      });
    }
  });

  return { games };
}

// Create a minimal valid PDF file buffer
function createTestPdfBuffer(): Buffer {
  // This is a minimal valid PDF structure
  const pdfContent = `%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R] /Count 3 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> /MediaBox [0 0 612 792] /Contents 6 0 R >>
endobj
4 0 obj
<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> /MediaBox [0 0 612 792] /Contents 7 0 R >>
endobj
5 0 obj
<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> /MediaBox [0 0 612 792] /Contents 8 0 R >>
endobj
6 0 obj
<< /Length 44 >>
stream
BT /F1 24 Tf 100 700 Td (Page 1) Tj ET
endstream
endobj
7 0 obj
<< /Length 44 >>
stream
BT /F1 24 Tf 100 700 Td (Page 2) Tj ET
endstream
endobj
8 0 obj
<< /Length 44 >>
stream
BT /F1 24 Tf 100 700 Td (Page 3) Tj ET
endstream
endobj
xref
0 9
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000125 00000 n
0000000297 00000 n
0000000469 00000 n
0000000641 00000 n
0000000733 00000 n
0000000825 00000 n
trailer
<< /Size 9 /Root 1 0 R >>
startxref
917
%%EOF`;

  return Buffer.from(pdfContent);
}

test.describe('PDF Preview Component - Full User Journey', () => {
  test('User previews PDF with zoom, navigation, and thumbnails before upload', async ({ page }) => {
    // Given: Set up authentication and games
    const authControl = await setupAuthRoutes(page);
    await setupGamesRoutes(page);
    authControl.setAuthenticated(true);

    // When: User navigates to upload page
    await page.goto('/upload');

    // Then: Page should load successfully
    await expect(page.locator('h1')).toContainText('PDF Import Wizard');

    // Wait for games to load
    const gameSelect = page.locator('select#gameSelect');
    await expect(gameSelect).toBeVisible({ timeout: 10000 });

    // When: User confirms game selection
    const confirmButton = page.locator('button', { hasText: 'Confirm selection' });
    await confirmButton.click();
    await page.waitForTimeout(500);

    // When: User selects a PDF file
    const fileInput = page.locator('input[type="file"]');
    await expect(fileInput).toBeVisible();

    const pdfBuffer = createTestPdfBuffer();
    await fileInput.setInputFiles({
      name: 'test-rulebook.pdf',
      mimeType: 'application/pdf',
      buffer: pdfBuffer
    });

    // Then: PDF preview should appear
    const pdfPreview = page.locator('[data-testid="pdf-preview"]');
    await expect(pdfPreview).toBeVisible({ timeout: 15000 });

    // Then: Preview should display first page
    await expect(page.locator('[data-testid="current-page"]')).toContainText('Page 1 of 3', { timeout: 10000 });

    // Then: All zoom controls should be visible
    await expect(page.locator('[data-testid="zoom-25"]')).toBeVisible();
    await expect(page.locator('[data-testid="zoom-50"]')).toBeVisible();
    await expect(page.locator('[data-testid="zoom-100"]')).toBeVisible();
    await expect(page.locator('[data-testid="zoom-150"]')).toBeVisible();
    await expect(page.locator('[data-testid="zoom-200"]')).toBeVisible();

    // Then: Current zoom level should be 100%
    await expect(page.locator('[data-testid="zoom-level"]')).toContainText('100%');

    // When: User clicks zoom in button
    await page.locator('[data-testid="zoom-in"]').click();

    // Then: Zoom level should increase to 150%
    await expect(page.locator('[data-testid="zoom-level"]')).toContainText('150%');

    // When: User clicks zoom out button
    await page.locator('[data-testid="zoom-out"]').click();

    // Then: Zoom level should decrease to 100%
    await expect(page.locator('[data-testid="zoom-level"]')).toContainText('100%');

    // When: User clicks on a specific zoom preset (50%)
    await page.locator('[data-testid="zoom-50"]').click();

    // Then: Zoom level should be set to 50%
    await expect(page.locator('[data-testid="zoom-level"]')).toContainText('50%');

    // When: User clicks next page button
    await page.locator('[data-testid="next-page"]').click();

    // Then: Current page should be 2
    await expect(page.locator('[data-testid="current-page"]')).toContainText('Page 2 of 3');

    // When: User clicks previous page button
    await page.locator('[data-testid="prev-page"]').click();

    // Then: Current page should be back to 1
    await expect(page.locator('[data-testid="current-page"]')).toContainText('Page 1 of 3');

    // When: User jumps to specific page using input
    const jumpInput = page.locator('[data-testid="jump-to-page-input"]');
    await jumpInput.fill('3');
    await page.locator('[data-testid="jump-to-page-button"]').click();

    // Then: Current page should be 3
    await expect(page.locator('[data-testid="current-page"]')).toContainText('Page 3 of 3');

    // Then: Thumbnail sidebar should be visible (desktop)
    const thumbnailSidebar = page.locator('[data-testid="thumbnail-sidebar"]');
    if (await thumbnailSidebar.isVisible()) {
      // When: User clicks on thumbnail for page 1
      const thumbnail1 = page.locator('[data-testid="thumbnail-1"]');
      await thumbnail1.click();

      // Then: Current page should be 1
      await expect(page.locator('[data-testid="current-page"]')).toContainText('Page 1 of 3');
    }

    // Test keyboard navigation
    // When: User presses ArrowRight key
    await page.keyboard.press('ArrowRight');

    // Then: Should navigate to page 2
    await expect(page.locator('[data-testid="current-page"]')).toContainText('Page 2 of 3');

    // When: User presses ArrowLeft key
    await page.keyboard.press('ArrowLeft');

    // Then: Should navigate back to page 1
    await expect(page.locator('[data-testid="current-page"]')).toContainText('Page 1 of 3');

    // When: User presses + key
    await page.keyboard.press('+');

    // Then: Should zoom in (from 50% to 100%)
    await expect(page.locator('[data-testid="zoom-level"]')).toContainText('100%');

    // When: User presses - key
    await page.keyboard.press('-');

    // Then: Should zoom out (from 100% to 50%)
    await expect(page.locator('[data-testid="zoom-level"]')).toContainText('50%');

    // Then: Upload button should be enabled
    const uploadButton = page.locator('[data-testid="upload-button"]');
    await expect(uploadButton).toBeEnabled();

    // Verify accessibility attributes
    await expect(page.locator('[role="toolbar"]')).toBeVisible();
    await expect(page.locator('[role="navigation"][aria-label="Page navigation"]')).toBeVisible();
    await expect(page.locator('[aria-label="Previous page"]')).toBeVisible();
    await expect(page.locator('[aria-label="Next page"]')).toBeVisible();
    await expect(page.locator('[aria-label="Zoom in"]')).toBeVisible();
    await expect(page.locator('[aria-label="Zoom out"]')).toBeVisible();
  });

  test('PDF preview handles keyboard navigation without interfering with input fields', async ({ page }) => {
    // Given: Set up authentication and games
    const authControl = await setupAuthRoutes(page);
    await setupGamesRoutes(page);
    authControl.setAuthenticated(true);

    // When: User navigates to upload page
    await page.goto('/upload');
    await expect(page.locator('h1')).toContainText('PDF Import Wizard');

    // Wait for games and confirm selection
    await expect(page.locator('select#gameSelect')).toBeVisible({ timeout: 10000 });
    await page.locator('button', { hasText: 'Confirm selection' }).click();
    await page.waitForTimeout(500);

    // When: User selects a PDF file
    const fileInput = page.locator('input[type="file"]');
    const pdfBuffer = createTestPdfBuffer();
    await fileInput.setInputFiles({
      name: 'test-rulebook.pdf',
      mimeType: 'application/pdf',
      buffer: pdfBuffer
    });

    // Then: PDF preview should appear
    await expect(page.locator('[data-testid="pdf-preview"]')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('[data-testid="current-page"]')).toContainText('Page 1 of 3', { timeout: 10000 });

    // When: User focuses on jump-to-page input
    const jumpInput = page.locator('[data-testid="jump-to-page-input"]');
    await jumpInput.click();

    // When: User types arrow keys in input field
    await jumpInput.press('ArrowRight');
    await jumpInput.press('ArrowLeft');

    // Then: Page navigation should NOT be triggered (still on page 1)
    await expect(page.locator('[data-testid="current-page"]')).toContainText('Page 1 of 3');

    // When: User types in input field
    await jumpInput.fill('2');

    // Then: Input should contain the value
    await expect(jumpInput).toHaveValue('2');
  });

  test('PDF preview displays error for invalid PDF files', async ({ page }) => {
    // Given: Set up authentication and games
    const authControl = await setupAuthRoutes(page);
    await setupGamesRoutes(page);
    authControl.setAuthenticated(true);

    // When: User navigates to upload page
    await page.goto('/upload');
    await expect(page.locator('h1')).toContainText('PDF Import Wizard');

    // Wait for games and confirm selection
    await expect(page.locator('select#gameSelect')).toBeVisible({ timeout: 10000 });
    await page.locator('button', { hasText: 'Confirm selection' }).click();
    await page.waitForTimeout(500);

    // When: User selects an invalid file (not a real PDF)
    const fileInput = page.locator('input[type="file"]');
    const invalidBuffer = Buffer.from('This is not a valid PDF file');

    await fileInput.setInputFiles({
      name: 'invalid.pdf',
      mimeType: 'application/pdf',
      buffer: invalidBuffer
    });

    // Then: Validation error should appear (client-side validation)
    // The file should be rejected because it doesn't have PDF magic bytes
    await expect(page.locator('text=Validation Failed')).toBeVisible({ timeout: 5000 });
    await expect(page.locator('text=/Invalid PDF file format/i')).toBeVisible();
  });

  test('PDF preview zoom buttons have correct disabled states', async ({ page }) => {
    // Given: Set up authentication and games
    const authControl = await setupAuthRoutes(page);
    await setupGamesRoutes(page);
    authControl.setAuthenticated(true);

    // Navigate to upload page
    await page.goto('/upload');
    await expect(page.locator('select#gameSelect')).toBeVisible({ timeout: 10000 });
    await page.locator('button', { hasText: 'Confirm selection' }).click();
    await page.waitForTimeout(500);

    // Select PDF file
    const fileInput = page.locator('input[type="file"]');
    const pdfBuffer = createTestPdfBuffer();
    await fileInput.setInputFiles({
      name: 'test-rulebook.pdf',
      mimeType: 'application/pdf',
      buffer: pdfBuffer
    });

    // Wait for preview
    await expect(page.locator('[data-testid="pdf-preview"]')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('[data-testid="zoom-level"]')).toContainText('100%');

    // When: User zooms out to minimum (25%)
    await page.locator('[data-testid="zoom-25"]').click();

    // Then: Zoom-out button should be disabled
    await expect(page.locator('[data-testid="zoom-out"]')).toBeDisabled();
    await expect(page.locator('[data-testid="zoom-in"]')).toBeEnabled();

    // When: User zooms in to maximum (200%)
    await page.locator('[data-testid="zoom-200"]').click();

    // Then: Zoom-in button should be disabled
    await expect(page.locator('[data-testid="zoom-in"]')).toBeDisabled();
    await expect(page.locator('[data-testid="zoom-out"]')).toBeEnabled();
  });

  test('PDF preview page navigation buttons have correct disabled states', async ({ page }) => {
    // Given: Set up authentication and games
    const authControl = await setupAuthRoutes(page);
    await setupGamesRoutes(page);
    authControl.setAuthenticated(true);

    // Navigate to upload page
    await page.goto('/upload');
    await expect(page.locator('select#gameSelect')).toBeVisible({ timeout: 10000 });
    await page.locator('button', { hasText: 'Confirm selection' }).click();
    await page.waitForTimeout(500);

    // Select PDF file
    const fileInput = page.locator('input[type="file"]');
    const pdfBuffer = createTestPdfBuffer();
    await fileInput.setInputFiles({
      name: 'test-rulebook.pdf',
      mimeType: 'application/pdf',
      buffer: pdfBuffer
    });

    // Wait for preview
    await expect(page.locator('[data-testid="pdf-preview"]')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('[data-testid="current-page"]')).toContainText('Page 1 of 3', { timeout: 10000 });

    // Then: Previous button should be disabled on first page
    await expect(page.locator('[data-testid="prev-page"]')).toBeDisabled();
    await expect(page.locator('[data-testid="next-page"]')).toBeEnabled();

    // When: User navigates to last page
    await page.locator('[data-testid="jump-to-page-input"]').fill('3');
    await page.locator('[data-testid="jump-to-page-button"]').click();
    await expect(page.locator('[data-testid="current-page"]')).toContainText('Page 3 of 3');

    // Then: Next button should be disabled on last page
    await expect(page.locator('[data-testid="next-page"]')).toBeDisabled();
    await expect(page.locator('[data-testid="prev-page"]')).toBeEnabled();
  });
});
