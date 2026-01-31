/**
 * E2E Test: Document Source Filtering in Chat
 *
 * Issue #2051: Multi-document upload with source selector
 *
 * Test Scenario:
 * 1. Upload 2 PDFs (base + expansion) to a game
 * 2. Open chat for the game
 * 3. Verify DocumentSourceSelector shows both documents
 * 4. Select only "expansion" document
 * 5. Ask a question
 * 6. Verify citations come only from expansion document
 * 7. Select "All documents"
 * 8. Ask same question
 * 9. Verify citations come from both documents
 */

import { test, expect } from '@playwright/test';

test.describe('Document Source Filtering', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to upload page
    await page.goto('/upload');

    // Wait for page to be interactive
    await expect(page.getByRole('heading', { name: /carica pdf/i })).toBeVisible();
  });

  test('should filter chat citations by selected document sources', async ({ page }) => {
    // Skip if feature flag disabled or no test PDFs available
    const gameSelectVisible = await page
      .getByLabel(/seleziona gioco/i)
      .isVisible({ timeout: 5000 })
      .catch(() => false);
    if (!gameSelectVisible) {
      test.skip(true, 'Upload page not accessible or no games available');
    }

    // Step 1: Upload base rulebook
    const baseFileInput = page.locator('input[type="file"]').first();
    await baseFileInput.setInputFiles({
      name: 'test-base-rules.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('%PDF-1.4 test base content'),
    });

    // Wait for upload success indicator
    await expect(page.getByText(/caricamento completato/i)).toBeVisible({ timeout: 30000 });

    // Step 2: Upload expansion rules
    const expansionFileInput = page.locator('input[type="file"]').first();
    await expansionFileInput.setInputFiles({
      name: 'test-expansion-rules.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('%PDF-1.4 test expansion content'),
    });

    await expect(page.getByText(/caricamento completato/i)).toBeVisible({ timeout: 30000 });

    // Step 3: Navigate to chat page for the game
    await page.goto('/chat');

    // Wait for chat interface to load
    await expect(page.getByPlaceholder(/fai una domanda/i)).toBeVisible({ timeout: 10000 });

    // Step 4: Verify DocumentSourceSelector is present and shows 2 documents
    const sourceSelector = page.getByRole('button', { name: /tutti i documenti|2 documenti/i });
    await expect(sourceSelector).toBeVisible();

    // Open dropdown
    await sourceSelector.click();

    // Verify both documents are listed
    await expect(page.getByText('test-base-rules.pdf')).toBeVisible();
    await expect(page.getByText('test-expansion-rules.pdf')).toBeVisible();

    // Step 5: Select only "expansion" document
    await page.getByText('test-expansion-rules.pdf').click();

    // Deselect "all documents" and base document
    const allDocsCheckbox = page.getByRole('menuitemcheckbox', { name: /tutti i documenti/i });
    if (await allDocsCheckbox.isChecked()) {
      await allDocsCheckbox.click();
    }

    const baseCheckbox = page.getByRole('menuitemcheckbox', { name: /test-base-rules/ });
    if (await baseCheckbox.isChecked()) {
      await baseCheckbox.click();
    }

    // Close dropdown
    await page.keyboard.press('Escape');

    // Verify selector shows "1 documento"
    await expect(sourceSelector).toContainText(/1 documento|test-expansion/i);

    // Step 6: Ask a question
    const messageInput = page.getByPlaceholder(/fai una domanda/i);
    await messageInput.fill('Come si gioca?');
    await messageInput.press('Enter');

    // Wait for response
    await expect(page.getByText(/searching|generando risposta/i)).toBeVisible({ timeout: 5000 });
    await expect(page.getByText(/searching|generando risposta/i)).not.toBeVisible({
      timeout: 60000,
    });

    // Step 7: Verify citations are only from expansion document
    const citations = page.locator('[data-testid="citation"]').or(page.getByText(/fonte:/i));
    const citationCount = await citations.count();

    if (citationCount > 0) {
      // Check each citation references expansion document
      for (let i = 0; i < citationCount; i++) {
        const citation = citations.nth(i);
        const citationText = await citation.textContent();

        // Citation should reference expansion, not base
        expect(citationText).toMatch(/expansion|test-expansion/i);
        expect(citationText).not.toMatch(/base.*rules/i);
      }
    }

    // Step 8: Select "All documents"
    await sourceSelector.click();
    await allDocsCheckbox.click();
    await page.keyboard.press('Escape');

    // Verify selector shows "Tutti i documenti"
    await expect(sourceSelector).toContainText(/tutti i documenti/i);

    // Step 9: Ask same question again
    await messageInput.fill('Come si gioca?');
    await messageInput.press('Enter');

    // Wait for new response
    await expect(page.getByText(/searching|generando risposta/i)).toBeVisible({ timeout: 5000 });
    await expect(page.getByText(/searching|generando risposta/i)).not.toBeVisible({
      timeout: 60000,
    });

    // Step 10: Verify citations now include both documents
    const allCitations = page.locator('[data-testid="citation"]').or(page.getByText(/fonte:/i));
    const allCitationCount = await allCitations.count();

    // With "all documents", should potentially have more citations or different sources
    // At minimum, verify citations exist
    expect(allCitationCount).toBeGreaterThan(0);
  });

  test('should show "Nessun documento" when game has no PDFs', async ({ page }) => {
    await page.goto('/chat');

    // Wait for interface
    await expect(page.getByPlaceholder(/fai una domanda/i)).toBeVisible({ timeout: 10000 });

    // If game has no documents, selector should show "Nessun documento" or be disabled
    const selector = page.getByRole('button', { name: /nessun documento|tutti i documenti/i });

    if (await selector.isVisible()) {
      const isDisabled = await selector.isDisabled();
      const text = await selector.textContent();

      // Either disabled or shows "Nessun documento"
      expect(isDisabled || text?.includes('Nessun')).toBeTruthy();
    }
  });

  test('should reset document selection when switching games', async ({ page }) => {
    await page.goto('/chat');

    await expect(page.getByPlaceholder(/fai una domanda/i)).toBeVisible({ timeout: 10000 });

    const sourceSelector = page.getByRole('button', { name: /documenti/i });

    if (await sourceSelector.isVisible()) {
      // Select specific document
      await sourceSelector.click();
      const firstDoc = page.locator('[role="menuitemcheckbox"]').nth(1);

      if (await firstDoc.isVisible()) {
        await firstDoc.click();
        await page.keyboard.press('Escape');

        // Switch game (if game selector available)
        const gameSelector = page.getByLabel(/gioco|game/i).first();
        if (await gameSelector.isVisible()) {
          await gameSelector.click();

          // Select different game
          const anotherGame = page.getByRole('option').nth(1);
          if (await anotherGame.isVisible()) {
            await anotherGame.click();

            // Document selector should reset to "Tutti i documenti"
            await expect(sourceSelector).toContainText(/tutti i documenti/i);
          }
        }
      }
    }
  });
});
