import { expect, test } from './fixtures';
import { AuthHelper, USER_FIXTURES } from './pages';

test.describe('Add Game via BGG', () => {
  test.beforeEach(async ({ page }) => {
    const authHelper = new AuthHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
  });

  test('should search and add a game via BGG', async ({ page }) => {
    // 1. Navigate to Library page
    await page.goto('/library');

    // 2. Click "Aggiungi Gioco"
    await page.click('text=Aggiungi Gioco');
    await expect(page).toHaveURL(/\/library/);

    // 3. Search for a common game (real BGG API will return results)
    await page.fill('input[placeholder="Cerca su BoardGameGeek..."]', 'Catan');
    await page.click('button:has-text("Cerca")');

    // 4. Verify results appear (from real BGG API)
    const results = page.locator('.grid > div');
    await expect(results.first()).toBeVisible({ timeout: 10000 });

    // Results should contain game information
    await expect(results.first()).toContainText(/Catan/i);
    await expect(results.first()).toContainText(/\d{4}/); // Year pattern

    // 5. Add first game from results
    await results
      .first()
      .getByRole('button', { name: /Aggiungi/i })
      .click();

    // 6. Verify redirection to library page and success feedback
    await expect(page).toHaveURL(/\/(library)$/, { timeout: 10000 });
    await expect(page.locator('text=/Gioco aggiunto|successo|added/i')).toBeVisible({
      timeout: 5000,
    });
  });

  test('should show empty state when no results found', async ({ page }) => {
    await page.goto('/library');

    // Search for non-existent game (real BGG API will return no results)
    await page.fill('input[placeholder="Cerca su BoardGameGeek..."]', 'XYZ123NonExistentGame999');
    await page.click('button:has-text("Cerca")');

    // Verify empty state or no results message
    await expect(page.locator('text=/Nessun risultato|No results|not found/i')).toBeVisible({
      timeout: 10000,
    });
  });
});
