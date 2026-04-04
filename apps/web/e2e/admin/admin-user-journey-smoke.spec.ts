import { test, expect, type Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ---------------------------------------------------------------------------
// Mock data
// ---------------------------------------------------------------------------

const MOCK_USERS_LIST = {
  items: [
    {
      id: 'user-123',
      displayName: 'Test User',
      email: 'test@example.com',
      role: 'User',
      isSuspended: false,
      createdAt: '2026-01-15T10:00:00Z',
    },
  ],
  totalCount: 1,
};

const MOCK_USER_DETAIL = {
  id: 'user-123',
  displayName: 'Test User',
  email: 'test@example.com',
  role: 'User',
  isSuspended: false,
  createdAt: '2026-01-15T10:00:00Z',
  lastSeenAt: '2026-03-14T08:00:00Z',
  isTwoFactorEnabled: false,
  tier: 'Free',
  tokenUsage: 500,
  tokenLimit: 10000,
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
      id: 'a-1',
      action: 'Login',
      resource: 'Auth',
      result: 'Success',
      details: null,
      createdAt: '2026-03-10T09:15:00Z',
    },
  ],
  totalCount: 1,
  limit: 20,
  offset: 0,
};

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

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

async function mockUserApis(page: Page) {
  // Users list
  await page.context().route(`${API_BASE}/api/v1/admin/users**`, route => {
    const url = route.request().url();

    // Audit log
    if (url.includes('/audit-log')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_AUDIT_LOG),
      });
    }

    // Role history
    if (url.includes('/role-history')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_ROLE_HISTORY),
      });
    }

    // Role change (PUT)
    if (route.request().method() === 'PUT' && url.includes('/role')) {
      return route.fulfill({ status: 204 });
    }

    // Single user detail
    if (url.includes('/users/user-123') && !url.includes('?')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_USER_DETAIL),
      });
    }

    // Default: users list
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_USERS_LIST),
    });
  });
}

// ---------------------------------------------------------------------------
// Test
// ---------------------------------------------------------------------------

test.describe('Admin User Journey Smoke', () => {
  test('full admin user journey: list → detail → role change → audit', async ({ page }) => {
    // ---- Setup mocks ----
    await mockAdminAuth(page);
    await mockUserApis(page);

    // ---- Step 1: Navigate to users list ----
    await page.goto('/admin/users');
    await expect(page.getByText('Test User').first()).toBeVisible();
    await expect(page.getByText('test@example.com').first()).toBeVisible();

    // ---- Step 2: Click user row to navigate to detail ----
    await page.getByText('Test User').first().click();
    await page.waitForURL('**/admin/users/user-123');

    // ---- Step 3: Verify user detail overview ----
    await expect(page.getByText('Test User').first()).toBeVisible();
    await expect(page.getByText('test@example.com').first()).toBeVisible();

    // ---- Step 4: Switch to Role tab and trigger role change ----
    const roleTab = page.getByRole('tab', { name: /role/i }).or(page.getByText(/role/i).first());
    await roleTab.first().click();

    // Look for role history entry
    await expect(page.getByText('Contributor').first()).toBeVisible();

    // Attempt a role change if a select / button is present
    const roleSelect = page.getByRole('combobox').first();
    if (await roleSelect.isVisible({ timeout: 2000 }).catch(() => false)) {
      await roleSelect.click();
      await page
        .getByRole('option', { name: /contributor/i })
        .first()
        .click();

      const saveBtn = page.getByRole('button', { name: /save|update|change/i }).first();
      if (await saveBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
        await saveBtn.click();
      }
    }

    // ---- Step 5: Switch to Activity / Audit Log tab ----
    const activityTab = page
      .getByRole('tab', { name: /activity|audit|log/i })
      .or(page.getByText(/activity|audit log/i).first());
    await activityTab.first().click();

    await expect(page.getByText('Login').first()).toBeVisible();
    await expect(page.getByText('Success').first()).toBeVisible();

    // ---- Step 6: Navigate back to users list ----
    const backLink = page
      .getByRole('link', { name: /back|users/i })
      .or(page.getByText(/back to users/i))
      .first();
    if (await backLink.isVisible({ timeout: 2000 }).catch(() => false)) {
      await backLink.click();
    } else {
      await page.goto('/admin/users');
    }

    await page.waitForURL('**/admin/users');
    await expect(page.getByText('Test User').first()).toBeVisible();
  });
});
