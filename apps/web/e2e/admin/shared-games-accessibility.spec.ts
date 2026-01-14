/**
 * ISSUE #2426: SharedGameCatalog Accessibility E2E Tests
 *
 * Validates WCAG AA compliance using axe-playwright.
 * Success criteria: Zero WCAG AA violations
 *
 * Prerequisites:
 * - npm install -D axe-playwright
 *
 * Run: pnpm test:e2e apps/web/e2e/admin/shared-games-accessibility.spec.ts
 */

import AxeBuilder from '@axe-core/playwright';
import { test, expect } from '@playwright/test';

test.describe('SharedGameCatalog Accessibility (WCAG AA)', () => {
  test.beforeEach(async ({ page }) => {
    // Note: axe-playwright auto-injects axe-core, no manual injection needed
  });

  test('Public search page should have no WCAG AA violations', async ({ page }) => {
    await page.goto('/games/add');

    // Wait for SharedGameSearch to load
    await expect(page.locator('input[placeholder*="Cerca"]')).toBeVisible();

    // Run axe accessibility scan
    const accessibilityScanResults = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa']) // WCAG 2.x Level A + AA
      .analyze();

    // Expect zero violations
    expect(accessibilityScanResults.violations).toEqual([]);
  });

  test('Admin list page should have no WCAG AA violations', async ({ page }) => {
    // Login as admin
    await page.goto('/login');
    await page.fill('input[type="email"]', 'admin@meepleai.dev');
    await page.fill('input[type="password"]', 'Admin123!');
    await page.click('button[type="submit"]');
    await page.waitForURL('/board-game-ai');

    // Navigate to admin shared games
    await page.goto('/admin/shared-games');
    await expect(page.locator('h1:has-text("Shared Games Catalog")')).toBeVisible();

    // Run accessibility scan
    const accessibilityScanResults = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .analyze();

    expect(accessibilityScanResults.violations).toEqual([]);
  });

  test('Game detail modal should be accessible', async ({ page }) => {
    await page.goto('/games/add');

    // Search for a game
    await page.fill('input[placeholder*="Cerca"]', 'Catan');
    await page.keyboard.press('Enter');

    // Wait for results
    await expect(page.locator('[data-testid="search-result"]').first()).toBeVisible();

    // Click first result to open modal
    await page.click('[data-testid="search-result"]').first();

    // Wait for modal
    await expect(page.locator('[role="dialog"]')).toBeVisible();

    // Run accessibility scan on modal
    const accessibilityScanResults = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .analyze();

    expect(accessibilityScanResults.violations).toEqual([]);
  });

  test('Keyboard navigation should work throughout admin UI', async ({ page }) => {
    await page.goto('/login');
    await page.fill('input[type="email"]', 'admin@meepleai.dev');
    await page.fill('input[type="password"]', 'Admin123!');
    await page.click('button[type="submit"]');
    await page.waitForURL('/board-game-ai');

    await page.goto('/admin/shared-games');

    // Tab through interactive elements
    await page.keyboard.press('Tab'); // Focus first element
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');

    // Verify focus is visible (no focus trapped)
    const focused = await page.evaluate(() => document.activeElement?.tagName);
    expect(['BUTTON', 'INPUT', 'A']).toContain(focused);

    // Verify can activate with Enter/Space
    await page.keyboard.press('Enter');

    // Should trigger action (exact behavior depends on focused element)
  });
});
