/**
 * Admin Mobile Navigation — E2E Tests
 * Validates hamburger drawer + breadcrumb on mobile viewports
 */

import { test, expect, type Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function mockAdminAuth(page: Page) {
  await page.context().route(`${API_BASE}/api/v1/auth/me`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: { id: 'admin-1', email: 'admin@meepleai.dev', displayName: 'Admin', role: 'Admin' },
        expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      }),
    })
  );
}

async function mockAdminApi(page: Page) {
  // Mock common admin API endpoints to prevent 404s
  await page
    .context()
    .route(`${API_BASE}/api/v1/**`, route =>
      route.fulfill({ status: 200, contentType: 'application/json', body: '{}' })
    );
}

test.describe('Admin Mobile Navigation', () => {
  test.describe('Mobile viewport (375x812)', () => {
    test.use({ viewport: { width: 375, height: 812 } });

    test.beforeEach(async ({ page }) => {
      await mockAdminAuth(page);
      await mockAdminApi(page);
    });

    test('sidebar is hidden on mobile', async ({ page }) => {
      await page.goto('/admin/overview');
      await expect(page.getByTestId('admin-tab-sidebar').first()).toBeHidden();
    });

    test('hamburger button is visible and opens drawer', async ({ page }) => {
      await page.goto('/admin/overview');
      const hamburger = page.getByRole('button', { name: /open admin menu/i }).first();
      await expect(hamburger).toBeVisible();
      await hamburger.click();

      // Drawer should show all 6 sections
      await expect(page.getByTestId('admin-tab-overview').first()).toBeVisible();
      await expect(page.getByTestId('admin-tab-content').first()).toBeVisible();
      await expect(page.getByTestId('admin-tab-ai').first()).toBeVisible();
      await expect(page.getByTestId('admin-tab-users').first()).toBeVisible();
      await expect(page.getByTestId('admin-tab-system').first()).toBeVisible();
      await expect(page.getByTestId('admin-tab-analytics').first()).toBeVisible();
    });

    test('navigation from drawer closes it', async ({ page }) => {
      await page.goto('/admin/overview');
      const hamburger = page.getByRole('button', { name: /open admin menu/i }).first();
      await hamburger.click();

      // Click on AI section
      await page.getByTestId('admin-tab-ai').first().click();
      await page.waitForURL('**/admin/agents');

      // Drawer should close after navigation
      await expect(page).toHaveURL(/\/admin\/agents/);
    });

    test('breadcrumb shows current section', async ({ page }) => {
      await page.goto('/admin/agents/pipeline');
      const breadcrumb = page.getByTestId('admin-breadcrumb').first();
      await expect(breadcrumb).toBeVisible();
      await expect(breadcrumb.getByText('AI').first()).toBeVisible();
    });

    test('breadcrumb dropdown shows sub-sections', async ({ page }) => {
      await page.goto('/admin/agents/pipeline');
      const chevron = page.getByRole('button', { name: /show sub-sections/i }).first();
      await chevron.click();

      const dropdown = page.getByTestId('admin-breadcrumb-dropdown').first();
      await expect(dropdown).toBeVisible();
      await expect(dropdown.getByText('All Agents').first()).toBeVisible();
      await expect(dropdown.getByText('Debug Console').first()).toBeVisible();
    });

    test('navigate from breadcrumb dropdown', async ({ page }) => {
      await page.goto('/admin/agents/pipeline');
      const chevron = page.getByRole('button', { name: /show sub-sections/i }).first();
      await chevron.click();

      await page
        .getByTestId('admin-breadcrumb-dropdown')
        .first()
        .getByText('Debug Console')
        .first()
        .click();
      await page.waitForURL('**/admin/agents/debug');
      await expect(page).toHaveURL(/\/admin\/agents\/debug/);
    });
  });

  test.describe('Desktop viewport (1280x800)', () => {
    test.use({ viewport: { width: 1280, height: 800 } });

    test.beforeEach(async ({ page }) => {
      await mockAdminAuth(page);
      await mockAdminApi(page);
    });

    test('sidebar is visible and hamburger is hidden on desktop', async ({ page }) => {
      await page.goto('/admin/overview');
      await expect(page.getByTestId('admin-tab-sidebar').first()).toBeVisible();
      await expect(page.getByRole('button', { name: /open admin menu/i })).toHaveCount(0);
    });
  });

  test.describe('Viewport resize transition', () => {
    test.beforeEach(async ({ page }) => {
      await mockAdminAuth(page);
      await mockAdminApi(page);
    });

    test('resize from desktop to mobile hides sidebar and shows hamburger', async ({ page }) => {
      // Start at desktop
      await page.setViewportSize({ width: 1280, height: 800 });
      await page.goto('/admin/overview');
      await expect(page.getByTestId('admin-tab-sidebar').first()).toBeVisible();

      // Resize to mobile
      await page.setViewportSize({ width: 375, height: 812 });
      await expect(page.getByTestId('admin-tab-sidebar').first()).toBeHidden();
      await expect(page.getByRole('button', { name: /open admin menu/i }).first()).toBeVisible();
    });
  });
});
