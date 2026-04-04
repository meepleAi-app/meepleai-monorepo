/**
 * Admin Invite Full Flow E2E Tests (Task 1.5)
 *
 * Complements invitations.spec.ts with end-to-end user journey tests:
 * 1. Admin sends invitation and sees it in the table
 * 2. User accepts invitation via /accept-invite page (separate browser context)
 * 3. Expired token shows error state on /accept-invite
 * 4. Admin resends an existing pending invitation
 *
 * Uses page.context().route() for API mocking (Next.js dev server compatibility).
 * Uses .first() on all locators to avoid Playwright strict mode errors.
 */

import { test, expect, type Page } from '@playwright/test';

// ========== Configuration ==========

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ========== Test Data ==========

const MOCK_INVITATION_ID = '00000000-0000-0000-0000-000000000010';

const MOCK_INVITATION = {
  id: MOCK_INVITATION_ID,
  email: 'newuser@example.com',
  role: 'User',
  status: 'Pending',
  createdAt: '2026-03-10T12:00:00Z',
  expiresAt: '2026-03-17T12:00:00Z',
  acceptedAt: null,
  invitedByUserId: '00000000-0000-0000-0000-000000000099',
};

const MOCK_STATS = {
  total: 5,
  pending: 2,
  accepted: 2,
  expired: 1,
  revoked: 0,
};

const MOCK_INVITATIONS_LIST = {
  items: [MOCK_INVITATION],
  totalCount: 1,
  page: 1,
  pageSize: 20,
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

async function setupInvitationPageMocks(page: Page) {
  await mockAdminAuth(page);

  // Mock stats endpoint (must come before the general invitations route)
  await page.context().route(`${API_BASE}/api/v1/admin/users/invitations/stats**`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_STATS),
    })
  );

  // Mock invitations list + resend
  await page.context().route(`${API_BASE}/api/v1/admin/users/invitations**`, route => {
    const url = route.request().url();

    // Resend endpoint
    if (url.includes('/resend') && route.request().method() === 'POST') {
      return route.fulfill({ status: 204 });
    }

    // List endpoint (GET)
    if (route.request().method() === 'GET') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_INVITATIONS_LIST),
      });
    }

    return route.continue();
  });

  // Mock POST invite (send new invitation)
  await page.context().route(`${API_BASE}/api/v1/admin/users/invite`, route => {
    if (route.request().method() === 'POST') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: MOCK_INVITATION_ID,
          email: 'newuser@example.com',
          role: 'User',
          status: 'Pending',
          createdAt: new Date().toISOString(),
          expiresAt: new Date(Date.now() + 7 * 86_400_000).toISOString(),
          acceptedAt: null,
          invitedByUserId: 'admin-1',
        }),
      });
    }
    return route.continue();
  });

  // Catch-all for other admin API calls
  await page.context().route(`${API_BASE}/api/v1/admin/**`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ data: [] }),
    })
  );
}

// ========== Tests ==========

test.describe('Admin Invite Flow — Full Journey', () => {
  test('admin sends invitation and sees it in the table', async ({ page }) => {
    await setupInvitationPageMocks(page);

    await page.goto('/admin/users/invitations', { waitUntil: 'domcontentloaded' });

    // Verify page loaded — title and action buttons
    await expect(page.getByText('Invitations').first()).toBeVisible({ timeout: 10_000 });

    // Click "Invite User" button to open dialog
    const inviteButton = page.getByRole('button', { name: /invite user/i }).first();
    await expect(inviteButton).toBeVisible();
    await inviteButton.click();

    // Dialog should appear
    await expect(page.getByRole('dialog')).toBeVisible({ timeout: 5_000 });

    // Fill email in the dialog
    const emailInput = page.getByLabel(/email address/i).first();
    await expect(emailInput).toBeVisible();
    await emailInput.fill('newuser@example.com');

    // Submit the form
    const sendButton = page.getByRole('button', { name: /send invitation/i }).first();
    await expect(sendButton).toBeVisible();
    await sendButton.click();

    // After dialog closes, the invitation should appear in the table
    await expect(page.getByText('newuser@example.com').first()).toBeVisible({ timeout: 10_000 });
  });

  test('user accepts invitation via accept-invite page', async ({ browser }) => {
    // Use a separate browser context for the invited user (no admin session)
    const userContext = await browser.newContext();
    const userPage = await userContext.newPage();

    // Mock validate-invitation endpoint (success)
    await userContext.route(`**/api/v1/auth/validate-invitation`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          email: 'newuser@example.com',
          role: 'User',
          expiresAt: '2026-03-17T12:00:00Z',
        }),
      });
    });

    // Mock accept-invitation endpoint (success)
    await userContext.route(`**/api/v1/auth/accept-invitation`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          sessionToken: 'mock-session-token-abc123',
          expiresAt: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
        }),
      });
    });

    await userPage.goto('/accept-invite?token=test-token-valid');
    await userPage.waitForLoadState('networkidle');

    // Email field should be visible, filled, and read-only
    const emailInput = userPage.getByLabel(/email/i).first();
    await expect(emailInput).toBeVisible({ timeout: 10_000 });
    await expect(emailInput).toHaveValue('newuser@example.com');
    await expect(emailInput).toHaveAttribute('readonly', '');

    // Fill password fields with a strong password (meets all requirements)
    const passwordInput = userPage.getByLabel(/^password$/i).first();
    await expect(passwordInput).toBeVisible();
    await passwordInput.fill('SecureP@ss1');

    const confirmInput = userPage.getByLabel(/confirm password/i).first();
    await expect(confirmInput).toBeVisible();
    await confirmInput.fill('SecureP@ss1');

    // Click "Create Account"
    const submitButton = userPage.getByRole('button', { name: /create account/i }).first();
    await expect(submitButton).toBeEnabled();
    await submitButton.click();

    // Assert success state — "Account Created!" card appears
    await expect(userPage.getByText(/account created/i).first()).toBeVisible({ timeout: 10_000 });

    await userContext.close();
  });

  test('expired token shows error on accept-invite page', async ({ browser }) => {
    const userContext = await browser.newContext();
    const userPage = await userContext.newPage();

    // Mock validate-invitation to return 400 (expired)
    await userContext.route(`**/api/v1/auth/validate-invitation`, async route => {
      await route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({
          message: 'Invitation has expired',
        }),
      });
    });

    await userPage.goto('/accept-invite?token=expired-token');
    await userPage.waitForLoadState('networkidle');

    // Should show "Invalid Invitation" error card
    await expect(userPage.getByText(/invalid invitation/i).first()).toBeVisible({
      timeout: 10_000,
    });

    // Password fields should NOT be visible (form is not rendered)
    await expect(userPage.getByLabel(/^password$/i)).not.toBeVisible();

    await userContext.close();
  });

  test('admin resends a pending invitation', async ({ page }) => {
    await setupInvitationPageMocks(page);

    await page.goto('/admin/users/invitations', { waitUntil: 'domcontentloaded' });

    // Wait for invitation data to render
    await expect(page.getByText('newuser@example.com').first()).toBeVisible({ timeout: 10_000 });

    // Find and click the Resend button (aria-label includes email)
    const resendButton = page.getByRole('button', { name: /resend/i }).first();
    await expect(resendButton).toBeVisible();
    await resendButton.click();

    // After clicking resend, verify no error state is shown
    await expect(page.getByText(/failed to resend/i)).not.toBeVisible();
  });
});
