/**
 * E2E Test: PDF Processing Progress Tracking (PDF-08)
 *
 * Scenario: User uploads a PDF and monitors real-time processing progress
 *
 * Given: An authenticated editor user with an existing game
 * When: The user uploads a PDF and watches the processing progress
 * Then: Progress bar advances through steps, shows time estimates, and allows cancellation
 */

import { test, expect, type Page } from '@playwright/test';

const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

/**
 * Sets up mock authentication routes
 */
async function setupAuthRoutes(page: Page) {
  let authenticated = false;

  const userResponse = {
    user: {
      id: 'test-user-1',
      email: 'testuser@meepleai.dev',
      displayName: 'Test User',
      role: 'Editor'
    },
    expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString()
  };

  await page.route(`${apiBase}/auth/me`, async (route) => {
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

  await page.route(`${apiBase}/auth/login`, async (route) => {
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
 * Sets up mock games routes
 */
async function setupGamesRoutes(page: Page) {
  const games: any[] = [
    {
      id: 'game-1',
      name: 'Catan',
      description: 'A strategic board game',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    }
  ];

  await page.route(`${apiBase}/games`, async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(games)
      });
    }
  });

  return { games };
}

/**
 * Sets up mock PDF upload and progress routes
 */
async function setupPdfProgressRoutes(page: Page, gameId: string) {
  let currentStep = 'Uploading';
  let percentComplete = 10;
  let documentId = '';
  let cancelled = false;

  // Mock PDF upload endpoint
  await page.route(`${apiBase}/ingest/pdf`, async (route) => {
    if (route.request().method() === 'POST') {
      documentId = 'test-doc-123';
      currentStep = 'Extracting';
      percentComplete = 20;

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ documentId })
      });
    }
  });

  // Mock PDF processing progress endpoint
  await page.route(`${apiBase}/pdfs/*/progress`, async (route) => {
    if (route.request().method() === 'GET') {
      // Simulate progress advancement
      const progressSteps: Record<string, { next: string; percent: number; time: number }> = {
        'Uploading': { next: 'Extracting', percent: 20, time: 120 },
        'Extracting': { next: 'Chunking', percent: 40, time: 90 },
        'Chunking': { next: 'Embedding', percent: 60, time: 60 },
        'Embedding': { next: 'Indexing', percent: 80, time: 30 },
        'Indexing': { next: 'Completed', percent: 100, time: 0 }
      };

      if (cancelled) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            currentStep: 'Failed',
            percentComplete: percentComplete,
            errorMessage: 'Cancelled by user',
            updatedAt: new Date().toISOString()
          })
        });
        return;
      }

      const stepInfo = progressSteps[currentStep];
      if (stepInfo) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            currentStep,
            percentComplete,
            estimatedTimeRemaining: stepInfo.time,
            updatedAt: new Date().toISOString()
          })
        });

        // Auto-advance to next step for next poll
        currentStep = stepInfo.next;
        percentComplete = stepInfo.percent;
      } else {
        // Completed state
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            currentStep: 'Completed',
            percentComplete: 100,
            updatedAt: new Date().toISOString()
          })
        });
      }
    }
  });

  // Mock PDF processing cancellation endpoint
  await page.route(`${apiBase}/pdfs/*/processing`, async (route) => {
    if (route.request().method() === 'DELETE') {
      cancelled = true;
      await route.fulfill({
        status: 204,
        contentType: 'application/json'
      });
    }
  });

  // Mock PDF list endpoint
  await page.route(`${apiBase}/games/${gameId}/pdfs`, async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ pdfs: [] })
      });
    }
  });

  // Mock PDF text endpoint (for old progress polling)
  await page.route(`${apiBase}/pdfs/*/text`, async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: documentId,
          fileName: 'test.pdf',
          processingStatus: 'processing'
        })
      });
    }
  });

  return { documentId };
}

test.describe('PDF Processing Progress Tracking (PDF-08)', () => {
  test('should display processing progress with all steps', async ({ page }) => {
    // Given: Authenticated user with a game
    const authControl = await setupAuthRoutes(page);
    const { games } = await setupGamesRoutes(page);
    const gameId = games[0].id;
    await setupPdfProgressRoutes(page, gameId);

    authControl.setAuthenticated(true);
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
      buffer: buffer
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

  test('should show time remaining estimate', async ({ page }) => {
    // Given: Authenticated user
    const authControl = await setupAuthRoutes(page);
    const { games } = await setupGamesRoutes(page);
    await setupPdfProgressRoutes(page, games[0].id);

    authControl.setAuthenticated(true);
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
      buffer: buffer
    });

    const uploadButton = page.locator('button[type="submit"]', { hasText: /Upload/i });
    await uploadButton.click();

    // Then: Time remaining should be displayed
    await page.waitForTimeout(1000);
    await expect(page.locator('text=/estimated time remaining/i')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('text=/min|sec/i')).toBeVisible();
  });

  test('should show cancel button and confirmation dialog', async ({ page }) => {
    // Given: Authenticated user
    const authControl = await setupAuthRoutes(page);
    const { games } = await setupGamesRoutes(page);
    await setupPdfProgressRoutes(page, games[0].id);

    authControl.setAuthenticated(true);
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
      buffer: buffer
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

  test('should cancel processing when user confirms', async ({ page }) => {
    // Given: Authenticated user
    const authControl = await setupAuthRoutes(page);
    const { games } = await setupGamesRoutes(page);
    await setupPdfProgressRoutes(page, games[0].id);

    authControl.setAuthenticated(true);
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
      buffer: buffer
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

  test('should close dialog when user chooses to continue', async ({ page }) => {
    // Given: Authenticated user
    const authControl = await setupAuthRoutes(page);
    const { games } = await setupGamesRoutes(page);
    await setupPdfProgressRoutes(page, games[0].id);

    authControl.setAuthenticated(true);
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
      buffer: buffer
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

  test('should update progress percentage', async ({ page }) => {
    // Given: Authenticated user
    const authControl = await setupAuthRoutes(page);
    const { games } = await setupGamesRoutes(page);
    await setupPdfProgressRoutes(page, games[0].id);

    authControl.setAuthenticated(true);
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
      buffer: buffer
    });

    const uploadButton = page.locator('button[type="submit"]', { hasText: /Upload/i });
    await uploadButton.click();

    // Then: Progress percentage should be displayed
    await page.waitForTimeout(1000);
    await expect(page.locator('text=/progress:/i')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('text=/%/i')).toBeVisible();
  });

  test('should have proper accessibility attributes', async ({ page }) => {
    // Given: Authenticated user
    const authControl = await setupAuthRoutes(page);
    const { games } = await setupGamesRoutes(page);
    await setupPdfProgressRoutes(page, games[0].id);

    authControl.setAuthenticated(true);
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
      buffer: buffer
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
