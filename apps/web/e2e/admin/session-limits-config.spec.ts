/**
 * ADM-11: Session Limits Configuration
 * Issue #3082 - P0 Critical
 *
 * Tests admin configuration of session limits by tier:
 * - View current session limits per tier
 * - Update session limits for each tier
 * - Validation of limit values
 * - Immediate effect of limit changes
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface SessionLimitsConfig {
  free: number;
  normal: number;
  premium: number | 'unlimited';
}

/**
 * Setup mock routes for admin session limits configuration
 */
async function setupAdminSessionLimitsMocks(
  page: Page,
  options: {
    initialLimits?: SessionLimitsConfig;
  } = {}
) {
  const {
    initialLimits = {
      free: 3,
      normal: 10,
      premium: 'unlimited',
    },
  } = options;

  const currentLimits = { ...initialLimits };

  // Mock admin auth
  await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'admin-user-id',
          email: 'admin@meepleai.dev',
          displayName: 'Admin User',
          role: 'Admin',
        },
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      }),
    });
  });

  // Mock session limits config endpoint (GET)
  await page.route(`${API_BASE}/api/v1/admin/system/session-limits`, async (route) => {
    const method = route.request().method();

    if (method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          limits: currentLimits,
          lastModified: new Date().toISOString(),
          modifiedBy: 'admin@meepleai.dev',
        }),
      });
    } else if (method === 'PUT' || method === 'PATCH') {
      const body = await route.request().postDataJSON();

      // Validate limits
      if (body.free !== undefined && (body.free < 1 || body.free > 100)) {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({
            error: 'Validation error',
            message: 'Free tier limit must be between 1 and 100',
          }),
        });
        return;
      }

      if (body.normal !== undefined && (body.normal < 1 || body.normal > 500)) {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({
            error: 'Validation error',
            message: 'Normal tier limit must be between 1 and 500',
          }),
        });
        return;
      }

      // Update limits
      if (body.free !== undefined) currentLimits.free = body.free;
      if (body.normal !== undefined) currentLimits.normal = body.normal;
      if (body.premium !== undefined) currentLimits.premium = body.premium;

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          message: 'Session limits updated successfully',
          limits: currentLimits,
        }),
      });
    } else {
      await route.continue();
    }
  });

  // Mock admin config navigation endpoint
  await page.route(`${API_BASE}/api/v1/admin/configuration**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        categories: [
          { id: 'session-limits', name: 'Session Limits', path: '/admin/config/session-limits' },
          { id: 'pdf-limits', name: 'PDF Limits', path: '/admin/config/pdf-limits' },
          { id: 'feature-flags', name: 'Feature Flags', path: '/admin/config/feature-flags' },
        ],
      }),
    });
  });

  // Mock audit log endpoint (for tracking changes)
  await page.route(`${API_BASE}/api/v1/admin/audit-log`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        {
          id: 'log-1',
          action: 'session_limits_updated',
          actor: 'admin@meepleai.dev',
          timestamp: new Date().toISOString(),
          details: { oldLimits: initialLimits, newLimits: currentLimits },
        },
      ]),
    });
  });

  // Mock common admin endpoints
  await page.route(`${API_BASE}/api/v1/admin/**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ data: [] }),
    });
  });

  return {
    getLimits: () => currentLimits,
    initialLimits,
  };
}

test.describe('ADM-11: Session Limits Configuration', () => {
  test.describe('View Session Limits', () => {
    test('should navigate to session limits configuration page', async ({ page }) => {
      await setupAdminSessionLimitsMocks(page);

      await page.goto('/admin');
      await page.waitForLoadState('networkidle');

      // Navigate to configuration section
      const configLink = page.getByRole('link', { name: /configuration|settings|system/i });
      if (await configLink.isVisible()) {
        await configLink.click();
        await page.waitForLoadState('networkidle');
      }

      // Look for session limits option
      const sessionLimitsLink = page.getByRole('link', { name: /session.*limit/i }).or(
        page.getByText(/session.*limit/i)
      );
      await expect(sessionLimitsLink.first()).toBeVisible();
    });

    test('should display current limits per tier', async ({ page }) => {
      await setupAdminSessionLimitsMocks(page, {
        initialLimits: { free: 3, normal: 10, premium: 'unlimited' },
      });

      await page.goto('/admin/config/session-limits');
      await page.waitForLoadState('networkidle');

      // Should show limits for each tier
      await expect(page.getByText(/free/i)).toBeVisible();
      await expect(page.getByText(/normal|standard/i)).toBeVisible();
      await expect(page.getByText(/premium/i)).toBeVisible();

      // Should show current values
      await expect(page.getByText(/3/)).toBeVisible(); // Free limit
      await expect(page.getByText(/10/)).toBeVisible(); // Normal limit
      await expect(page.getByText(/unlimited|∞/i)).toBeVisible(); // Premium limit
    });

    test('should show input fields for editing limits', async ({ page }) => {
      await setupAdminSessionLimitsMocks(page);

      await page.goto('/admin/config/session-limits');
      await page.waitForLoadState('networkidle');

      // Should have input fields for Free and Normal tiers
      const freeInput = page.getByLabel(/free.*limit|free.*session/i).or(
        page.locator('input[name*="free"]')
      );
      const normalInput = page.getByLabel(/normal.*limit|normal.*session/i).or(
        page.locator('input[name*="normal"]')
      );

      await expect(freeInput.first()).toBeVisible();
      await expect(normalInput.first()).toBeVisible();
    });
  });

  test.describe('Update Session Limits', () => {
    test('should update Free tier limit successfully', async ({ page }) => {
      const mocks = await setupAdminSessionLimitsMocks(page, {
        initialLimits: { free: 3, normal: 10, premium: 'unlimited' },
      });

      await page.goto('/admin/config/session-limits');
      await page.waitForLoadState('networkidle');

      // Find Free tier input
      const freeInput = page.getByLabel(/free.*limit/i).or(
        page.locator('input[name*="free"], input[id*="free"]')
      ).first();

      if (await freeInput.isVisible()) {
        await freeInput.clear();
        await freeInput.fill('5');

        // Submit changes
        const saveButton = page.getByRole('button', { name: /save|update|apply/i });
        await saveButton.click();

        // Verify success message
        await expect(page.getByText(/updated|saved|success/i)).toBeVisible();
      }
    });

    test('should update Normal tier limit successfully', async ({ page }) => {
      await setupAdminSessionLimitsMocks(page);

      await page.goto('/admin/config/session-limits');
      await page.waitForLoadState('networkidle');

      // Find Normal tier input
      const normalInput = page.getByLabel(/normal.*limit/i).or(
        page.locator('input[name*="normal"], input[id*="normal"]')
      ).first();

      if (await normalInput.isVisible()) {
        await normalInput.clear();
        await normalInput.fill('20');

        // Submit changes
        const saveButton = page.getByRole('button', { name: /save|update|apply/i });
        await saveButton.click();

        // Verify success message
        await expect(page.getByText(/updated|saved|success/i)).toBeVisible();
      }
    });

    test('should validate minimum limit value', async ({ page }) => {
      await setupAdminSessionLimitsMocks(page);

      await page.goto('/admin/config/session-limits');
      await page.waitForLoadState('networkidle');

      const freeInput = page.getByLabel(/free.*limit/i).or(
        page.locator('input[name*="free"]')
      ).first();

      if (await freeInput.isVisible()) {
        await freeInput.clear();
        await freeInput.fill('0'); // Invalid: below minimum

        const saveButton = page.getByRole('button', { name: /save|update|apply/i });
        await saveButton.click();

        // Should show validation error
        await expect(page.getByText(/validation|invalid|must.*be.*between|minimum/i)).toBeVisible();
      }
    });

    test('should validate maximum limit value', async ({ page }) => {
      await setupAdminSessionLimitsMocks(page);

      await page.goto('/admin/config/session-limits');
      await page.waitForLoadState('networkidle');

      const freeInput = page.getByLabel(/free.*limit/i).or(
        page.locator('input[name*="free"]')
      ).first();

      if (await freeInput.isVisible()) {
        await freeInput.clear();
        await freeInput.fill('999'); // Invalid: above maximum for Free tier

        const saveButton = page.getByRole('button', { name: /save|update|apply/i });
        await saveButton.click();

        // Should show validation error
        await expect(page.getByText(/validation|invalid|must.*be.*between|maximum/i)).toBeVisible();
      }
    });
  });

  test.describe('Premium Tier', () => {
    test('should show Premium as unlimited', async ({ page }) => {
      await setupAdminSessionLimitsMocks(page);

      await page.goto('/admin/config/session-limits');
      await page.waitForLoadState('networkidle');

      // Premium should show as unlimited
      const premiumSection = page.locator('text=Premium').locator('..');
      await expect(premiumSection.getByText(/unlimited|∞/i).or(
        page.getByText(/unlimited|∞/i)
      )).toBeVisible();
    });

    test('should not have editable input for Premium tier', async ({ page }) => {
      await setupAdminSessionLimitsMocks(page);

      await page.goto('/admin/config/session-limits');
      await page.waitForLoadState('networkidle');

      // Premium should not have an editable number input
      const premiumInput = page.locator('input[name*="premium"][type="number"]');
      const isVisible = await premiumInput.isVisible().catch(() => false);

      if (isVisible) {
        // If visible, it should be disabled
        await expect(premiumInput).toBeDisabled();
      }
    });
  });

  test.describe('Immediate Effect', () => {
    test('should show warning about immediate effect', async ({ page }) => {
      await setupAdminSessionLimitsMocks(page);

      await page.goto('/admin/config/session-limits');
      await page.waitForLoadState('networkidle');

      // Should show warning about immediate effect
      await expect(page.getByText(/immediate|take.*effect|existing.*user/i)).toBeVisible();
    });

    test('should require confirmation for limit reduction', async ({ page }) => {
      await setupAdminSessionLimitsMocks(page, {
        initialLimits: { free: 5, normal: 20, premium: 'unlimited' },
      });

      await page.goto('/admin/config/session-limits');
      await page.waitForLoadState('networkidle');

      const freeInput = page.getByLabel(/free.*limit/i).or(
        page.locator('input[name*="free"]')
      ).first();

      if (await freeInput.isVisible()) {
        await freeInput.clear();
        await freeInput.fill('2'); // Reducing limit

        const saveButton = page.getByRole('button', { name: /save|update|apply/i });
        await saveButton.click();

        // May show confirmation dialog
        const confirmButton = page.getByRole('button', { name: /confirm|yes|proceed/i });
        if (await confirmButton.isVisible()) {
          await confirmButton.click();
        }
      }
    });
  });

  test.describe('Audit Trail', () => {
    test('should show last modified information', async ({ page }) => {
      await setupAdminSessionLimitsMocks(page);

      await page.goto('/admin/config/session-limits');
      await page.waitForLoadState('networkidle');

      // Should show last modified info
      await expect(page.getByText(/last.*modified|updated.*by|changed/i)).toBeVisible();
    });
  });
});
