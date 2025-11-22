/**
 * E2E-004: RBAC Authorization Tests
 *
 * Issue #1490: Comprehensive Role-Based Access Control testing
 *
 * Tests that users with different roles (Admin, Editor, User) have appropriate
 * access to protected routes and receive 403 Forbidden when attempting unauthorized access.
 *
 * Coverage:
 * - Admin-only routes (12 tests)
 * - Editor+ routes (3 tests)
 * - Public routes (3 tests)
 * - Unauthenticated access (3 tests)
 *
 * Total: 21 RBAC tests
 */

import { test, expect } from '@playwright/test';
import { setupMockAuth, setupMockAuthWithForbidden } from './fixtures/auth';

const API_BASE = process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('RBAC Authorization Tests - E2E-004', () => {

  test.describe('Admin-Only Routes', () => {

    test('Admin can access /admin dashboard', async ({ page }) => {
      await setupMockAuth(page, 'Admin');
      await page.goto('/admin');

      await expect(page).toHaveURL('/admin');
      // Verify admin dashboard content loads (flexible: any heading or main content)
      const hasHeading = await page.locator('h1, h2, h3').first().isVisible().catch(() => false);
      const hasMainContent = await page.locator('main, [role="main"]').isVisible().catch(() => false);
      expect(hasHeading || hasMainContent).toBe(true);
    });

    test('Editor is forbidden from /admin dashboard', async ({ page }) => {
      await setupMockAuthWithForbidden(page, 'Editor', ['/admin/**']);
      await page.goto('/admin');

      // Should redirect away from /admin or show forbidden
      await page.waitForLoadState('networkidle');
      const currentUrl = page.url();
      const isForbidden = await page.locator('text=/forbidden|403|not authorized/i').isVisible().catch(() => false);
      const isRedirected = !currentUrl.includes('/admin') || currentUrl === '/admin' && isForbidden;

      expect(isForbidden || isRedirected).toBe(true);
    });

    test('User is forbidden from /admin dashboard', async ({ page }) => {
      await setupMockAuthWithForbidden(page, 'User', ['/admin/**']);
      await page.goto('/admin');

      await page.waitForLoadState('networkidle');
      const currentUrl = page.url();
      const isForbidden = await page.locator('text=/forbidden|403|not authorized/i').isVisible().catch(() => false);
      const isRedirected = !currentUrl.includes('/admin') || currentUrl === '/admin' && isForbidden;

      expect(isForbidden || isRedirected).toBe(true);
    });

    test('Admin can access /admin/users', async ({ page }) => {
      await setupMockAuth(page, 'Admin');
      await page.goto('/admin/users');

      await expect(page).toHaveURL('/admin/users');
      // Verify page content loads (flexible)
      const hasHeading = await page.locator('h1, h2, h3').first().isVisible().catch(() => false);
      const hasMainContent = await page.locator('main, [role="main"]').isVisible().catch(() => false);
      expect(hasHeading || hasMainContent).toBe(true);
    });

    test('Editor is forbidden from /admin/users', async ({ page }) => {
      await setupMockAuthWithForbidden(page, 'Editor', ['/admin/**']);
      await page.goto('/admin/users');

      await page.waitForLoadState('networkidle');
      const currentUrl = page.url();
      const isForbidden = await page.locator('text=/forbidden|403|not authorized/i').isVisible().catch(() => false);
      const isRedirected = !currentUrl.includes('/admin/users') || isForbidden;

      expect(isForbidden || isRedirected).toBe(true);
    });

    test('User is forbidden from /admin/users', async ({ page }) => {
      await setupMockAuthWithForbidden(page, 'User', ['/admin/**']);
      await page.goto('/admin/users');

      await page.waitForLoadState('networkidle');
      const currentUrl = page.url();
      const isForbidden = await page.locator('text=/forbidden|403|not authorized/i').isVisible().catch(() => false);
      const isRedirected = !currentUrl.includes('/admin/users') || isForbidden;

      expect(isForbidden || isRedirected).toBe(true);
    });

    test('Admin can access /admin/configuration', async ({ page }) => {
      await setupMockAuth(page, 'Admin');
      await page.goto('/admin/configuration');

      await expect(page).toHaveURL('/admin/configuration');
      // Verify page content loads (flexible)
      const hasHeading = await page.locator('h1, h2, h3').first().isVisible().catch(() => false);
      const hasMainContent = await page.locator('main, [role="main"]').isVisible().catch(() => false);
      expect(hasHeading || hasMainContent).toBe(true);
    });

    test('Editor is forbidden from /admin/configuration', async ({ page }) => {
      await setupMockAuthWithForbidden(page, 'Editor', ['/admin/**']);
      await page.goto('/admin/configuration');

      await page.waitForLoadState('networkidle');
      const currentUrl = page.url();
      const isForbidden = await page.locator('text=/forbidden|403|not authorized/i').isVisible().catch(() => false);
      const isRedirected = !currentUrl.includes('/admin/configuration') || isForbidden;

      expect(isForbidden || isRedirected).toBe(true);
    });

    test('User is forbidden from /admin/configuration', async ({ page }) => {
      await setupMockAuthWithForbidden(page, 'User', ['/admin/**']);
      await page.goto('/admin/configuration');

      await page.waitForLoadState('networkidle');
      const currentUrl = page.url();
      const isForbidden = await page.locator('text=/forbidden|403|not authorized/i').isVisible().catch(() => false);
      const isRedirected = !currentUrl.includes('/admin/configuration') || isForbidden;

      expect(isForbidden || isRedirected).toBe(true);
    });

    test('Admin can access /admin/analytics', async ({ page }) => {
      await setupMockAuth(page, 'Admin');
      await page.goto('/admin/analytics');

      await expect(page).toHaveURL('/admin/analytics');
      // Verify page content loads (flexible)
      const hasHeading = await page.locator('h1, h2, h3').first().isVisible().catch(() => false);
      const hasMainContent = await page.locator('main, [role="main"]').isVisible().catch(() => false);
      expect(hasHeading || hasMainContent).toBe(true);
    });

    test('Editor is forbidden from /admin/analytics', async ({ page }) => {
      await setupMockAuthWithForbidden(page, 'Editor', ['/admin/**']);
      await page.goto('/admin/analytics');

      await page.waitForLoadState('networkidle');
      const currentUrl = page.url();
      const isForbidden = await page.locator('text=/forbidden|403|not authorized/i').isVisible().catch(() => false);
      const isRedirected = !currentUrl.includes('/admin/analytics') || isForbidden;

      expect(isForbidden || isRedirected).toBe(true);
    });

    test('User is forbidden from /admin/analytics', async ({ page }) => {
      await setupMockAuthWithForbidden(page, 'User', ['/admin/**']);
      await page.goto('/admin/analytics');

      await page.waitForLoadState('networkidle');
      const currentUrl = page.url();
      const isForbidden = await page.locator('text=/forbidden|403|not authorized/i').isVisible().catch(() => false);
      const isRedirected = !currentUrl.includes('/admin/analytics') || isForbidden;

      expect(isForbidden || isRedirected).toBe(true);
    });
  });

  test.describe('Editor+ Routes (Admin or Editor)', () => {

    test('Admin can access /editor', async ({ page }) => {
      await setupMockAuth(page, 'Admin');
      await page.goto('/editor');

      await expect(page).toHaveURL('/editor');
      // Verify page content loads (flexible)
      const hasHeading = await page.locator('h1, h2, h3').first().isVisible().catch(() => false);
      const hasMainContent = await page.locator('main, [role="main"]').isVisible().catch(() => false);
      expect(hasHeading || hasMainContent).toBe(true);
    });

    test('Editor can access /editor', async ({ page }) => {
      await setupMockAuth(page, 'Editor');
      await page.goto('/editor');

      await expect(page).toHaveURL('/editor');
      // Verify page content loads (flexible)
      const hasHeading = await page.locator('h1, h2, h3').first().isVisible().catch(() => false);
      const hasMainContent = await page.locator('main, [role="main"]').isVisible().catch(() => false);
      expect(hasHeading || hasMainContent).toBe(true);
    });

    test('User is forbidden from /editor', async ({ page }) => {
      await setupMockAuthWithForbidden(page, 'User', ['/editor/**']);
      await page.goto('/editor');

      await page.waitForLoadState('networkidle');
      const currentUrl = page.url();
      const isForbidden = await page.locator('text=/forbidden|403|not authorized/i').isVisible().catch(() => false);
      const isRedirected = !currentUrl.includes('/editor') || isForbidden;

      expect(isForbidden || isRedirected).toBe(true);
    });

    test('Admin can access /upload', async ({ page }) => {
      await setupMockAuth(page, 'Admin');
      await page.goto('/upload');

      await expect(page).toHaveURL('/upload');
      // Verify page content loads (flexible)
      const hasHeading = await page.locator('h1, h2, h3').first().isVisible().catch(() => false);
      const hasMainContent = await page.locator('main, [role="main"]').isVisible().catch(() => false);
      expect(hasHeading || hasMainContent).toBe(true);
    });

    test('Editor can access /upload', async ({ page }) => {
      await setupMockAuth(page, 'Editor');
      await page.goto('/upload');

      await expect(page).toHaveURL('/upload');
      // Verify page content loads (flexible)
      const hasHeading = await page.locator('h1, h2, h3').first().isVisible().catch(() => false);
      const hasMainContent = await page.locator('main, [role="main"]').isVisible().catch(() => false);
      expect(hasHeading || hasMainContent).toBe(true);
    });

    test('User is forbidden from /upload', async ({ page }) => {
      await setupMockAuthWithForbidden(page, 'User', ['/upload/**']);
      await page.goto('/upload');

      await page.waitForLoadState('networkidle');
      const currentUrl = page.url();
      const isForbidden = await page.locator('text=/forbidden|403|not authorized/i').isVisible().catch(() => false);
      const isRedirected = !currentUrl.includes('/upload') || isForbidden;

      expect(isForbidden || isRedirected).toBe(true);
    });
  });

  test.describe('Public Routes (All Roles)', () => {

    const publicRoutes = ['/', '/chat', '/games'];

    publicRoutes.forEach(route => {
      test(`Admin can access ${route}`, async ({ page }) => {
        await setupMockAuth(page, 'Admin');
        await page.goto(route);

        await expect(page).toHaveURL(route);
        await page.waitForLoadState('networkidle');
      });

      test(`Editor can access ${route}`, async ({ page }) => {
        await setupMockAuth(page, 'Editor');
        await page.goto(route);

        await expect(page).toHaveURL(route);
        await page.waitForLoadState('networkidle');
      });

      test(`User can access ${route}`, async ({ page }) => {
        await setupMockAuth(page, 'User');
        await page.goto(route);

        await expect(page).toHaveURL(route);
        await page.waitForLoadState('networkidle');
      });
    });
  });

  test.describe('Unauthenticated Access', () => {

    test('Unauthenticated user redirected from /admin', async ({ page }) => {
      // No mock auth setup = unauthenticated
      await page.goto('/admin');

      await page.waitForLoadState('networkidle');
      const currentUrl = page.url();

      // Should redirect to login or show unauthorized
      const isLoginRedirect = currentUrl.includes('/login');
      const isUnauthorized = await page.locator('text=/unauthorized|401|sign in/i').isVisible().catch(() => false);

      expect(isLoginRedirect || isUnauthorized).toBe(true);
    });

    test('Unauthenticated user redirected from /editor', async ({ page }) => {
      await page.goto('/editor');

      await page.waitForLoadState('networkidle');
      const currentUrl = page.url();

      const isLoginRedirect = currentUrl.includes('/login');
      const isUnauthorized = await page.locator('text=/unauthorized|401|sign in/i').isVisible().catch(() => false);

      expect(isLoginRedirect || isUnauthorized).toBe(true);
    });

    test('Unauthenticated user can access public routes', async ({ page }) => {
      // No auth setup
      await page.goto('/');

      await expect(page).toHaveURL('/');
      await page.waitForLoadState('networkidle');

      // Should see landing page, not login redirect
      const hasContent = await page.locator('body').isVisible();
      expect(hasContent).toBe(true);
    });
  });
});