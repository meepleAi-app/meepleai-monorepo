import { test, expect } from '@playwright/test';

test.describe('@dev-loop DevPanel opens', () => {
  test('Ctrl+Shift+M opens panel', async ({ page }) => {
    await page.goto('/');
    await page.waitForSelector('[data-testid="dev-badge"]', { timeout: 15000 });
    await page.keyboard.press('Control+Shift+m');
    await expect(page.locator('[data-testid="dev-panel"]')).toBeVisible({ timeout: 5000 });
  });

  test('?devpanel=1 URL opens panel and strips param', async ({ page }) => {
    await page.goto('/?devpanel=1');
    await page.waitForSelector('[data-testid="dev-badge"]', { timeout: 15000 });
    await expect(page.locator('[data-testid="dev-panel"]')).toBeVisible({ timeout: 5000 });
    await expect(page).toHaveURL(/^(?!.*devpanel)/);
  });

  test('DevBadge click opens panel', async ({ page }) => {
    await page.goto('/');
    await page.waitForSelector('[data-testid="dev-badge"]', { timeout: 15000 });
    await page.click('[data-testid="dev-badge"]');
    await expect(page.locator('[data-testid="dev-panel"]')).toBeVisible({ timeout: 5000 });
  });

  test('Escape closes panel', async ({ page }) => {
    await page.goto('/?devpanel=1');
    await page.waitForSelector('[data-testid="dev-panel"]', { timeout: 15000 });
    await page.keyboard.press('Escape');
    await expect(page.locator('[data-testid="dev-panel"]')).not.toBeVisible();
  });
});
