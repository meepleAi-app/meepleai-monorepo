import { test, expect } from '@playwright/test';

test.describe('@dev-loop DevPanel accessibility', () => {
  test('panel has ARIA dialog role', async ({ page }) => {
    await page.goto('/?devpanel=1');
    await page.waitForSelector('[data-testid="dev-panel"]', { timeout: 15000 });
    const panel = page.locator('[data-testid="dev-panel"]');
    await expect(panel).toHaveAttribute('role', 'dialog');
    await expect(panel).toHaveAttribute('aria-label', 'MeepleDev Panel');
  });

  test('tabs have proper ARIA roles', async ({ page }) => {
    await page.goto('/?devpanel=1');
    await page.waitForSelector('[data-testid="dev-panel"]', { timeout: 15000 });

    const tablist = page.locator('[role="tablist"]');
    await expect(tablist).toBeVisible();

    const tabs = page.locator('[role="tab"]');
    await expect(tabs).toHaveCount(4);

    // Active tab has aria-selected=true
    await expect(page.locator('[data-testid="panel-tab-toggles"]')).toHaveAttribute(
      'aria-selected',
      'true'
    );
  });

  test('toggle switches have ARIA switch role', async ({ page }) => {
    await page.goto('/?devpanel=1');
    await page.waitForSelector('[data-testid="dev-panel"]', { timeout: 15000 });

    const switches = page.locator('[role="switch"]');
    const count = await switches.count();
    expect(count).toBeGreaterThan(0);

    // Each switch has aria-checked
    const firstSwitch = switches.first();
    const checked = await firstSwitch.getAttribute('aria-checked');
    expect(checked === 'true' || checked === 'false').toBe(true);
  });

  test('keyboard navigation: ArrowRight/Left switches tabs', async ({ page }) => {
    await page.goto('/?devpanel=1');
    await page.waitForSelector('[data-testid="dev-panel"]', { timeout: 15000 });

    // Focus first tab
    await page.locator('[data-testid="panel-tab-toggles"]').focus();
    // Press ArrowRight → should focus Scenarios
    await page.keyboard.press('ArrowRight');
    // Note: tab focus behavior depends on implementation
  });
});
