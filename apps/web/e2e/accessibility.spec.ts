/**
 * E2E Accessibility Tests (Issue #2965 Wave 9)
 *
 * Tests WCAG 2.1 AA compliance using axe-core in both light and dark modes.
 */

import AxeBuilder from '@axe-core/playwright';
import { test, expect } from '@playwright/test';

test.describe('Accessibility - Light Mode', () => {
  test.beforeEach(async ({ page }) => {
    // Ensure light mode
    await page.emulateMedia({ colorScheme: 'light' });
  });

  test('homepage has no accessibility violations (light mode)', async ({ page }) => {
    await page.goto('/');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    expect(results.violations).toEqual([]);
  });

  test('games page has no accessibility violations (light mode)', async ({ page }) => {
    await page.goto('/games');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    expect(results.violations).toEqual([]);
  });
});

test.describe('Accessibility - Dark Mode', () => {
  test.beforeEach(async ({ page }) => {
    // Ensure dark mode
    await page.emulateMedia({ colorScheme: 'dark' });
  });

  test('homepage has no accessibility violations (dark mode)', async ({ page }) => {
    await page.goto('/');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    expect(results.violations).toEqual([]);
  });

  test('contrast ratios meet WCAG AA standards (dark mode)', async ({ page }) => {
    await page.goto('/');

    const results = await new AxeBuilder({ page })
      .withTags(['cat.color'])
      .analyze();

    expect(results.violations).toEqual([]);
  });
});
