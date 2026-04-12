import { test, expect } from '@playwright/test';

test.describe('@dev-loop Inspector capture', () => {
  test('Inspector tab shows captured requests', async ({ page }) => {
    await page.goto('/?devpanel=1');
    await page.waitForSelector('[data-testid="dev-panel"]', { timeout: 15000 });

    // Navigate to Inspector tab
    await page.click('[data-testid="panel-tab-inspector"]');

    // Should have captured requests from page load
    await expect(page.locator('[data-testid="inspector-row"]').first()).toBeVisible({
      timeout: 5000,
    });
  });

  test('Clear button empties inspector', async ({ page }) => {
    await page.goto('/?devpanel=1');
    await page.waitForSelector('[data-testid="dev-panel"]', { timeout: 15000 });
    await page.click('[data-testid="panel-tab-inspector"]');

    await expect(page.locator('[data-testid="inspector-row"]').first()).toBeVisible({
      timeout: 5000,
    });
    await page.click('[data-testid="inspector-clear"]');

    await expect(page.locator('[data-testid="inspector-row"]')).toHaveCount(0);
  });
});
