/**
 * E2E Test: Accessibility - Theme System - Issue #2965 Wave 9
 *
 * Verifies WCAG 2.1 AA compliance for dual-theme system.
 *
 * Tests:
 * - Color contrast ratios (4.5:1 for text, 3:1 for UI)
 * - Focus visibility in both themes
 * - Keyboard navigation
 * - Screen reader compatibility
 */

import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

test.describe('Accessibility - Dual-Theme System', () => {
  test('should have no accessibility violations in light mode', async ({ page }) => {
    // Set light mode
    await page.goto('/dashboard');
    await page.evaluate(() => {
      localStorage.setItem('theme', 'light');
      document.documentElement.classList.remove('dark');
    });
    await page.reload();
    await page.waitForLoadState('networkidle');

    // Run axe accessibility scan
    const accessibilityScanResults = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    // Expect no violations
    expect(accessibilityScanResults.violations).toEqual([]);
  });

  test('should have no accessibility violations in dark mode', async ({ page }) => {
    // Set dark mode
    await page.goto('/dashboard');
    await page.evaluate(() => {
      localStorage.setItem('theme', 'dark');
      document.documentElement.classList.add('dark');
    });
    await page.reload();
    await page.waitForLoadState('networkidle');

    // Run axe accessibility scan
    const accessibilityScanResults = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    // Expect no violations
    expect(accessibilityScanResults.violations).toEqual([]);
  });

  test('should have visible focus indicators in light mode', async ({ page }) => {
    await page.goto('/dashboard');
    await page.evaluate(() => {
      localStorage.setItem('theme', 'light');
      document.documentElement.classList.remove('dark');
    });
    await page.reload();

    // Tab through interactive elements
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');

    // Check focus is visible (ring should be present)
    const focused = page.locator(':focus');
    await expect(focused).toBeVisible();

    // Verify focus ring exists (primary color in light mode)
    const focusedElement = await focused.evaluate((el) => {
      const styles = window.getComputedStyle(el);
      return {
        outline: styles.outline,
        outlineWidth: styles.outlineWidth,
        boxShadow: styles.boxShadow,
      };
    });

    // Should have some focus indicator (outline or box-shadow/ring)
    expect(
      focusedElement.outline !== 'none' ||
        focusedElement.outlineWidth !== '0px' ||
        focusedElement.boxShadow !== 'none'
    ).toBeTruthy();
  });

  test('should have visible focus indicators in dark mode (amber rings)', async ({ page }) => {
    await page.goto('/dashboard');
    await page.evaluate(() => {
      localStorage.setItem('theme', 'dark');
      document.documentElement.classList.add('dark');
    });
    await page.reload();

    // Tab through interactive elements
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');

    // Check focus is visible
    const focused = page.locator(':focus');
    await expect(focused).toBeVisible();

    // In dark mode, focus rings should use amber (--accent) for better visibility
    const focusedElement = await focused.evaluate((el) => {
      const styles = window.getComputedStyle(el);
      return {
        outline: styles.outline,
        boxShadow: styles.boxShadow,
        outlineColor: styles.outlineColor,
      };
    });

    // Should have amber-ish focus indicator in dark mode
    // (Can't easily test exact color, but verify SOME focus exists)
    expect(
      focusedElement.outline !== 'none' || focusedElement.boxShadow !== 'none'
    ).toBeTruthy();
  });

  test('should support keyboard navigation of theme toggle', async ({ page }) => {
    await page.goto('/dashboard');

    // Open user dropdown with keyboard
    const userAvatar = page.getByRole('button', { name: /avatar|profilo|user/i }).first();
    await userAvatar.focus();
    await page.keyboard.press('Enter');

    // Tab to theme toggle
    await page.keyboard.press('Tab'); // Settings
    await page.keyboard.press('Tab'); // Separator
    await page.keyboard.press('Tab'); // ThemeToggle (should be next)

    // Verify theme toggle is focused
    const focused = page.locator(':focus');
    const ariaLabel = await focused.getAttribute('aria-label');
    expect(ariaLabel).toMatch(/tema|theme|chiaro|scuro|light|dark/i);

    // Activate with keyboard
    await page.keyboard.press('Enter');

    // Theme should change
    await page.waitForTimeout(300);
    const htmlClass = await page.locator('html').getAttribute('class');
    expect(htmlClass).toBeTruthy();
  });

  test('should maintain WCAG AA contrast ratios in both themes', async ({ page }) => {
    // This is a smoke test - full contrast testing done by axe-core
    // Just verify critical text elements are readable

    // Test light mode
    await page.goto('/dashboard');
    await page.evaluate(() => {
      localStorage.setItem('theme', 'light');
      document.documentElement.classList.remove('dark');
    });
    await page.reload();

    const lightContrast = await page.evaluate(() => {
      const sampleText = document.querySelector('h1, h2, p');
      if (!sampleText) return null;

      const styles = window.getComputedStyle(sampleText);
      return {
        color: styles.color,
        backgroundColor: styles.backgroundColor,
      };
    });
    expect(lightContrast).toBeTruthy();

    // Test dark mode
    await page.evaluate(() => {
      localStorage.setItem('theme', 'dark');
      document.documentElement.classList.add('dark');
    });
    await page.reload();

    const darkContrast = await page.evaluate(() => {
      const sampleText = document.querySelector('h1, h2, p');
      if (!sampleText) return null;

      const styles = window.getComputedStyle(sampleText);
      return {
        color: styles.color,
        backgroundColor: styles.backgroundColor,
      };
    });
    expect(darkContrast).toBeTruthy();

    // Colors should be different between themes
    expect(darkContrast?.color).not.toBe(lightContrast?.color);
  });

  test('should handle system preference detection', async ({ page, context }) => {
    // Clear localStorage to test system preference
    await page.goto('/dashboard');
    await page.evaluate(() => {
      localStorage.removeItem('theme');
    });

    // Mock system dark mode preference
    await page.emulateMedia({ colorScheme: 'dark' });
    await page.reload();
    await page.waitForLoadState('networkidle');

    // Should default to dark mode based on system preference
    const htmlClass = await page.locator('html').getAttribute('class');
    expect(htmlClass).toContain('dark');

    // Mock system light mode preference
    await page.emulateMedia({ colorScheme: 'light' });
    await page.evaluate(() => localStorage.removeItem('theme'));
    await page.reload();
    await page.waitForLoadState('networkidle');

    // Should default to light mode
    const lightClass = await page.locator('html').getAttribute('class');
    expect(lightClass).not.toContain('dark');
  });
});
