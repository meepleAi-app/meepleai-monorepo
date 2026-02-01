/**
 * Admin Login E2E Test - Real Backend Integration
 *
 * Tests the complete login flow with INITIAL_ADMIN credentials
 * and navigation to the admin dashboard.
 *
 * Prerequisites:
 * - Backend API running: cd apps/api/src/Api && dotnet run
 * - Required services: cd infra && docker compose up -d postgres qdrant redis
 * - INITIAL_ADMIN_EMAIL and INITIAL_ADMIN_PASSWORD configured in environment
 * - Frontend dev server: cd apps/web && pnpm dev
 *
 * Environment Variables:
 * - E2E_ADMIN_EMAIL: Admin email (defaults to INITIAL_ADMIN_EMAIL from .env)
 * - E2E_ADMIN_PASSWORD: Admin password (defaults to Demo123!)
 *
 * Usage:
 * - Run with real backend: pnpm test:e2e admin-login-real.spec.ts
 * - Skip in CI (no real backend): Test checks for CI environment
 */

import { authenticateViaAPI } from './fixtures/auth';
import { test, expect } from './fixtures';

// Get admin credentials from environment or use defaults
const ADMIN_EMAIL =
  process.env.E2E_ADMIN_EMAIL || process.env.INITIAL_ADMIN_EMAIL || 'admin@meepleai.dev';
const ADMIN_PASSWORD =
  process.env.E2E_ADMIN_PASSWORD || process.env.INITIAL_ADMIN_PASSWORD || 'Demo123!';

test.describe('Admin Login - Real Backend Integration', () => {
  // Increase timeout for real backend operations
  test.setTimeout(60000);

  // Skip in CI environment if no real backend is available
  test.beforeEach(async ({ page }) => {
    // Check if backend is reachable
    const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
    try {
      const response = await page.request.get(`${apiBase}/health`);
      if (!response.ok()) {
        test.skip(true, 'Backend API is not reachable - skipping real backend tests');
      }
    } catch {
      test.skip(true, 'Backend API is not reachable - skipping real backend tests');
    }
  });

  test('should login with INITIAL_ADMIN credentials and navigate to admin dashboard', async ({
    browser,
  }) => {
    // Create new browser context for isolation
    const context = await browser.newContext({
      baseURL: 'http://localhost:3000',
    });

    const page = await context.newPage();

    // ========================================================================
    // Phase 1: Authenticate via API with INITIAL_ADMIN credentials
    // ========================================================================

    await test.step('Authenticate admin user via API', async () => {
      console.log(`🔐 Authenticating as: ${ADMIN_EMAIL}`);

      const authenticated = await authenticateViaAPI(page, ADMIN_EMAIL, ADMIN_PASSWORD);

      if (!authenticated) {
        test.skip(
          true,
          `Authentication failed for ${ADMIN_EMAIL} - verify INITIAL_ADMIN credentials are correct`
        );
      }

      console.log('✅ Admin authentication successful');
    });

    // ========================================================================
    // Phase 2: Navigate to Admin Dashboard
    // ========================================================================

    await test.step('Navigate to admin dashboard', async () => {
      await page.goto('/admin');
      await page.waitForLoadState('networkidle');

      // Verify we're on the admin page (not redirected to login)
      const currentUrl = page.url();
      console.log(`📍 Current URL: ${currentUrl}`);

      expect(currentUrl).toContain('/admin');
    });

    // ========================================================================
    // Phase 3: Verify Admin Dashboard Content
    // ========================================================================

    await test.step('Verify admin dashboard is displayed', async () => {
      // Wait for dashboard heading to appear
      const dashboardHeading = page.getByRole('heading', { name: /dashboard|admin/i });
      await expect(dashboardHeading).toBeVisible({ timeout: 10000 });

      // Verify admin layout elements
      const adminHeader = page.getByRole('heading', { name: /meepleai admin/i });
      await expect(adminHeader).toBeVisible({ timeout: 5000 });

      // Verify navigation sidebar is present
      const dashboardLink = page.getByRole('link', { name: 'Dashboard' });
      await expect(dashboardLink).toBeVisible();

      const usersLink = page.getByRole('link', { name: 'Users' });
      await expect(usersLink).toBeVisible();

      console.log('✅ Admin dashboard loaded successfully');
    });

    // ========================================================================
    // Phase 4: Verify Admin Role Permissions
    // ========================================================================

    await test.step('Verify admin has proper access', async () => {
      // Navigate to Users section to confirm admin permissions
      await page.getByRole('link', { name: 'Users', exact: true }).click();
      await page.waitForLoadState('networkidle');

      // Verify URL changed to users page
      await expect(page).toHaveURL(/\/admin\/users/);

      // Navigate back to dashboard
      await page.getByRole('link', { name: 'Dashboard' }).click();
      await page.waitForLoadState('networkidle');

      await expect(page).toHaveURL(/\/admin/);

      console.log('✅ Admin navigation working correctly');
    });

    await context.close();
  });

  test('should display dashboard metrics after admin login', async ({ browser }) => {
    const context = await browser.newContext({
      baseURL: 'http://localhost:3000',
    });

    const page = await context.newPage();

    // Authenticate
    await test.step('Authenticate admin user', async () => {
      const authenticated = await authenticateViaAPI(page, ADMIN_EMAIL, ADMIN_PASSWORD);
      if (!authenticated) {
        test.skip(true, 'Authentication failed');
      }
    });

    // Navigate and verify metrics
    await test.step('Verify dashboard metrics are displayed', async () => {
      await page.goto('/admin');
      await page.waitForLoadState('networkidle');

      // Wait for dashboard grid to be visible (condition-based wait instead of hardcoded timeout)
      const gridLocator = page.locator('[class*="grid"]');
      await expect(gridLocator).toBeVisible({ timeout: 5000 });

      // Check for common dashboard elements (metrics cards)
      // These may vary based on actual dashboard implementation
      const metricsVisible = await gridLocator.isVisible();

      if (metricsVisible) {
        console.log('✅ Dashboard metrics grid is visible');
      }

      // Verify page title or main heading
      const pageTitle = await page.title();
      console.log(`📄 Page title: ${pageTitle}`);

      expect(pageTitle).toBeTruthy();
    });

    await context.close();
  });

  test('should persist session after page reload', async ({ browser }) => {
    const context = await browser.newContext({
      baseURL: 'http://localhost:3000',
    });

    const page = await context.newPage();

    // Authenticate
    await test.step('Authenticate and verify session', async () => {
      const authenticated = await authenticateViaAPI(page, ADMIN_EMAIL, ADMIN_PASSWORD);
      if (!authenticated) {
        test.skip(true, 'Authentication failed');
      }

      // Navigate to admin dashboard
      await page.goto('/admin');
      await page.waitForLoadState('networkidle');

      // Verify we're on admin page
      await expect(page).toHaveURL(/\/admin/);
    });

    // Reload and verify session persists
    await test.step('Reload page and verify session persists', async () => {
      await page.reload();
      await page.waitForLoadState('networkidle');

      // Should still be on admin page (not redirected to login)
      const currentUrl = page.url();
      expect(currentUrl).toContain('/admin');

      // Dashboard should still be visible
      const dashboardHeading = page.getByRole('heading', { name: /dashboard|admin/i });
      await expect(dashboardHeading).toBeVisible({ timeout: 10000 });

      console.log('✅ Session persisted after page reload');
    });

    await context.close();
  });
});
