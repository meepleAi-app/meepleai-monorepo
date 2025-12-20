/**
 * RBAC Authorization E2E Tests - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/helpers/AuthHelper.ts
 */

import { test, expect } from './fixtures/chromatic';
import { expectForbiddenOrRedirect, expectPageLoaded } from './helpers/assertions';
import { WaitHelper } from './helpers/WaitHelper';
import { AuthHelper } from './pages';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('RBAC Authorization Tests - E2E-004', () => {
  test.describe('Admin-Only Routes', () => {
    test('Admin can access /admin dashboard', async ({ page }) => {
      const authHelper = new AuthHelper(page);
      await authHelper.setupMockAuth('Admin');
      await page.goto('/admin');

      await expect(page).toHaveURL('/admin');
      await page.waitForLoadState('networkidle');
      await expectPageLoaded(page);
    });

    test('Editor is forbidden from /admin dashboard', async ({ page }) => {
      await setupMockAuthWithForbidden(page, 'Editor', ['/admin/**']);
      await page.goto('/admin');

      await expectForbiddenOrRedirect(page, '/admin');
    });

    test('User is forbidden from /admin dashboard', async ({ page }) => {
      await setupMockAuthWithForbidden(page, 'User', ['/admin/**']);
      await page.goto('/admin');

      await expectForbiddenOrRedirect(page, '/admin');
    });

    test('Admin can access /admin/users', async ({ page }) => {
      const authHelper = new AuthHelper(page);
      await authHelper.setupMockAuth('Admin');
      await page.goto('/admin/users');

      await expect(page).toHaveURL('/admin/users');
      await page.waitForLoadState('networkidle');
      await expectPageLoaded(page);
    });

    test('Editor is forbidden from /admin/users', async ({ page }) => {
      await setupMockAuthWithForbidden(page, 'Editor', ['/admin/**']);
      await page.goto('/admin/users');

      await expectForbiddenOrRedirect(page, '/admin/users');
    });

    test('User is forbidden from /admin/users', async ({ page }) => {
      await setupMockAuthWithForbidden(page, 'User', ['/admin/**']);
      await page.goto('/admin/users');

      await expectForbiddenOrRedirect(page, '/admin/users');
    });

    test('Admin can access /admin/configuration', async ({ page }) => {
      const authHelper = new AuthHelper(page);
      await authHelper.setupMockAuth('Admin');
      await page.goto('/admin/configuration');

      await expect(page).toHaveURL('/admin/configuration');
      await page.waitForLoadState('networkidle');
      await expectPageLoaded(page);
    });

    test('Editor is forbidden from /admin/configuration', async ({ page }) => {
      await setupMockAuthWithForbidden(page, 'Editor', ['/admin/**']);
      await page.goto('/admin/configuration');

      await expectForbiddenOrRedirect(page, '/admin/configuration');
    });

    test('User is forbidden from /admin/configuration', async ({ page }) => {
      await setupMockAuthWithForbidden(page, 'User', ['/admin/**']);
      await page.goto('/admin/configuration');

      await expectForbiddenOrRedirect(page, '/admin/configuration');
    });

    test('Admin can access /admin/analytics', async ({ page }) => {
      const authHelper = new AuthHelper(page);
      await authHelper.setupMockAuth('Admin');
      await page.goto('/admin/analytics');

      await expect(page).toHaveURL('/admin/analytics');
      await page.waitForLoadState('networkidle');
      await expectPageLoaded(page);
    });

    test('Editor is forbidden from /admin/analytics', async ({ page }) => {
      await setupMockAuthWithForbidden(page, 'Editor', ['/admin/**']);
      await page.goto('/admin/analytics');

      await expectForbiddenOrRedirect(page, '/admin/analytics');
    });

    test('User is forbidden from /admin/analytics', async ({ page }) => {
      await setupMockAuthWithForbidden(page, 'User', ['/admin/**']);
      await page.goto('/admin/analytics');

      await expectForbiddenOrRedirect(page, '/admin/analytics');
    });
  });

  test.describe('Editor+ Routes (Admin or Editor)', () => {
    test('Admin can access /editor', async ({ page }) => {
      const authHelper = new AuthHelper(page);
      await authHelper.setupMockAuth('Admin');
      await page.goto('/editor');

      await expect(page).toHaveURL('/editor');
      await page.waitForLoadState('networkidle');
      await expectPageLoaded(page);
    });

    test('Editor can access /editor', async ({ page }) => {
      const authHelper = new AuthHelper(page);
      await authHelper.setupMockAuth('Editor');
      await page.goto('/editor');

      await expect(page).toHaveURL('/editor');
      await page.waitForLoadState('networkidle');
      await expectPageLoaded(page);
    });

    test('User is forbidden from /editor', async ({ page }) => {
      await setupMockAuthWithForbidden(page, 'User', ['/editor/**']);
      await page.goto('/editor');

      await expectForbiddenOrRedirect(page, '/editor');
    });

    test('Admin can access /upload', async ({ page }) => {
      const authHelper = new AuthHelper(page);
      await authHelper.setupMockAuth('Admin');
      await page.goto('/upload');

      await expect(page).toHaveURL('/upload');
      await page.waitForLoadState('networkidle');
      await expectPageLoaded(page);
    });

    test('Editor can access /upload', async ({ page }) => {
      const authHelper = new AuthHelper(page);
      await authHelper.setupMockAuth('Editor');
      await page.goto('/upload');

      await expect(page).toHaveURL('/upload');
      await page.waitForLoadState('networkidle');
      await expectPageLoaded(page);
    });

    test('User is forbidden from /upload', async ({ page }) => {
      await setupMockAuthWithForbidden(page, 'User', ['/upload/**']);
      await page.goto('/upload');

      await expectForbiddenOrRedirect(page, '/upload');
    });
  });

  test.describe('Public Routes (All Roles)', () => {
    const publicRoutes = ['/', '/chat', '/games'];

    publicRoutes.forEach(route => {
      test(`Admin can access ${route}`, async ({ page }) => {
        const authHelper = new AuthHelper(page);
        await authHelper.setupMockAuth('Admin');
        await page.goto(route);

        await expect(page).toHaveURL(route);
        await page.waitForLoadState('networkidle');
      });

      test(`Editor can access ${route}`, async ({ page }) => {
        const authHelper = new AuthHelper(page);
        await authHelper.setupMockAuth('Editor');
        await page.goto(route);

        await expect(page).toHaveURL(route);
        await page.waitForLoadState('networkidle');
      });

      test(`User can access ${route}`, async ({ page }) => {
        const authHelper = new AuthHelper(page);
        await authHelper.setupMockAuth('User');
        await page.goto(route);

        await expect(page).toHaveURL(route);
        await page.waitForLoadState('networkidle');
      });
    });
  });

  test.describe('Unauthenticated Access', () => {
    test('Unauthenticated user redirected from /admin', async ({ page }) => {
      // No mock auth setup = unauthenticated
      // Clear any existing cookies first
      await page.context().clearCookies();

      await page.goto('/admin');

      // Wait for either middleware redirect OR client-side RequireRole redirect
      try {
        await page.waitForURL(/\/login/, { timeout: 3000 });
        expect(page.url()).toContain('/login');
      } catch {
        // If middleware didn't redirect, check for RequireRole loading/redirect
        await page.waitForLoadState('networkidle');
        const waitHelper = new WaitHelper(page);
        await waitHelper.waitForNetworkIdle(5000); // Give RequireRole time to redirect

        const currentUrl = page.url();
        const isLoginRedirect = currentUrl.includes('/login');
        const hasLoadingSpinner = await page
          .locator('text=/verifica autorizzazioni/i')
          .isVisible()
          .catch(() => false);

        expect(isLoginRedirect || hasLoadingSpinner).toBe(true);
      }
    });

    test('Unauthenticated user redirected from /editor', async ({ page }) => {
      // No mock auth setup = unauthenticated
      // Clear any existing cookies first
      await page.context().clearCookies();

      await page.goto('/editor');

      // Wait for either middleware redirect OR client-side RequireRole redirect
      try {
        await page.waitForURL(/\/login/, { timeout: 3000 });
        expect(page.url()).toContain('/login');
      } catch {
        // If middleware didn't redirect, check for RequireRole loading/redirect
        await page.waitForLoadState('networkidle');
        const waitHelper = new WaitHelper(page);
        await waitHelper.waitForNetworkIdle(5000); // Give RequireRole time to redirect

        const currentUrl = page.url();
        const isLoginRedirect = currentUrl.includes('/login');
        const hasLoadingSpinner = await page
          .locator('text=/verifica autorizzazioni/i')
          .isVisible()
          .catch(() => false);

        expect(isLoginRedirect || hasLoadingSpinner).toBe(true);
      }
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
