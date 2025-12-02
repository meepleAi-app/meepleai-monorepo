/**
 * E2E Accessibility Tests (UI-05)
 *
 * Comprehensive accessibility testing with Playwright + axe-core
 * Tests all major pages for WCAG 2.1 AA compliance
 */

/**
 * E2E Accessibility Tests (UI-05) - MIGRATED TO POM
 *
 * Comprehensive accessibility testing with Playwright + axe-core
 * Tests all major pages for WCAG 2.1 AA compliance
 *
 * @see apps/web/e2e/pages/ - Page Object Model architecture
 */

import { test, expect } from './fixtures/chromatic';
import AxeBuilder from '@axe-core/playwright';
import type { Result } from 'axe-core';
import { getTextMatcher, t } from './fixtures/i18n';
import { AuthHelper } from './pages';
import { setupMockAuth } from './fixtures/auth';

// Helper to get readable violations
function formatViolations(violations: Result[]) {
  return violations.map(v => ({
    id: v.id,
    impact: v.impact,
    description: v.description,
    nodes: v.nodes.length,
    helpUrl: v.helpUrl,
  }));
}

/**
 * Known accessibility issues to exclude temporarily
 * These rules are disabled while the underlying UI issues are being fixed
 * TODO: Remove these exclusions as issues are resolved
 *
 * - color-contrast: Multiple components have insufficient color contrast (Issue #1868)
 * - aria-allowed-attr: Chat textarea has invalid aria-describedby (Issue #1868)
 */
const KNOWN_A11Y_ISSUES = ['color-contrast', 'aria-allowed-attr'];

/**
 * CI Environment Detection
 * Issue #1868: Some interactive tests have timing issues in CI due to:
 * - Production server vs dev server behavior differences
 * - Modal animations and focus management timing
 * - Network idle detection variations
 */
const isCI = process.env.CI === 'true';

/**
 * Helper function to test page accessibility (Issue #841 - reduce code duplication)
 *
 * @param page - Playwright page object
 * @param url - URL to test
 * @param pageName - Descriptive page name for error messages
 * @param options - Optional configuration
 */
async function testPageAccessibility(
  page: import('@playwright/test').Page,
  url: string,
  pageName: string,
  options: {
    waitForNetworkIdle?: boolean;
    setupAuth?: () => Promise<void>;
    customSelector?: string;
    customAction?: () => Promise<void>;
  } = {}
) {
  await page.goto(url);

  // Wait for network idle if requested
  if (options.waitForNetworkIdle) {
    await page.waitForLoadState('networkidle');
  }

  // Wait for custom selector if provided
  if (options.customSelector) {
    await page.waitForSelector(options.customSelector, { state: 'visible' });
  }

  // Execute custom action if provided (e.g., open modal)
  if (options.customAction) {
    await options.customAction();
  }

  // Run axe accessibility analysis
  // Exclude known issues temporarily (see KNOWN_A11Y_ISSUES comment)
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .disableRules(KNOWN_A11Y_ISSUES)
    .analyze();

  // Log violations if found
  if (results.violations.length > 0) {
    console.error(`❌ ${pageName} violations:`, formatViolations(results.violations));
  }

  // Assert no violations
  expect(results.violations).toEqual([]);
}

test.describe('Accessibility Tests - WCAG 2.1 AA', () => {
  test('Landing page should have no accessibility violations', async ({ page }) => {
    await testPageAccessibility(page, '/', 'Landing page');
  });

  test('Chess page should have no accessibility violations', async ({ page }) => {
    await testPageAccessibility(page, '/chess', 'Chess page');
  });

  test('Chat page (unauthenticated) should have no accessibility violations', async ({ page }) => {
    await testPageAccessibility(page, '/chat', 'Chat page', { waitForNetworkIdle: true });
  });

  test('Setup page (unauthenticated) should have no accessibility violations', async ({ page }) => {
    await testPageAccessibility(page, '/setup', 'Setup page', { waitForNetworkIdle: true });
  });

  // Issue #1868: Skip in CI - modal timing issues with production server
  // TODO: Re-enable when modal focus management is stabilized
  test.skip(
    isCI,
    'Landing page auth modal should have no violations when open',
    async ({ page }) => {
      await testPageAccessibility(page, '/', 'Auth modal', {
        customAction: async () => {
          // Open auth modal - wait for button to be clickable (Issue #841 - removed force: true)
          const getStartedButton = page.locator(`text=${t('home.getStartedButton')}`);
          await getStartedButton.waitFor({ state: 'visible' });
          await getStartedButton.click();

          // Wait for modal to be visible
          await page.waitForSelector('input[type="email"]', { state: 'visible' });
        },
      });
    }
  );
});

test.describe('Keyboard Navigation Tests', () => {
  // Issue #1868: Skip in CI - networkidle timing issues with production server
  // TODO: Re-enable when keyboard navigation tests are stabilized for CI
  test.skip(isCI, 'should be able to navigate landing page with keyboard', async ({ page }) => {
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

  // Issue #1868: Skip in CI - modal/dialog timing issues with production server
  // TODO: Re-enable when button activation tests are stabilized for CI
  test.skip(isCI, 'should be able to activate buttons with keyboard', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Focus the Get Started button and activate with Enter (more reliable for React onClick)
    const getStartedButton = page.getByTestId('hero-get-started');
    await getStartedButton.focus();

    // Issue #841: Replace waitForTimeout with explicit wait for element stability
    await expect(getStartedButton).toBeFocused();

    // Press Enter (React onClick handlers respond better to Enter than Space)
    await page.keyboard.press('Enter');

    // Modal should open - wait for animation to complete
    await page.waitForSelector('[role="dialog"]', { state: 'visible', timeout: 10000 });

    // Then check for email input - use a more flexible selector
    const emailInput = page.locator('input[type="email"]').first();
    await expect(emailInput).toBeVisible({ timeout: 3000 });
  });

  test.skip('should be able to close modal with ESC key', async ({ page }) => {
    // Skipped until Fase 5 AccessibleModal implementation
    // TODO: Enable when AccessibleModal is used in index.tsx
    await page.goto('/');

    // Open modal - wait for button to be clickable (Issue #841 - removed force: true)
    const getStartedButton = page.locator(`text=${t('home.getStartedButton')}`);
    await getStartedButton.waitFor({ state: 'visible' });
    await getStartedButton.click();
    await page.waitForSelector('input[type="email"]', { state: 'visible' });

    // Close with ESC
    await page.keyboard.press('Escape');

    // Issue #841: Replace waitForTimeout - wait explicitly for modal to close
    await page.waitForSelector('input[type="email"]', { state: 'hidden', timeout: 3000 });

    // Verify modal is closed
    await expect(page.locator('input[type="email"]')).not.toBeVisible();
  });
});

test.describe('Focus Indicators', () => {
  // Issue #1868: Skip in CI - focus indicator evaluation timing issues
  // TODO: Re-enable when focus indicator tests are stabilized for CI
  test.skip(isCI, 'buttons should have visible focus indicators', async ({ page }) => {
    await page.goto('/');

    // Focus a button (use test-id to avoid ambiguity)
    const button = page.getByTestId('hero-get-started');
    await button.focus();

    // Check for focus styles (outline should be visible)
    const hasOutline = await button.evaluate(el => {
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
    const hasOutline = await link.evaluate(el => {
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

  // Issue #1868: Skip in CI - modal opening timing issues with production server
  // TODO: Re-enable when form label tests are stabilized for CI
  test.skip(isCI, 'forms should have proper labels', async ({ page }) => {
    await page.goto('/');

    // Open auth modal - wait for button to be clickable (Issue #841 - removed force: true)
    const getStartedButton = page.locator(`text=${t('home.getStartedButton')}`);
    await getStartedButton.waitFor({ state: 'visible' });
    await getStartedButton.click();
    await page.waitForSelector('input[type="email"]', { state: 'visible' });

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
    await testPageAccessibility(page, '/chat', 'Chat interface', { waitForNetworkIdle: true });
  });

  test('upload page should not have accessibility violations', async ({ page }) => {
    await testPageAccessibility(page, '/upload', 'Upload page', { waitForNetworkIdle: true });
  });

  test('user profile should not have accessibility violations', async ({ page }) => {
    // Profile page redirects to settings page
    await testPageAccessibility(page, '/settings', 'User profile', { waitForNetworkIdle: true });
  });

  test('settings page should not have accessibility violations', async ({ page }) => {
    await testPageAccessibility(page, '/settings', 'Settings page', { waitForNetworkIdle: true });
  });

  test('games listing (authenticated) should not have violations', async ({ page }) => {
    await testPageAccessibility(page, '/games', 'Games listing', { waitForNetworkIdle: true });
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
    await testPageAccessibility(page, '/editor', 'Rule editor', {
      waitForNetworkIdle: true,
      customAction: async () => {
        // TipTap editor may not initialize without RuleSpec data - check page structure instead
        // Skip waiting for .ProseMirror if it doesn't appear quickly
        try {
          await page.waitForSelector('.ProseMirror', { timeout: 2000 });
        } catch {
          // Editor not initialized - test page accessibility anyway
          console.log('TipTap editor not initialized, testing page structure');
        }
      },
    });
  });

  test('version history should not have accessibility violations', async ({ page }) => {
    await testPageAccessibility(page, '/versions', 'Version history', { waitForNetworkIdle: true });
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
    await testPageAccessibility(page, '/admin', 'Admin dashboard', { waitForNetworkIdle: true });
  });

  test('admin users page should not have accessibility violations', async ({ page }) => {
    await testPageAccessibility(page, '/admin/users', 'Admin users', { waitForNetworkIdle: true });
  });

  test('admin analytics should not have accessibility violations', async ({ page }) => {
    await testPageAccessibility(page, '/admin/analytics', 'Admin analytics', {
      waitForNetworkIdle: true,
    });
  });

  test('admin configuration should not have accessibility violations', async ({ page }) => {
    await testPageAccessibility(page, '/admin/configuration', 'Admin configuration', {
      waitForNetworkIdle: true,
    });
  });
});

/**
 * Error State Accessibility Tests
 * Issue #841 - Comprehensive error scenario testing
 */
test.describe('Accessibility - Error States', () => {
  test('401 Unauthorized error should be accessible', async ({ page }) => {
    // Mock 401 error for auth endpoint
    await page.route('**/api/v1/auth/me', async route => {
      await route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Unauthorized', message: 'Session expired' }),
      });
    });

    // Try to access protected page
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Should redirect or show error - test current page accessibility
    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .disableRules(KNOWN_A11Y_ISSUES)
      .analyze();

    if (results.violations.length > 0) {
      console.error('❌ 401 error page violations:', formatViolations(results.violations));
    }

    expect(results.violations).toEqual([]);
  });

  test('404 Not Found page should be accessible', async ({ page }) => {
    // Navigate to non-existent page
    await page.goto('/this-page-does-not-exist-404');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .disableRules(KNOWN_A11Y_ISSUES)
      .analyze();

    if (results.violations.length > 0) {
      console.error('❌ 404 error page violations:', formatViolations(results.violations));
    }

    expect(results.violations).toEqual([]);
  });

  test('500 Internal Server Error should be accessible', async ({ page }) => {
    // Mock 500 error for a commonly accessed endpoint
    await page.route('**/api/v1/games', async route => {
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Internal Server Error' }),
      });
    });

    await page.goto('/games');
    await page.waitForLoadState('networkidle');

    // Error state should still be accessible
    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .disableRules(KNOWN_A11Y_ISSUES)
      .analyze();

    if (results.violations.length > 0) {
      console.error('❌ 500 error state violations:', formatViolations(results.violations));
    }

    expect(results.violations).toEqual([]);
  });

  test('403 Forbidden error should be accessible', async ({ page }) => {
    // Setup user role (not admin)
    await page.route('**/api/v1/auth/me', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          user: {
            id: 'user-test-id',
            email: 'user@meepleai.dev',
            displayName: 'Test User',
            role: 'User',
          },
        }),
      });
    });

    // Mock 403 for admin endpoint
    await page.route('**/api/v1/admin/**', async route => {
      await route.fulfill({
        status: 403,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Forbidden', message: 'Insufficient permissions' }),
      });
    });

    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Error state should be accessible
    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .disableRules(KNOWN_A11Y_ISSUES)
      .analyze();

    if (results.violations.length > 0) {
      console.error('❌ 403 error state violations:', formatViolations(results.violations));
    }

    expect(results.violations).toEqual([]);
  });

  test('Loading state should be accessible', async ({ page }) => {
    // Setup mock auth
    await setupMockAuth(page, 'User', 'user@meepleai.dev');

    // Mock delayed response to test loading state
    await page.route('**/api/v1/games', async route => {
      // Delay response to capture loading state
      await new Promise(resolve => setTimeout(resolve, 1000));
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    });

    // Start navigation
    await page.goto('/games');

    // Wait a bit for loading state to appear (but not complete)
    await page.waitForTimeout(300);

    // Check loading state accessibility
    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .disableRules(KNOWN_A11Y_ISSUES)
      .analyze();

    if (results.violations.length > 0) {
      console.error('❌ Loading state violations:', formatViolations(results.violations));
    }

    // Loading states should have appropriate ARIA labels
    expect(results.violations).toEqual([]);

    // Wait for page to finish loading
    await page.waitForLoadState('networkidle');
  });

  test('Network timeout error should be accessible', async ({ page }) => {
    // Mock timeout by aborting request
    await page.route('**/api/v1/games', async route => {
      await route.abort('timedout');
    });

    await page.goto('/games');
    await page.waitForLoadState('networkidle');

    // Error state should be accessible
    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .disableRules(KNOWN_A11Y_ISSUES)
      .analyze();

    if (results.violations.length > 0) {
      console.error('❌ Network timeout error violations:', formatViolations(results.violations));
    }

    expect(results.violations).toEqual([]);
  });
});
