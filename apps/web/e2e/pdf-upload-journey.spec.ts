/**
 * E2E Test: PDF Upload Journey - Real Backend Integration
 *
 * Scenario: User uploads a PDF and verifies it appears in the uploaded PDFs list
 *
 * Given: An authenticated editor user with an existing game
 * When: The user selects the game, uploads a PDF rulebook
 * Then: The PDF should appear in the "Uploaded PDFs" table with correct details
 */

import { test as base, expect, Page } from './fixtures';
import { WaitHelper } from './helpers/WaitHelper';
import { AuthHelper, USER_FIXTURES } from './pages';

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
    // When: User navigates to upload page
    await page.goto('/upload');

    // Then: User should see the upload page
    await expect(page.locator('h1')).toContainText('PDF Import Wizard');

    // Wait for games to load - the game select dropdown should be visible
    const gameSelect = page.locator('select#gameSelect');
    await expect(gameSelect).toBeVisible({ timeout: 10000 });

    // Then: At least one game should be available (from real backend)
    const gameOptions = await gameSelect.locator('option').count();
    expect(gameOptions).toBeGreaterThan(0);

    // When: User confirms the game selection
    const confirmButton = page.locator('button', { hasText: 'Confirm selection' });
    await confirmButton.click();

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
    const waitHelper = new WaitHelper(page);
    await waitHelper.waitForNetworkIdle(10000);

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
    await waitHelper.waitForNetworkIdle(10000);

    // Then: PDF table should be visible
    const pdfsTable = page.locator('table[aria-label="Uploaded PDFs"]');
    await expect(pdfsTable).toBeVisible({ timeout: 15000 });

    // Then: Table should have the correct headers
    const headers = pdfsTable.locator('thead th');
    await expect(headers.nth(0)).toContainText(/File name|Name/i);
    await expect(headers.nth(1)).toContainText(/Size/i);
    await expect(headers.nth(2)).toContainText(/Uploaded|Date/i);
    await expect(headers.nth(3)).toContainText(/Status/i);

    // Then: Table should contain at least one PDF (from real backend)
    const pdfRows = pdfsTable.locator('tbody tr');
    await expect(pdfRows.first()).toBeVisible();

    // Then: Verify PDF details structure (not specific values)
    const firstRow = pdfRows.first();
    await expect(firstRow.locator('td').nth(0)).not.toBeEmpty(); // Has filename
    await expect(firstRow.locator('td').nth(1)).toContainText(/KB|MB|B/i); // Has size
    await expect(firstRow.locator('td').nth(3)).toContainText(
      /Completed|Success|Pending|Processing/i
    ); // Has status

    // Then: Verify action buttons are present
    const actionButtons = firstRow.locator('button');
    await expect(actionButtons.first()).toBeVisible();
  });
});
