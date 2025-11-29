/**
 * E2E Test: PDF Processing Progress Tracking (PDF-08) - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/helpers/AuthHelper.ts - mockAuthenticatedSession()
 * @see apps/web/e2e/pages/helpers/GamesHelper.ts - mockPdfProcessingProgress()
 *
 * Scenario: User uploads a PDF and monitors real-time processing progress
 *
 * Given: An authenticated editor user with an existing game
 * When: The user uploads a PDF and watches the processing progress
 * Then: Progress bar advances through steps, shows time estimates, and allows cancellation
 */

import { test as base, expect, type Page } from '@playwright/test';
import { AuthHelper, GamesHelper, USER_FIXTURES } from './pages';
import { getTextMatcher, t } from './fixtures/i18n';

// Extend test with editor authentication
const test = base.extend<{ editorPage: Page; gameId: string }>({
  editorPage: async ({ page }, use) => {
    const authHelper = new AuthHelper(page);
    const gamesHelper = new GamesHelper(page);

    // Mock editor authentication
    await authHelper.mockAuthenticatedSession({
      ...USER_FIXTURES.user,
      role: 'Editor' as const,
    });

    // Mock games list for game selection
    await gamesHelper.mockGamesList([
      {
        id: 'game-1',
        name: 'Catan',
        createdAt: new Date().toISOString(),
      },
    ]);

    await use(page);
  },
  gameId: async ({ page }, use) => {
    await use('game-1');
  },
});

test.describe('PDF Processing Progress Tracking (PDF-08)', () => {
  test('should display processing progress with all steps', async ({
    editorPage: page,
    gameId,
  }) => {
    const gamesHelper = new GamesHelper(page);

    // Setup: Mock PDF processing progress with auto-advancing steps
    await gamesHelper.mockPdfProcessingProgress(gameId);

    await page.goto('/upload');

    // When: User confirms game and uploads PDF
    await expect(page.locator('h1')).toContainText('PDF Import Wizard');
    const gameSelect = page.locator('select#gameSelect');
    await expect(gameSelect).toBeVisible({ timeout: 10000 });

    const confirmButton = page.locator('button', { hasText: 'Confirm selection' });
    await confirmButton.click();
    await page.waitForTimeout(500);

    // Upload file
    const fileInput = page.locator('input[type="file"]');
    const buffer = Buffer.from('%PDF-1.4\nTest PDF content');
    await fileInput.setInputFiles({
      name: 'test-rulebook.pdf',
      mimeType: 'application/pdf',
      buffer: buffer,
    });

    const uploadButton = page.locator('button[type="submit"]', { hasText: /Upload/i });
    await uploadButton.click();

    // Then: Progress bar should appear
    await page.waitForTimeout(1000);
    const progressBar = page.locator('[role="progressbar"]');
    await expect(progressBar).toBeVisible({ timeout: 10000 });

    // Then: All processing steps should be visible
    await expect(page.locator('text=Uploading')).toBeVisible();
    await expect(page.locator('text=Extracting')).toBeVisible();
    await expect(page.locator('text=Chunking')).toBeVisible();
    await expect(page.locator('text=Embedding')).toBeVisible();
    await expect(page.locator('text=Indexing')).toBeVisible();

    // Then: Current status should be displayed
    await expect(page.locator('text=/Status:/i')).toBeVisible();
  });

  test('should show time remaining estimate', async ({ editorPage: page, gameId }) => {
    const gamesHelper = new GamesHelper(page);
    await gamesHelper.mockPdfProcessingProgress(gameId);

    await page.goto('/upload');

    // When: User uploads PDF
    const gameSelect = page.locator('select#gameSelect');
    await expect(gameSelect).toBeVisible({ timeout: 10000 });
    const confirmButton = page.locator('button', { hasText: 'Confirm selection' });
    await confirmButton.click();
    await page.waitForTimeout(500);

    const fileInput = page.locator('input[type="file"]');
    const buffer = Buffer.from('%PDF-1.4\nTest PDF content');
    await fileInput.setInputFiles({
      name: 'test.pdf',
      mimeType: 'application/pdf',
      buffer: buffer,
    });

    const uploadButton = page.locator('button[type="submit"]', { hasText: /Upload/i });
    await uploadButton.click();

    // Then: Time remaining should be displayed
    await page.waitForTimeout(1000);
    await expect(page.locator('text=/estimated time remaining/i')).toBeVisible({
      timeout: 10000,
    });
    await expect(page.locator('text=/min|sec/i')).toBeVisible();
  });

  test('should show cancel button and confirmation dialog', async ({
    editorPage: page,
    gameId,
  }) => {
    const gamesHelper = new GamesHelper(page);
    await gamesHelper.mockPdfProcessingProgress(gameId);

    await page.goto('/upload');

    // When: User uploads PDF
    const gameSelect = page.locator('select#gameSelect');
    await expect(gameSelect).toBeVisible({ timeout: 10000 });
    const confirmButton = page.locator('button', { hasText: 'Confirm selection' });
    await confirmButton.click();
    await page.waitForTimeout(500);

    const fileInput = page.locator('input[type="file"]');
    const buffer = Buffer.from('%PDF-1.4\nTest PDF content');
    await fileInput.setInputFiles({
      name: 'test.pdf',
      mimeType: 'application/pdf',
      buffer: buffer,
    });

    const uploadButton = page.locator('button[type="submit"]', { hasText: /Upload/i });
    await uploadButton.click();

    // Then: Cancel button should be visible
    await page.waitForTimeout(1000);
    const cancelButton = page.locator('button', { hasText: /cancel processing/i });
    await expect(cancelButton).toBeVisible({ timeout: 10000 });

    // When: User clicks cancel
    await cancelButton.click();

    // Then: Confirmation dialog should appear
    const dialog = page.locator('[role="dialog"]');
    await expect(dialog).toBeVisible();
    await expect(page.locator('text=/cancel pdf processing/i')).toBeVisible();
    await expect(page.locator('button', { hasText: /yes, cancel/i })).toBeVisible();
    await expect(page.locator('button', { hasText: /no, continue processing/i })).toBeVisible();
  });

  test('should cancel processing when user confirms', async ({ editorPage: page, gameId }) => {
    const gamesHelper = new GamesHelper(page);
    await gamesHelper.mockPdfProcessingProgress(gameId);

    await page.goto('/upload');

    // When: User uploads PDF
    const gameSelect = page.locator('select#gameSelect');
    await expect(gameSelect).toBeVisible({ timeout: 10000 });
    const confirmButton = page.locator('button', { hasText: 'Confirm selection' });
    await confirmButton.click();
    await page.waitForTimeout(500);

    const fileInput = page.locator('input[type="file"]');
    const buffer = Buffer.from('%PDF-1.4\nTest PDF content');
    await fileInput.setInputFiles({
      name: 'test.pdf',
      mimeType: 'application/pdf',
      buffer: buffer,
    });

    const uploadButton = page.locator('button[type="submit"]', { hasText: /Upload/i });
    await uploadButton.click();

    // When: User cancels processing
    await page.waitForTimeout(1000);
    const cancelButton = page.locator('button', { hasText: /cancel processing/i });
    await expect(cancelButton).toBeVisible({ timeout: 10000 });
    await cancelButton.click();

    const confirmCancelButton = page.locator('button', { hasText: /yes, cancel/i });
    await confirmCancelButton.click();

    // Then: Error message should appear
    await expect(page.locator('text=/cancelled by user/i')).toBeVisible({ timeout: 10000 });
  });

  test('should close dialog when user chooses to continue', async ({
    editorPage: page,
    gameId,
  }) => {
    const gamesHelper = new GamesHelper(page);
    await gamesHelper.mockPdfProcessingProgress(gameId);

    await page.goto('/upload');

    // When: User uploads PDF
    const gameSelect = page.locator('select#gameSelect');
    await expect(gameSelect).toBeVisible({ timeout: 10000 });
    const confirmButton = page.locator('button', { hasText: 'Confirm selection' });
    await confirmButton.click();
    await page.waitForTimeout(500);

    const fileInput = page.locator('input[type="file"]');
    const buffer = Buffer.from('%PDF-1.4\nTest PDF content');
    await fileInput.setInputFiles({
      name: 'test.pdf',
      mimeType: 'application/pdf',
      buffer: buffer,
    });

    const uploadButton = page.locator('button[type="submit"]', { hasText: /Upload/i });
    await uploadButton.click();

    // When: User clicks cancel but chooses to continue
    await page.waitForTimeout(1000);
    const cancelButton = page.locator('button', { hasText: /cancel processing/i });
    await expect(cancelButton).toBeVisible({ timeout: 10000 });
    await cancelButton.click();

    const continueButton = page.locator('button', { hasText: /no, continue processing/i });
    await continueButton.click();

    // Then: Dialog should close
    const dialog = page.locator('[role="dialog"]');
    await expect(dialog).not.toBeVisible();

    // And: Processing should continue
    await expect(cancelButton).toBeVisible();
  });

  test('should update progress percentage', async ({ editorPage: page, gameId }) => {
    const gamesHelper = new GamesHelper(page);
    await gamesHelper.mockPdfProcessingProgress(gameId);

    await page.goto('/upload');

    // When: User uploads PDF
    const gameSelect = page.locator('select#gameSelect');
    await expect(gameSelect).toBeVisible({ timeout: 10000 });
    const confirmButton = page.locator('button', { hasText: 'Confirm selection' });
    await confirmButton.click();
    await page.waitForTimeout(500);

    const fileInput = page.locator('input[type="file"]');
    const buffer = Buffer.from('%PDF-1.4\nTest PDF content');
    await fileInput.setInputFiles({
      name: 'test.pdf',
      mimeType: 'application/pdf',
      buffer: buffer,
    });

    const uploadButton = page.locator('button[type="submit"]', { hasText: /Upload/i });
    await uploadButton.click();

    // Then: Progress percentage should be displayed
    await page.waitForTimeout(1000);
    await expect(page.locator('text=/progress:/i')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('text=/%/i')).toBeVisible();
  });

  test('should have proper accessibility attributes', async ({ editorPage: page, gameId }) => {
    const gamesHelper = new GamesHelper(page);
    await gamesHelper.mockPdfProcessingProgress(gameId);

    await page.goto('/upload');

    // When: User uploads PDF
    const gameSelect = page.locator('select#gameSelect');
    await expect(gameSelect).toBeVisible({ timeout: 10000 });
    const confirmButton = page.locator('button', { hasText: 'Confirm selection' });
    await confirmButton.click();
    await page.waitForTimeout(500);

    const fileInput = page.locator('input[type="file"]');
    const buffer = Buffer.from('%PDF-1.4\nTest PDF content');
    await fileInput.setInputFiles({
      name: 'test.pdf',
      mimeType: 'application/pdf',
      buffer: buffer,
    });

    const uploadButton = page.locator('button[type="submit"]', { hasText: /Upload/i });
    await uploadButton.click();

    // Then: Progress bar should have proper ARIA attributes
    await page.waitForTimeout(1000);
    const progressBar = page.locator('[role="progressbar"]').first();
    await expect(progressBar).toHaveAttribute('aria-label', 'PDF processing progress');
    await expect(progressBar).toHaveAttribute('aria-valuemin', '0');
    await expect(progressBar).toHaveAttribute('aria-valuemax', '100');
    await expect(progressBar).toHaveAttribute('aria-live', 'polite');
  });
});
