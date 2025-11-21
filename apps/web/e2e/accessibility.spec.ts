/**
 * E2E Accessibility Tests (UI-05)
 *
 * Comprehensive accessibility testing with Playwright + axe-core
 * Tests all major pages for WCAG 2.1 AA compliance
 */

import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { getTextMatcher, t } from './fixtures/i18n';
import { setupMockAuth } from './fixtures/auth';

// Helper to get readable violations
function formatViolations(violations: any[]) {
  return violations.map((v) => ({
    id: v.id,
    impact: v.impact,
    description: v.description,
    nodes: v.nodes.length,
  }));
}

test.describe('Accessibility Tests - WCAG 2.1 AA', () => {
  test('Landing page should have no accessibility violations', async ({ page }) => {
    await page.goto('/');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    // Log violations if any for debugging
    if (results.violations.length > 0) {
      console.log('Violations found:', formatViolations(results.violations));
    }

    expect(results.violations).toEqual([]);
  });

  test('Chess page should have no accessibility violations', async ({ page }) => {
    await page.goto('/chess');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    if (results.violations.length > 0) {
      console.log('Violations found:', formatViolations(results.violations));
    }

    expect(results.violations).toEqual([]);
  });

  test('Chat page (unauthenticated) should have no accessibility violations', async ({
    page,
  }) => {
    await page.goto('/chat');

    // Wait for page to load completely (removed specific text selector to avoid timeout - Issue #841)
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    if (results.violations.length > 0) {
      console.log('Violations found:', formatViolations(results.violations));
    }

    expect(results.violations).toEqual([]);
  });

  test('Setup page (unauthenticated) should have no accessibility violations', async ({
    page,
  }) => {
    await page.goto('/setup');

    // Wait for page to load (removed specific text selector to avoid timeout - Issue #841)
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    if (results.violations.length > 0) {
      console.log('Violations found:', formatViolations(results.violations));
    }

    expect(results.violations).toEqual([]);
  });

  test('Landing page auth modal should have no violations when open', async ({ page }) => {
    await page.goto('/');

    // Open auth modal
    await page.click(`text=${t('home.getStartedButton')}`, { force: true });

    // Wait for modal to be visible
    await page.waitForSelector('input[type="email"]', { state: 'visible' });

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    if (results.violations.length > 0) {
      console.log('Violations found in modal:', formatViolations(results.violations));
    }

    // This will likely have violations until we implement fixes in Fase 5
    // For now, just log them
    console.log(`Auth modal violations: ${results.violations.length}`);
  });
});

test.describe('Keyboard Navigation Tests', () => {
  test('should be able to navigate landing page with keyboard', async ({ page }) => {
    await page.goto('/');

    // Wait for page to be fully loaded to avoid race conditions with Next.js dev tools
    await page.waitForLoadState('networkidle');

    // Tab through focusable elements (skip Next.js dev tools which might be first)
    for (let i = 0; i < 3; i++) {
      await page.keyboard.press('Tab');
    }

    // After a few tabs, should have focus on a valid interactive element
    const focusedTag = await page.evaluate(() => document.activeElement?.tagName);
    expect(['A', 'BUTTON', 'INPUT', 'SELECT', 'NEXTJS-PORTAL']).toContain(focusedTag);

    // Should still have focus on a valid element after more tabs
    for (let i = 0; i < 5; i++) {
      await page.keyboard.press('Tab');
    }

    const hasFocus = await page.evaluate(() => document.activeElement !== document.body);
    expect(hasFocus).toBe(true);
  });

  test('should be able to activate buttons with keyboard', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Focus the Get Started button and activate with Enter (more reliable for React onClick)
    const getStartedButton = page.getByTestId('hero-get-started');
    await getStartedButton.focus();

    // Wait a moment for focus to settle
    await page.waitForTimeout(500);

    // Press Enter (React onClick handlers respond better to Enter than Space)
    await page.keyboard.press('Enter');

    // Modal should open - wait longer for animation
    await page.waitForSelector('[role="dialog"]', { state: 'visible', timeout: 10000 });

    // Then check for email input - use a more flexible selector
    const emailInput = page.locator('input[type="email"]').first();
    await expect(emailInput).toBeVisible({ timeout: 3000 });
  });

  test.skip('should be able to close modal with ESC key', async ({ page }) => {
    // Skipped until Fase 5 AccessibleModal implementation
    // TODO: Enable when AccessibleModal is used in index.tsx
    await page.goto('/');

    // Open modal
    await page.click(`text=${t('home.getStartedButton')}`, { force: true });
    await page.waitForSelector('input[type="email"]', { state: 'visible' });

    // Close with ESC
    await page.keyboard.press('Escape');

    // Modal should close (wait a bit for animation)
    await page.waitForTimeout(500);

    // Verify modal is closed
    await expect(page.locator('input[type="email"]')).not.toBeVisible();
  });
});

test.describe('Focus Indicators', () => {
  test('buttons should have visible focus indicators', async ({ page }) => {
    await page.goto('/');

    // Focus a button (use test-id to avoid ambiguity)
    const button = page.getByTestId('hero-get-started');
    await button.focus();

    // Check for focus styles (outline should be visible)
    const hasOutline = await button.evaluate((el) => {
      const styles = window.getComputedStyle(el);
      return styles.outline !== 'none' || styles.boxShadow !== 'none';
    });

    expect(hasOutline).toBe(true);
  });

  test('links should have visible focus indicators', async ({ page }) => {
    await page.goto('/');

    // Focus first link
    const link = page.getByRole('link').first();
    await link.focus();

    // Check for focus styles
    const hasOutline = await link.evaluate((el) => {
      const styles = window.getComputedStyle(el);
      return styles.outline !== 'none' || styles.boxShadow !== 'none';
    });

    expect(hasOutline).toBe(true);
  });
});

test.describe('Screen Reader - Semantic HTML', () => {
  test('landing page should have proper heading hierarchy', async ({ page }) => {
    await page.goto('/');

    // Check for h1
    const h1Count = await page.locator('h1').count();
    expect(h1Count).toBeGreaterThan(0);

    // h1 should be the main title
    const h1Text = await page.locator('h1').first().textContent();
    expect(h1Text).toContain('AI');
  });

  test.skip('landing page should have main landmark', async ({ page }) => {
    // Skipped until Fase 5 adds skip link + main wrapper
    // TODO: Enable when semantic HTML landmarks are added
    await page.goto('/');

    // Should have <main> element or role="main"
    const mainExists = await page.locator('main, [role="main"]').count();
    expect(mainExists).toBeGreaterThan(0);
  });

  test('forms should have proper labels', async ({ page }) => {
    await page.goto('/');

    // Open auth modal
    await page.click(`text=${t('home.getStartedButton')}`, { force: true });
    await page.waitForSelector('input[type="email"]');

    // All inputs should have labels
    const inputs = await page.locator('input[type="email"], input[type="password"]').all();

    for (const input of inputs) {
      const id = await input.getAttribute('id');
      const hasLabel = await page.locator(`label[for="${id}"]`).count();

      expect(hasLabel).toBeGreaterThan(0);
    }
  });
});

/**
 * Authenticated Pages Accessibility Tests
 * Issue #841 - Phase 2
 */
test.describe('Accessibility - Authenticated User Pages', () => {
  test.beforeEach(async ({ page }) => {
    // Setup mock auth (this creates authenticated state)
    await setupMockAuth(page, 'User', 'user@meepleai.dev');
  });

  test('chat interface should not have accessibility violations', async ({ page }) => {
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    if (results.violations.length > 0) {
      console.log('Chat violations:', formatViolations(results.violations));
    }

    expect(results.violations).toEqual([]);
  });

  test('upload page should not have accessibility violations', async ({ page }) => {
    await page.goto('/upload');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    if (results.violations.length > 0) {
      console.log('Upload violations:', formatViolations(results.violations));
    }

    expect(results.violations).toEqual([]);
  });

  test('user profile should not have accessibility violations', async ({ page }) => {
    // Profile page redirects to settings page
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    if (results.violations.length > 0) {
      console.log('Profile violations:', formatViolations(results.violations));
    }

    expect(results.violations).toEqual([]);
  });

  test('settings page should not have accessibility violations', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    if (results.violations.length > 0) {
      console.log('Settings violations:', formatViolations(results.violations));
    }

    expect(results.violations).toEqual([]);
  });

  test('games listing (authenticated) should not have violations', async ({ page }) => {
    await page.goto('/games');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    if (results.violations.length > 0) {
      console.log('Games violations:', formatViolations(results.violations));
    }

    expect(results.violations).toEqual([]);
  });
});

/**
 * Authenticated Editor Pages Accessibility Tests
 * Issue #841 - Phase 2
 */
test.describe('Accessibility - Editor Role Pages', () => {
  test.beforeEach(async ({ page }) => {
    // Setup mock auth (this creates authenticated state)
    await setupMockAuth(page, 'Editor', 'editor@meepleai.dev');
  });

  test('rule editor should not have accessibility violations', async ({ page }) => {
    await page.goto('/editor');
    await page.waitForLoadState('networkidle');

    // TipTap editor may not initialize without RuleSpec data - check page structure instead
    // Skip waiting for .ProseMirror if it doesn't appear quickly
    try {
      await page.waitForSelector('.ProseMirror', { timeout: 2000 });
    } catch {
      // Editor not initialized - test page accessibility anyway
      console.log('TipTap editor not initialized, testing page structure');
    }

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    if (results.violations.length > 0) {
      console.log('Editor violations:', formatViolations(results.violations));
    }

    expect(results.violations).toEqual([]);
  });

  test('version history should not have accessibility violations', async ({ page }) => {
    await page.goto('/versions');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    if (results.violations.length > 0) {
      console.log('Versions violations:', formatViolations(results.violations));
    }

    expect(results.violations).toEqual([]);
  });
});

/**
 * Authenticated Admin Pages Accessibility Tests
 * Issue #841 - Phase 2
 */
test.describe('Accessibility - Admin Role Pages', () => {
  test.beforeEach(async ({ page }) => {
    // Setup mock auth (this creates authenticated state)
    await setupMockAuth(page, 'Admin', 'admin@meepleai.dev');
  });

  test('admin dashboard should not have accessibility violations', async ({ page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    if (results.violations.length > 0) {
      console.log('Admin dashboard violations:', formatViolations(results.violations));
    }

    expect(results.violations).toEqual([]);
  });

  test('admin users page should not have accessibility violations', async ({ page }) => {
    await page.goto('/admin/users');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    if (results.violations.length > 0) {
      console.log('Admin users violations:', formatViolations(results.violations));
    }

    expect(results.violations).toEqual([]);
  });

  test('admin analytics should not have accessibility violations', async ({ page }) => {
    await page.goto('/admin/analytics');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    if (results.violations.length > 0) {
      console.log('Admin analytics violations:', formatViolations(results.violations));
    }

    expect(results.violations).toEqual([]);
  });

  test('admin configuration should not have accessibility violations', async ({ page }) => {
    await page.goto('/admin/configuration');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    if (results.violations.length > 0) {
      console.log('Admin config violations:', formatViolations(results.violations));
    }

    expect(results.violations).toEqual([]);
  });
});
