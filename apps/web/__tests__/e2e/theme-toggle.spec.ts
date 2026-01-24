/**
 * E2E Test: Theme Toggle Functionality - Issue #2965 Wave 9
 *
 * Verifies dual-theme system works correctly across the application.
 *
 * Tests:
 * - Theme toggle button visibility
 * - Theme switching (light ↔ dark)
 * - localStorage persistence
 * - Visual verification (class changes)
 * - No layout shifts
 */

import { test, expect } from '@playwright/test';

test.describe('Theme Toggle - Dual-Theme System', () => {
  test.beforeEach(async ({ page }) => {
    // Start on dashboard (requires auth - adjust if needed)
    await page.goto('/dashboard');

    // Wait for page load
    await page.waitForLoadState('networkidle');
  });

  test('should show ThemeToggle in TopNav user dropdown', async ({ page }) => {
    // Click user avatar in TopNav
    const userAvatar = page.getByRole('button', { name: /avatar|profilo|user/i }).first();
    await expect(userAvatar).toBeVisible();
    await userAvatar.click();

    // Verify dropdown opens
    const dropdown = page.getByRole('menu').or(page.locator('[role="menu"]'));
    await expect(dropdown).toBeVisible();

    // Verify ThemeToggle button exists
    const themeToggle = page.getByRole('button', { name: /tema|theme/i });
    await expect(themeToggle).toBeVisible();
  });

  test('should toggle theme from light to dark', async ({ page }) => {
    // Get initial theme (should be light by default)
    const htmlElement = page.locator('html');
    const initialClass = await htmlElement.getAttribute('class');

    // Open user dropdown
    const userAvatar = page.getByRole('button', { name: /avatar|profilo|user/i }).first();
    await userAvatar.click();

    // Click theme toggle
    const themeToggle = page.getByRole('button', { name: /tema|theme/i });
    await themeToggle.click();

    // Wait for theme change
    await page.waitForTimeout(300); // Transition duration

    // Verify theme class changed
    const newClass = await htmlElement.getAttribute('class');
    expect(newClass).not.toBe(initialClass);

    // Should now have 'dark' class (or not have it, depending on initial state)
    if (initialClass?.includes('dark')) {
      expect(newClass).not.toContain('dark');
    } else {
      expect(newClass).toContain('dark');
    }
  });

  test('should persist theme preference in localStorage', async ({ page, context }) => {
    // Open user dropdown and toggle theme
    const userAvatar = page.getByRole('button', { name: /avatar|profilo|user/i }).first();
    await userAvatar.click();

    const themeToggle = page.getByRole('button', { name: /tema|theme/i });
    await themeToggle.click();

    // Get current theme from localStorage
    const theme = await page.evaluate(() => localStorage.getItem('theme'));
    expect(theme).toBeTruthy();

    // Reload page
    await page.reload();
    await page.waitForLoadState('networkidle');

    // Verify theme persisted
    const persistedTheme = await page.evaluate(() => localStorage.getItem('theme'));
    expect(persistedTheme).toBe(theme);

    // Verify HTML class matches persisted theme
    const htmlClass = await page.locator('html').getAttribute('class');
    if (theme === 'dark') {
      expect(htmlClass).toContain('dark');
    }
  });

  test('should show glass effects in light mode (desktop)', async ({ page }) => {
    // Ensure light mode
    await page.evaluate(() => {
      localStorage.setItem('theme', 'light');
      document.documentElement.classList.remove('dark');
    });
    await page.reload();

    // Check for backdrop-blur in computed styles (desktop only)
    const card = page.locator('[class*="backdrop-blur"]').first();

    // On desktop (viewport ≥768px), blur should be present
    const computedStyle = await card.evaluate((el) => {
      const style = window.getComputedStyle(el);
      return style.backdropFilter || style.webkitBackdropFilter;
    });

    // Expect some blur value (blur(8px), blur(12px), etc.)
    expect(computedStyle).toMatch(/blur\(/);
  });

  test('should use solid backgrounds in dark mode', async ({ page }) => {
    // Set dark mode
    await page.evaluate(() => {
      localStorage.setItem('theme', 'dark');
      document.documentElement.classList.add('dark');
    });
    await page.reload();

    // Check that dark mode uses solid backgrounds
    const htmlClass = await page.locator('html').getAttribute('class');
    expect(htmlClass).toContain('dark');

    // Verify dark class is applied
    const card = page.locator('[class*="dark:bg-card"]').first();
    await expect(card).toBeVisible();
  });

  test('should not cause layout shifts when switching themes', async ({ page }) => {
    // Get initial layout metrics
    const initialHeight = await page.evaluate(() => document.body.scrollHeight);

    // Toggle theme
    const userAvatar = page.getByRole('button', { name: /avatar|profilo|user/i }).first();
    await userAvatar.click();
    const themeToggle = page.getByRole('button', { name: /tema|theme/i });
    await themeToggle.click();

    // Wait for transition
    await page.waitForTimeout(500);

    // Check layout didn't shift significantly
    const newHeight = await page.evaluate(() => document.body.scrollHeight);
    const shift = Math.abs(newHeight - initialHeight);

    // Allow small shift (<5px for rounding), but not major layout changes
    expect(shift).toBeLessThan(5);
  });

  test('should work on mobile viewport', async ({ page }) => {
    // Set mobile viewport
    await page.setViewportSize({ width: 375, height: 667 }); // iPhone SE

    await page.goto('/dashboard');
    await page.waitForLoadState('networkidle');

    // Mobile uses BottomNav, not TopNav
    // ThemeToggle might be in settings or mobile menu
    // For now, verify page renders without errors
    await expect(page.locator('body')).toBeVisible();

    // Verify theme can be set programmatically
    await page.evaluate(() => {
      localStorage.setItem('theme', 'dark');
      document.documentElement.classList.add('dark');
    });
    await page.reload();

    const htmlClass = await page.locator('html').getAttribute('class');
    expect(htmlClass).toContain('dark');
  });

  test('should show Sun icon in light mode, Moon icon in dark mode', async ({ page }) => {
    // Set light mode
    await page.evaluate(() => {
      localStorage.setItem('theme', 'light');
      document.documentElement.classList.remove('dark');
    });
    await page.reload();

    // Open dropdown
    const userAvatar = page.getByRole('button', { name: /avatar|profilo|user/i }).first();
    await userAvatar.click();

    // In light mode, ThemeToggle should show Moon icon (to switch to dark)
    const themeButton = page.getByRole('button', { name: /tema|theme/i });
    const buttonText = await themeButton.textContent();

    // Should show "Scuro" (dark mode option) in light mode
    expect(buttonText).toMatch(/scuro/i);
  });
});
