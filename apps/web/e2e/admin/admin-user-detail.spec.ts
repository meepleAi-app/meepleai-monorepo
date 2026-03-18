/**
 * Admin User Detail Page E2E Tests
 *
 * Tests the /admin/users/[id] page with 3 tabs:
 * 1. Overview — user info display (name, email, role, status)
 * 2. Role — change role form + role history table
 * 3. Activity Log — paginated audit log table
 *
 * Uses page.context().route() for API mocking (Next.js dev server compatibility).
 * Uses .first() on all locators to avoid Playwright strict mode errors.
 */

import { test, expect, type Page } from '@playwright/test';

// ========== Configuration ==========

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const USER_ID = 'user-123';
const USER_DETAIL_URL = `/admin/users/${USER_ID}`;

// ========== Mock Data ==========

const MOCK_USER_DETAIL = {
  id: USER_ID,
  displayName: 'Test User',
  email: 'test@example.com',
  role: 'User',
  isSuspended: false,
  createdAt: '2026-01-15T10:00:00Z',
};

const MOCK_ROLE_HISTORY = [
  {
    oldRole: 'User',
    newRole: 'Contributor',
    changedByDisplayName: 'Admin',
    changedAt: '2026-02-01T14:30:00Z',
  },
];

const MOCK_AUDIT_LOG = {
  entries: [
    {
      id: 'audit-1',
      action: 'Login',
      resource: 'Auth',
      result: 'Success',
      details: 'IP: 192.168.1.1',
      createdAt: '2026-03-10T09:15:00Z',
    },
  ],
  totalCount: 1,
  limit: 20,
  offset: 0,
};

// ========== Helpers ==========

async function mockAdminAuth(page: Page) {
  await page.context().route(`${API_BASE}/api/v1/auth/me`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'admin-1',
          email: 'admin@meepleai.dev',
          displayName: 'Admin',
          role: 'Admin',
        },
        expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      }),
    })
  );
}

async function mockUserDetailEndpoints(page: Page) {
  await mockAdminAuth(page);

  await page.context().route(`${API_BASE}/api/v1/admin/users/${USER_ID}/role-history**`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_ROLE_HISTORY),
    })
  );

  await page.context().route(`${API_BASE}/api/v1/admin/users/${USER_ID}/audit-log**`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_AUDIT_LOG),
    })
  );

  await page.context().route(`${API_BASE}/api/v1/admin/users/${USER_ID}/role`, route => {
    if (route.request().method() === 'PUT') {
      return route.fulfill({ status: 204 });
    }
    return route.continue();
  });

  // User detail must come after more specific routes to avoid shadowing
  await page.context().route(`${API_BASE}/api/v1/admin/users/${USER_ID}`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_USER_DETAIL),
    })
  );
}

// ========== Tests ==========

test.describe('Admin User Detail Page', () => {
  test('navigates to user detail and shows overview tab', async ({ page }) => {
    await mockUserDetailEndpoints(page);
    await page.goto(USER_DETAIL_URL);

    // Verify user name is displayed
    await expect(page.getByText('Test User').first()).toBeVisible();

    // Verify email is displayed
    await expect(page.getByText('test@example.com').first()).toBeVisible();

    // Verify role badge is shown
    await expect(page.getByText('User').first()).toBeVisible();

    // Verify status — not suspended means active
    await expect(page.getByText('Active').or(page.getByText('Enabled')).first()).toBeVisible();
  });

  test('shows role tab with change role form and role history', async ({ page }) => {
    await mockUserDetailEndpoints(page);
    await page.goto(USER_DETAIL_URL);

    // Click Role tab
    await page.getByRole('tab', { name: /role/i }).first().click();

    // Verify change role form elements
    await expect(page.getByRole('combobox').or(page.locator('select')).first()).toBeVisible();

    await expect(
      page
        .getByPlaceholder(/reason/i)
        .or(page.getByLabel(/reason/i))
        .first()
    ).toBeVisible();

    await expect(page.getByRole('button', { name: /change role/i }).first()).toBeVisible();

    // Verify role history table shows entry
    await expect(page.getByText('Contributor').first()).toBeVisible();
    await expect(page.getByText('Admin').first()).toBeVisible();
  });

  test('changes user role with confirmation dialog', async ({ page }) => {
    await mockUserDetailEndpoints(page);
    await page.goto(USER_DETAIL_URL);

    // Navigate to Role tab
    await page.getByRole('tab', { name: /role/i }).first().click();

    // Select new role — click the combobox/select trigger
    const roleSelect = page.getByRole('combobox').or(page.locator('select')).first();
    await roleSelect.click();

    // Pick a different role option
    await page
      .getByRole('option', { name: /contributor/i })
      .or(page.getByText('Contributor'))
      .first()
      .click();

    // Enter reason
    const reasonField = page
      .getByPlaceholder(/reason/i)
      .or(page.getByLabel(/reason/i))
      .first();
    await reasonField.fill('Promoted for active contributions');

    // Click Change Role button
    await page
      .getByRole('button', { name: /change role/i })
      .first()
      .click();

    // Confirm in AlertDialog
    const confirmButton = page
      .getByRole('alertdialog')
      .getByRole('button', { name: /confirm/i })
      .or(page.getByRole('button', { name: /confirm/i }))
      .first();
    await confirmButton.click();

    // Verify success feedback
    await expect(
      page
        .getByText(/success/i)
        .or(page.getByText(/role.*changed/i))
        .or(page.getByText(/updated/i))
        .first()
    ).toBeVisible({ timeout: 5000 });
  });

  test('shows activity log tab with audit entries', async ({ page }) => {
    await mockUserDetailEndpoints(page);
    await page.goto(USER_DETAIL_URL);

    // Click Activity Log tab
    await page
      .getByRole('tab', { name: /activity/i })
      .first()
      .click();

    // Verify audit table entry — action
    await expect(page.getByText('Login').first()).toBeVisible();

    // Verify resource column
    await expect(page.getByText('Auth').first()).toBeVisible();

    // Verify result column
    await expect(page.getByText('Success').first()).toBeVisible();

    // Verify details
    await expect(page.getByText('192.168.1.1').first()).toBeVisible();
  });

  test('handles user not found error', async ({ page }) => {
    await mockAdminAuth(page);

    // Mock user detail returning 400 error
    await page.context().route(`${API_BASE}/api/v1/admin/users/${USER_ID}`, route =>
      route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'User not found' }),
      })
    );

    await page.goto(USER_DETAIL_URL);

    // Verify error state is shown
    await expect(
      page
        .getByText(/not found/i)
        .or(page.getByText(/error/i))
        .or(page.getByText(/failed/i))
        .first()
    ).toBeVisible({ timeout: 5000 });
  });
});
