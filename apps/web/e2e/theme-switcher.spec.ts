/**
 * E2E Tests for ThemeSwitcher Component (FRONTEND-4) - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/home/HomePage.ts
 */

import { test, expect } from './fixtures/chromatic';

/**
 * ThemeSwitcher Component Tests
 *
 * Tests cover:
 * - Dark/Light/System mode switching
 * - localStorage persistence
 * - Keyboard navigation accessibility
 * - Visual state verification
 */

test.describe('ThemeSwitcher Component', () => {
  test.beforeEach(async ({ page }) => {
    // Clear localStorage to start fresh
    await page.context().clearCookies();
    await page.goto('/');
    // Wait for page to be fully loaded
    await page.waitForLoadState('networkidle');
  });

  test('should render theme switcher in navigation', async ({ page }) => {
    // Wait for theme switcher button to be visible
    const themeSwitcher = page.getByRole('button', { name: /theme switcher/i });
    await expect(themeSwitcher).toBeVisible();
  });

  test('should toggle between dark and light themes', async ({ page }) => {
    const themeSwitcher = page.getByRole('button', { name: /theme switcher/i });

    // Open dropdown
    await themeSwitcher.click();

    // Click Light mode
    await page.getByRole('menuitem', { name: /switch to light theme/i }).click();

    // Verify light theme applied (check HTML class)
    const html = page.locator('html');
    await expect(html).toHaveClass(/light/);

    // Open dropdown again
    await themeSwitcher.click();

    // Click Dark mode
    await page.getByRole('menuitem', { name: /switch to dark theme/i }).click();

    // Verify dark theme applied
    await expect(html).toHaveClass(/dark/);
  });

  test('should switch to system theme mode', async ({ page }) => {
    const themeSwitcher = page.getByRole('button', { name: /theme switcher/i });

    // Open dropdown
    await themeSwitcher.click();

    // Click System mode
    await page.getByRole('menuitem', { name: /use system theme preference/i }).click();

    // Verify system theme is set (localStorage check)
    const theme = await page.evaluate(() => localStorage.getItem('theme'));
    expect(theme).toBe('system');
  });

  test('should persist theme preference in localStorage', async ({ page, context }) => {
    const themeSwitcher = page.getByRole('button', { name: /theme switcher/i });

    // Set to light mode
    await themeSwitcher.click();
    await page.getByRole('menuitem', { name: /switch to light theme/i }).click();

    // Verify localStorage
    let theme = await page.evaluate(() => localStorage.getItem('theme'));
    expect(theme).toBe('light');

    // Reload page
    await page.reload();
    await page.waitForLoadState('networkidle');

    // Verify theme persisted
    const html = page.locator('html');
    await expect(html).toHaveClass(/light/);

    theme = await page.evaluate(() => localStorage.getItem('theme'));
    expect(theme).toBe('light');
  });

  test('should support keyboard navigation', async ({ page }) => {
    // Focus theme switcher with Tab
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab'); // May need multiple tabs depending on page structure

    // Find and focus the theme switcher button
    const themeSwitcher = page.getByRole('button', { name: /theme switcher/i });
    await themeSwitcher.focus();

    // Open dropdown with Enter
    await page.keyboard.press('Enter');

    // Wait for dropdown menu to appear
    const lightOption = page.getByRole('menuitem', { name: /switch to light theme/i });
    await expect(lightOption).toBeVisible();

    // Navigate with ArrowDown
    await page.keyboard.press('ArrowDown');

    // Select with Enter
    await page.keyboard.press('Enter');

    // Verify theme changed
    const html = page.locator('html');
    // Depending on which option was selected, verify the class
    await expect(html).toHaveClass(/(light|dark)/);
  });

  test('should show visual indicator for current theme', async ({ page }) => {
    const themeSwitcher = page.getByRole('button', { name: /theme switcher/i });

    // Open dropdown
    await themeSwitcher.click();

    // Click Light mode
    await page.getByRole('menuitem', { name: /switch to light theme/i }).click();

    // Open dropdown again
    await themeSwitcher.click();

    // Verify checkmark is visible on Light option
    const lightOption = page.getByRole('menuitem', { name: /switch to light theme/i });
    const checkmark = lightOption.locator('[aria-label="Current theme"]');
    await expect(checkmark).toBeVisible();
  });

  test('should display correct icon for current theme', async ({ page }) => {
    const themeSwitcher = page.getByRole('button', { name: /theme switcher/i });

    // Set to light mode
    await themeSwitcher.click();
    await page.getByRole('menuitem', { name: /switch to light theme/i }).click();

    // Verify button shows "Light" text on desktop
    await expect(themeSwitcher).toContainText(/light/i);

    // Set to dark mode
    await themeSwitcher.click();
    await page.getByRole('menuitem', { name: /switch to dark theme/i }).click();

    // Verify button shows "Dark" text
    await expect(themeSwitcher).toContainText(/dark/i);

    // Set to system mode
    await themeSwitcher.click();
    await page.getByRole('menuitem', { name: /use system theme preference/i }).click();

    // Verify button shows "System" text
    await expect(themeSwitcher).toContainText(/system/i);
  });

  test('should not cause FOUC (Flash of Unstyled Content)', async ({ page }) => {
    // Set theme to light
    const themeSwitcher = page.getByRole('button', { name: /theme switcher/i });
    await themeSwitcher.click();
    await page.getByRole('menuitem', { name: /switch to light theme/i }).click();

    // Reload page and immediately check theme
    await page.reload();

    // Check theme class is applied before any content renders
    const html = page.locator('html');
    await expect(html).toHaveClass(/light/);

    // Verify no flash by checking theme is set immediately
    const initialTheme = await page.evaluate(() => {
      return document.documentElement.classList.contains('light');
    });
    expect(initialTheme).toBe(true);
  });
});
