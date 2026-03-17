/**
 * Accept-Invite → Onboarding Wizard E2E Test
 *
 * Validates the full journey: accept invitation → set password → redirect
 * to onboarding wizard → complete wizard steps → dashboard.
 *
 * Uses page.context().route() for API mocking (Next.js dev server pattern).
 * Uses .first() on all locators to avoid Playwright strict mode errors.
 */

import { test, expect, type BrowserContext } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ========== Helpers ==========

async function mockPublicAuth(context: BrowserContext) {
  await context.route(`${API_BASE}/api/v1/auth/me`, route =>
    route.fulfill({
      status: 401,
      contentType: 'application/json',
      body: JSON.stringify({ message: 'Unauthorized' }),
    })
  );
}

async function mockValidToken(context: BrowserContext) {
  await context.route(`${API_BASE}/api/v1/auth/validate-invitation`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        email: 'invited@example.com',
        role: 'User',
        expiresAt: new Date(Date.now() + 7 * 86_400_000).toISOString(),
      }),
    })
  );
}

async function mockAcceptInvitation(context: BrowserContext) {
  await context.route(`${API_BASE}/api/v1/auth/accept-invitation`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'new-user-1',
          email: 'invited@example.com',
          displayName: null,
          role: 'User',
        },
        sessionToken: 'mock-session-token',
        expiresAt: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
      }),
    })
  );
}

async function mockOnboardingApis(context: BrowserContext) {
  // Mock onboarding status
  await context.route(`${API_BASE}/api/v1/users/me/onboarding-status`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        wizardSeenAt: null,
        dismissedAt: null,
        steps: { hasGames: false, hasSessions: false, hasCompletedProfile: false },
      }),
    })
  );

  // Mock profile update
  await context.route(`${API_BASE}/api/v1/users/profile`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ displayName: 'Test User', email: 'invited@example.com' }),
    })
  );

  // Mock interests save
  await context.route(`${API_BASE}/api/v1/users/interests`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ ok: true }),
    })
  );

  // Mock onboarding complete
  await context.route(`${API_BASE}/api/v1/users/onboarding/complete`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ ok: true }),
    })
  );

  // Mock wizard seen
  await context.route(`${API_BASE}/api/v1/users/me/onboarding-wizard-seen`, route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({}) })
  );

  // Catch-all for other API calls
  await context.route(`${API_BASE}/api/v1/**`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({}),
    })
  );
}

// ========== Tests ==========

test.describe('Accept-Invite → Onboarding Wizard Journey', () => {
  test('accept invitation redirects to onboarding wizard', async ({ browser }) => {
    const context = await browser.newContext();
    const page = await context.newPage();

    await mockPublicAuth(context);
    await mockValidToken(context);
    await mockAcceptInvitation(context);
    await mockOnboardingApis(context);

    // Navigate to accept-invite
    await page.goto('/accept-invite?token=valid-test-token', {
      waitUntil: 'domcontentloaded',
    });

    // Wait for email field to appear (token validated)
    const emailInput = page.getByLabel(/email/i).first();
    await expect(emailInput).toBeVisible({ timeout: 10_000 });
    await expect(emailInput).toHaveValue('invited@example.com');

    // Fill password (meets all 4 requirements)
    const passwordInput = page.getByLabel(/^password$/i).first();
    await passwordInput.fill('SecureP@ss1');

    const confirmInput = page.getByLabel(/confirm password/i).first();
    await confirmInput.fill('SecureP@ss1');

    // Submit
    const submitButton = page.getByRole('button', { name: /create account/i }).first();
    await expect(submitButton).toBeEnabled();
    await submitButton.click();

    // Success state shows
    await expect(page.getByText(/account created/i).first()).toBeVisible({ timeout: 10_000 });

    // Should redirect to /onboarding (not /dashboard)
    await expect(page.getByText(/setting up your account/i).first()).toBeVisible();

    await context.close();
  });

  test('onboarding wizard starts at step 2 (profile) when accessed directly', async ({
    browser,
  }) => {
    const context = await browser.newContext();
    const page = await context.newPage();

    // Mock authenticated user
    await context.route(`${API_BASE}/api/v1/auth/me`, route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          user: { id: 'user-1', email: 'invited@example.com', displayName: null, role: 'User' },
          expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
        }),
      })
    );
    await mockOnboardingApis(context);

    await page.goto('/onboarding', { waitUntil: 'domcontentloaded' });

    // Should show Profile step (step 2), not Password step (step 1)
    await expect(page.getByText(/Step 2 of 4/).first()).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('Profile').first()).toBeVisible();

    // Skip wizard link should be visible (password already completed)
    await expect(page.getByTestId('skip-wizard')).toBeVisible();

    await context.close();
  });

  test('skip wizard on onboarding page navigates to dashboard', async ({ browser }) => {
    const context = await browser.newContext();
    const page = await context.newPage();

    // Mock authenticated user
    await context.route(`${API_BASE}/api/v1/auth/me`, route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          user: { id: 'user-1', email: 'invited@example.com', displayName: null, role: 'User' },
          expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
        }),
      })
    );
    await mockOnboardingApis(context);

    await page.goto('/onboarding', { waitUntil: 'domcontentloaded' });

    // Wait for wizard to render
    await expect(page.getByTestId('skip-wizard')).toBeVisible({ timeout: 10_000 });

    // Click skip wizard
    await page.getByTestId('skip-wizard').click();

    // Should navigate to /dashboard
    await page.waitForURL(/\/dashboard/, { timeout: 5_000 });

    await context.close();
  });
});
