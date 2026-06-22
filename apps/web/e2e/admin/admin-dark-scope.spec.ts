import { test, expect } from '@playwright/test';

test.describe('Admin dark theme scope (SP5 F0c)', () => {
  test.beforeEach(() => {
    process.env.PLAYWRIGHT_AUTH_BYPASS = 'true';
  });

  test('admin shell is scoped to dark', async ({ page }) => {
    await page.goto('/admin/overview');
    const shell = page.locator('[data-admin-shell]');
    await expect(shell).toHaveAttribute('data-theme', 'dark');
    // The shell background resolves to the dark token, not the light cream (#f7f3ee).
    const bg = await shell.evaluate(el => getComputedStyle(el).backgroundColor);
    expect(bg).not.toBe('rgb(247, 243, 238)');
  });

  test('a non-admin route stays light at the document root', async ({ page }) => {
    await page.goto('/dashboard');
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
    await expect(page.locator('[data-admin-shell]')).toHaveCount(0);
  });
});
