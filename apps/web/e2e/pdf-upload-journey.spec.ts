import { test, expect, Page } from '@playwright/test';

/**
 * E2E Test: PDF Upload Journey
 *
 * Scenario: User uploads a PDF and verifies it appears in the uploaded PDFs list
 *
 * Given: An authenticated editor user with an existing game
 * When: The user selects the game, uploads a PDF rulebook
 * Then: The PDF should appear in the "Uploaded PDFs" table with correct details
 */

const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

/**
 * Sets up mock API routes for authentication flow
 * Simulates a user registration and login flow
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

  // Mock /auth/me endpoint
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

  // Mock /auth/register endpoint
  await page.route(`${apiBase}/auth/register`, async (route) => {
    const request = route.request();
    if (request.method() === 'POST') {
      authenticated = true;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(userResponse)
      });
    }
  });

  // Mock /auth/login endpoint
  await page.route(`${apiBase}/auth/login`, async (route) => {
    const request = route.request();
    if (request.method() === 'POST') {
      authenticated = true;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(userResponse)
      });
    }
  });

  // Mock /auth/logout endpoint
  await page.route(`${apiBase}/auth/logout`, async (route) => {
    if (route.request().method() === 'POST') {
      authenticated = false;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Logged out' })
      });
    }
  });

  return { setAuthenticated: (value: boolean) => { authenticated = value; } };
}

/**
 * Sets up mock API routes for games management with a pre-existing game
 */
async function setupGamesRoutes(page: Page) {
  // Start with one existing game
  const games: any[] = [
    {
      id: 'game-1',
      name: 'Catan',
      description: 'A strategic board game about resource management',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    }
  ];

  // Mock GET /games endpoint
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
 * Sets up mock API routes for PDF management
 */
async function setupPdfRoutes(page: Page, gameId: string) {
  const pdfs: any[] = [];
  let nextPdfId = 1;
  let lastDocumentId = '';

  // Mock GET /games/{gameId}/pdfs endpoint
  await page.route(`${apiBase}/games/${gameId}/pdfs`, async (route) => {
    if (route.request().method() === 'GET') {
      // IMPORTANT: The response must have { pdfs: [...] } structure, not just the array
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ pdfs: pdfs })
      });
    }
  });

  // Mock POST /ingest/pdf endpoint (the actual upload endpoint)
  await page.route(`${apiBase}/ingest/pdf`, async (route) => {
    if (route.request().method() === 'POST') {
      // Simulate successful PDF upload
      const documentId = `doc-${nextPdfId}`;
      lastDocumentId = documentId;

      const newPdf = {
        id: `pdf-${nextPdfId++}`,
        gameId: gameId,
        fileName: 'test-rulebook.pdf',
        fileSizeBytes: 1024 * 100, // 100 KB
        uploadedAt: new Date().toISOString(),
        status: 'Completed',
        pageCount: 10
      };
      pdfs.push(newPdf);

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ documentId: documentId })
      });
    }
  });

  return { pdfs };
}

test.describe('PDF Upload Journey', () => {
  test('User uploads PDF and verifies it appears in the list', async ({ page }) => {
    // Given: Set up mock API routes
    const authControl = await setupAuthRoutes(page);
    const { games } = await setupGamesRoutes(page);

    // Set up PDF routes for the pre-existing game
    const gameId = games[0].id;
    const { pdfs } = await setupPdfRoutes(page, gameId);

    // When: User authenticates and navigates to upload page
    authControl.setAuthenticated(true);
    await page.goto('/upload');

    // Then: User should see the upload page
    await expect(page.locator('h1')).toContainText('PDF Import Wizard');

    // Wait for games to load - the game select dropdown should be visible
    const gameSelect = page.locator('select#gameSelect');
    await expect(gameSelect).toBeVisible({ timeout: 10000 });

    // Then: The existing game should be pre-selected
    await expect(gameSelect).toHaveValue(gameId);

    // When: User confirms the game selection
    const confirmButton = page.locator('button', { hasText: 'Confirm selection' });
    await confirmButton.click();

    // Wait for confirmation to process
    await page.waitForTimeout(500);

    // When: User uploads a PDF file
    const fileInput = page.locator('input[type="file"]');
    await expect(fileInput).toBeVisible();

    // Create a test PDF file
    const buffer = Buffer.from('%PDF-1.4\nTest PDF content');
    await fileInput.setInputFiles({
      name: 'test-rulebook.pdf',
      mimeType: 'application/pdf',
      buffer: buffer
    });

    // When: User clicks upload button
    const uploadButton = page.locator('button[type="submit"]', { hasText: /Upload/i });
    await uploadButton.click();

    // Wait for upload to complete - the page will transition to "parse" step
    // We need to wait for the success message or the step indicator to change
    await page.waitForTimeout(2000);

    // Verify that the PDF was added to the pdfs array
    // The upload should have triggered the POST /ingest/pdf endpoint
    // which adds the PDF to the pdfs array in the mock

    // Navigate back to upload step to see the uploaded PDFs table
    await page.goto('/upload');

    // Wait for page to load and games to be fetched
    await expect(gameSelect).toBeVisible({ timeout: 10000 });

    // Confirm the game again to trigger loading of PDFs
    await confirmButton.click();

    // Wait for the "Uploaded PDFs" section heading to appear
    const uploadedPdfsHeading = page.locator('h3', { hasText: 'Uploaded PDFs' });
    await expect(uploadedPdfsHeading).toBeVisible({ timeout: 10000 });

    // Wait for PDFs to load
    await page.waitForTimeout(2000);

    // Then: PDF table should be visible
    const pdfsTable = page.locator('table[aria-label="Uploaded PDFs"]');
    await expect(pdfsTable).toBeVisible({ timeout: 15000 });

    // Then: Table should have the correct headers
    const headers = pdfsTable.locator('thead th');
    await expect(headers.nth(0)).toContainText(/File name|Name/i);
    await expect(headers.nth(1)).toContainText(/Size/i);
    await expect(headers.nth(2)).toContainText(/Uploaded|Date/i);
    await expect(headers.nth(3)).toContainText(/Status/i);

    // Then: Table should contain the uploaded PDF
    const pdfRow = pdfsTable.locator('tbody tr').first();
    await expect(pdfRow).toBeVisible();

    // Then: Verify PDF details in the table
    await expect(pdfRow.locator('td').nth(0)).toContainText('test-rulebook.pdf');
    await expect(pdfRow.locator('td').nth(1)).toContainText(/KB|MB/i); // Size column
    await expect(pdfRow.locator('td').nth(3)).toContainText(/Completed|Success|Pending/i); // Status column

    // Then: Verify action buttons are present
    const actionButtons = pdfRow.locator('button');
    await expect(actionButtons.first()).toBeVisible();
  });
});
