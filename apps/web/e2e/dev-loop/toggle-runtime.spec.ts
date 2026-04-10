import { test, expect } from '@playwright/test';

test.describe('@dev-loop Toggle runtime', () => {
  test('MSW group toggle disables/enables', async ({ page }) => {
    await page.goto('/?devpanel=1');
    await page.waitForSelector('[data-testid="dev-panel"]', { timeout: 15000 });

    // Find games toggle and click it off
    const gamesToggle = page.locator('[data-testid="toggle-msw-games"]');
    await expect(gamesToggle).toBeVisible();
    await gamesToggle.click();

    // Verify badge or state reflects change
    await expect(gamesToggle).toHaveAttribute('data-state', 'off');
  });
});
