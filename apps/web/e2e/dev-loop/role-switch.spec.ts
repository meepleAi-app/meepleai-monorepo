import { test, expect } from '@playwright/test';

test.describe('@dev-loop Role switch', () => {
  test('changing role updates badge', async ({ page }) => {
    await page.goto('/?devpanel=1');
    await page.waitForSelector('[data-testid="dev-panel"]', { timeout: 15000 });

    // Navigate to Auth tab
    await page.click('[data-testid="panel-tab-auth"]');
    const select = page.locator('[data-testid="role-select"]');
    await expect(select).toBeVisible();

    // If we're on small-library scenario, Admin should be available
    await select.selectOption('Admin');

    // Verify badge shows Admin
    await expect(page.locator('[data-testid="dev-badge"]')).toContainText('Admin', {
      timeout: 5000,
    });
  });
});
