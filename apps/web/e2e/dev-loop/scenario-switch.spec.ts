import { test, expect } from '@playwright/test';

test.describe('@dev-loop Scenario switch', () => {
  test('switching scenario updates badge text', async ({ page }) => {
    await page.goto('/?devpanel=1');
    await page.waitForSelector('[data-testid="dev-panel"]', { timeout: 15000 });

    // Navigate to Scenarios tab
    await page.click('[data-testid="panel-tab-scenarios"]');
    const select = page.locator('[data-testid="scenario-select"]');
    await expect(select).toBeVisible();

    // Switch to admin-busy
    await select.selectOption('admin-busy');

    // Wait for switch to complete and verify badge updates
    await expect(page.locator('[data-testid="dev-badge"]')).toContainText('admin-busy', {
      timeout: 5000,
    });
  });
});
