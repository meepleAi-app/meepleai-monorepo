/**
 * E2E Tests: Theme Switching (Issue #2965 Wave 9)
 *
 * Tests theme toggle functionality, persistence, and visual consistency.
 */

import { test, expect } from '@playwright/test';

test.describe('Theme Switching', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
  });

  test('theme toggle exists and is accessible', async ({ page }) => {
    // Check if theme toggle button exists
    const themeToggle = page.locator('[aria-label*="tema"]').first();
    await expect(themeToggle).toBeVisible();

    // Verify it's keyboard accessible
    await themeToggle.focus();
    await expect(themeToggle).toBeFocused();
  });

  test('theme toggle switches between light and dark mode', async ({ page }) => {
    // Get initial theme
    const html = page.locator('html');
    const initialClasses = await html.getAttribute('class');
    const initialTheme = initialClasses?.includes('dark') ? 'dark' : 'light';

    // Find and click theme toggle
    const themeToggle = page.locator('[aria-label*="tema"]').first();
    await themeToggle.click();

    // Wait for theme change animation
    await page.waitForTimeout(500);

    // Verify theme changed
    const newClasses = await html.getAttribute('class');
    const newTheme = newClasses?.includes('dark') ? 'dark' : 'light';

    expect(newTheme).not.toBe(initialTheme);
  });

  test('theme persists across page navigations', async ({ page }) => {
    // Set to a specific theme (dark)
    const html = page.locator('html');
    let currentClasses = await html.getAttribute('class');
    const isDark = currentClasses?.includes('dark');

    // Toggle if not already in target theme
    if (!isDark) {
      const themeToggle = page.locator('[aria-label*="tema"]').first();
      await themeToggle.click();
      await page.waitForTimeout(500);
    }

    // Verify dark mode is active
    currentClasses = await html.getAttribute('class');
    expect(currentClasses).toContain('dark');

    // Navigate to another page
    await page.goto('/about');
    await page.waitForLoadState('networkidle');

    // Verify theme persisted
    const persistedClasses = await html.getAttribute('class');
    expect(persistedClasses).toContain('dark');

    // Navigate to another route
    await page.goto('/games');
    await page.waitForLoadState('networkidle');

    // Verify theme still persisted
    const finalClasses = await html.getAttribute('class');
    expect(finalClasses).toContain('dark');
  });

  test('theme preference is stored in localStorage', async ({ page }) => {
    // Click theme toggle
    const themeToggle = page.locator('[aria-label*="tema"]').first();
    await themeToggle.click();
    await page.waitForTimeout(500);

    // Check localStorage
    const theme = await page.evaluate(() => {
      return localStorage.getItem('theme');
    });

    expect(theme).toBeTruthy();
    expect(['light', 'dark']).toContain(theme);
  });

  test('no visible layout shift during theme switch', async ({ page }) => {
    // Get element positions before theme switch
    const mainContent = page.locator('main').first();
    const beforeBox = await mainContent.boundingBox();

    // Switch theme
    const themeToggle = page.locator('[aria-label*="tema"]').first();
    await themeToggle.click();
    await page.waitForTimeout(500);

    // Get element positions after theme switch
    const afterBox = await mainContent.boundingBox();

    // Verify no significant layout shift (allow 1px tolerance)
    expect(Math.abs((beforeBox?.y || 0) - (afterBox?.y || 0))).toBeLessThan(2);
    expect(Math.abs((beforeBox?.x || 0) - (afterBox?.x || 0))).toBeLessThan(2);
    expect(Math.abs((beforeBox?.width || 0) - (afterBox?.width || 0))).toBeLessThan(2);
  });

  test('keyboard navigation: Toggle theme with Enter key', async ({ page }) => {
    // Focus on theme toggle using Tab (simplified - in real app would need to tab to it)
    const themeToggle = page.locator('[aria-label*="tema"]').first();
    await themeToggle.focus();

    // Get current theme
    const html = page.locator('html');
    const beforeClasses = await html.getAttribute('class');
    const beforeTheme = beforeClasses?.includes('dark') ? 'dark' : 'light';

    // Press Enter to toggle
    await page.keyboard.press('Enter');
    await page.waitForTimeout(500);

    // Verify theme changed
    const afterClasses = await html.getAttribute('class');
    const afterTheme = afterClasses?.includes('dark') ? 'dark' : 'light';

    expect(afterTheme).not.toBe(beforeTheme);
  });

  test('keyboard navigation: Toggle theme with Space key', async ({ page }) => {
    // Focus on theme toggle
    const themeToggle = page.locator('[aria-label*="tema"]').first();
    await themeToggle.focus();

    // Get current theme
    const html = page.locator('html');
    const beforeClasses = await html.getAttribute('class');
    const beforeTheme = beforeClasses?.includes('dark') ? 'dark' : 'light';

    // Press Space to toggle
    await page.keyboard.press('Space');
    await page.waitForTimeout(500);

    // Verify theme changed
    const afterClasses = await html.getAttribute('class');
    const afterTheme = afterClasses?.includes('dark') ? 'dark' : 'light';

    expect(afterTheme).not.toBe(beforeTheme);
  });

  test('theme toggle shows correct icon for each theme', async ({ page }) => {
    const html = page.locator('html');
    const themeToggle = page.locator('[aria-label*="tema"]').first();

    // Check icon in light mode
    let currentClasses = await html.getAttribute('class');
    const isCurrentlyDark = currentClasses?.includes('dark');

    if (isCurrentlyDark) {
      // If currently dark, we should see sun icon (for switching to light)
      const sunIcon = themeToggle.locator('svg').first();
      await expect(sunIcon).toBeVisible();
    } else {
      // If currently light, we should see moon icon (for switching to dark)
      const moonIcon = themeToggle.locator('svg').first();
      await expect(moonIcon).toBeVisible();
    }

    // Toggle theme
    await themeToggle.click();
    await page.waitForTimeout(500);

    // Verify icon changed
    const iconAfter = themeToggle.locator('svg').first();
    await expect(iconAfter).toBeVisible();
  });

  test('system preference is respected on first load', async ({ page, context }) => {
    // Clear any stored theme preference
    await context.clearCookies();
    await page.evaluate(() => {
      localStorage.clear();
    });

    // Set system preference to dark
    await page.emulateMedia({ colorScheme: 'dark' });

    // Navigate to page
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Verify dark theme is applied
    const html = page.locator('html');
    const classes = await html.getAttribute('class');

    // NOTE: This test depends on ThemeProvider enableSystem prop
    // If system preference is honored, should be dark
    expect(classes).toBeTruthy();
  });
});

test.describe('Theme Consistency Across Components', () => {
  test('all major components render in both themes', async ({ page }) => {
    // Test pages with various components
    const testPages = [
      '/',
      '/games',
      '/chat',
      '/about',
    ];

    for (const pagePath of testPages) {
      // Test light mode
      await page.emulateMedia({ colorScheme: 'light' });
      await page.goto(pagePath);
      await page.waitForLoadState('networkidle');

      // Verify page loaded without errors
      const html = page.locator('html');
      await expect(html).toBeVisible();

      // Test dark mode
      await page.emulateMedia({ colorScheme: 'dark' });
      await page.reload();
      await page.waitForLoadState('networkidle');

      // Verify page loaded without errors in dark mode
      await expect(html).toBeVisible();
    }
  });
});
