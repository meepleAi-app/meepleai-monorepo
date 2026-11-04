/**
 * E2E Test: Document Source Filtering in Chat
 *
 * Issue #2051: Multi-document upload with source selector
 *
 * Covers the selector's empty state and its reset when the game changes. The
 * scenario that uploaded two PDFs first went through the retired PDF import
 * wizard and was removed with it (#4257).
 */

import { test, expect } from '@playwright/test';

test.describe('Document Source Filtering', () => {
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
