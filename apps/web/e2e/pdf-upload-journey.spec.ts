/**
 * E2E Test: PDF Upload Journey - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/helpers/AuthHelper.ts - mockAuthenticatedSession()
 * @see apps/web/e2e/pages/helpers/GamesHelper.ts - mockPdfUploadJourney()
 *
 * Scenario: User uploads a PDF and verifies it appears in the uploaded PDFs list
 *
 * Given: An authenticated editor user with an existing game
 * When: The user selects the game, uploads a PDF rulebook
 * Then: The PDF should appear in the "Uploaded PDFs" table with correct details
 */

import { test as base, expect, Page } from '@playwright/test';
import { AuthHelper, GamesHelper, USER_FIXTURES } from './pages';
import { getTextMatcher, t } from './fixtures/i18n';

// Extend test with editor authentication
const test = base.extend<{ editorPage: Page }>({
  editorPage: async ({ page }, use) => {
    const authHelper = new AuthHelper(page);
    // Use editor fixture for upload permissions
    await authHelper.mockAuthenticatedSession({
      ...USER_FIXTURES.user,
      role: 'Editor' as const,
    });
    await use(page);
  },
});

test.describe('PDF Upload Journey', () => {
  test('User uploads PDF and verifies it appears in the list', async ({ editorPage: page }) => {
    const gamesHelper = new GamesHelper(page);

    // Setup: Mock complete PDF upload journey with stateful logic
    const { games, pdfs } = await gamesHelper.mockPdfUploadJourney();
    const gameId = games[0].id;

    // When: User navigates to upload page
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
      buffer: buffer,
    });

    // When: User clicks upload button
    const uploadButton = page.locator('button[type="submit"]', { hasText: /Upload/i });
    await uploadButton.click();

    // Wait for upload to complete - the page will transition to "parse" step
    await page.waitForTimeout(2000);

    // Verify that the PDF was added to the pdfs array
    // The upload should have triggered the POST /ingest/pdf endpoint
    expect(pdfs.length).toBe(1);
    expect(pdfs[0].fileName).toBe('test-rulebook.pdf');

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
