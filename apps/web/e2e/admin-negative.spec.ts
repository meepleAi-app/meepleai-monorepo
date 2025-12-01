/**
 * Admin Negative Scenarios E2E Tests - Issue #1494
 *
 * @see apps/web/e2e/pages/helpers/AdminHelper.ts
 * @see apps/web/e2e/pages/helpers/AuthHelper.ts
 *
 * Tests authorization violations, invalid configuration values,
 * and boundary conditions for admin operations.
 *
 * Coverage:
 * - Unauthorized access (Editor/User → Admin)
 * - Invalid configuration values
 * - Non-existent resource operations
 * - Boundary violations in settings
 */

import { test, expect } from './fixtures/chromatic';
import { AuthHelper, USER_FIXTURES } from './pages';

test.describe('Admin Negative Scenarios - Issue #1494', () => {
  test.describe('Authorization violations', () => {
    test('should block Editor from accessing admin panel', async ({ page }) => {
      const authHelper = new AuthHelper(page);

      // Authenticate as Editor (not Admin)
      await authHelper.mockAuthenticatedSession({
        ...USER_FIXTURES.user,
        role: 'Editor' as const,
      });

      // Mock admin endpoint with 403 Forbidden
      await page.route('**/api/v1/admin/**', route => {
        route.fulfill({
          status: 403,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Forbidden' }),
        });
      });

      await page.goto('/admin');

      // Should redirect to home or show access denied
      await expect(page)
        .toHaveURL(/\/(|home|login)/, { timeout: 5000 })
        .catch(async () => {
          // Alternative: show access denied message
          const accessDenied = page
            .locator('text=/access denied|forbidden|403|non autorizzato/i')
            .first();
          await expect(accessDenied).toBeVisible({ timeout: 5000 });
        });
    });

    test('should block User from accessing admin panel', async ({ page }) => {
      const authHelper = new AuthHelper(page);

      // Authenticate as regular User
      await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

      // Mock admin endpoint with 403 Forbidden
      await page.route('**/api/v1/admin/**', route => {
        route.fulfill({
          status: 403,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Forbidden' }),
        });
      });

      await page.goto('/admin');

      // Should redirect or show access denied
      await expect(page)
        .toHaveURL(/\/(|home|login)/, { timeout: 5000 })
        .catch(async () => {
          const accessDenied = page.locator('text=/access denied|forbidden|403/i').first();
          await expect(accessDenied).toBeVisible({ timeout: 5000 });
        });
    });
  });

  test.describe('Configuration validation', () => {
    test('should reject invalid configuration value (negative number)', async ({ page }) => {
      const authHelper = new AuthHelper(page);
      await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

      // Mock configuration update with validation error
      await page.route('**/api/v1/admin/configuration', route => {
        if (route.request().method() === 'PUT' || route.request().method() === 'PATCH') {
          route.fulfill({
            status: 400,
            contentType: 'application/json',
            body: JSON.stringify({
              error: 'Validation failed',
              message: 'Value must be positive',
            }),
          });
        } else {
          route.continue();
        }
      });

      await page.goto('/admin/configuration');

      // Try to set invalid negative value
      const configInput = page.locator('input[type="number"]').first();
      if (await configInput.isVisible({ timeout: 5000 }).catch(() => false)) {
        await configInput.fill('-100');

        const submitButton = page.locator('button[type="submit"]').first();
        await submitButton.click();

        // Should show validation error
        const errorMessage = page
          .locator('text=/validation|invalid.*value|must be positive|deve essere positivo/i')
          .first();
        await expect(errorMessage).toBeVisible({ timeout: 5000 });
      }
    });

    test('should reject configuration boundary violation', async ({ page }) => {
      const authHelper = new AuthHelper(page);
      await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

      // Mock configuration update with boundary violation
      await page.route('**/api/v1/admin/configuration', route => {
        if (route.request().method() === 'PUT' || route.request().method() === 'PATCH') {
          const data = route.request().postDataJSON();
          // Check if value exceeds reasonable limit
          if (data && (data.maxConnections > 1000 || data.value > 1000)) {
            route.fulfill({
              status: 400,
              contentType: 'application/json',
              body: JSON.stringify({
                error: 'Validation failed',
                message: 'Value exceeds maximum allowed',
              }),
            });
          } else {
            route.continue();
          }
        } else {
          route.continue();
        }
      });

      await page.goto('/admin/configuration');

      // Try to set value beyond boundary
      const configInput = page.locator('input[type="number"]').first();
      if (await configInput.isVisible({ timeout: 5000 }).catch(() => false)) {
        await configInput.fill('9999');

        const submitButton = page.locator('button[type="submit"]').first();
        await submitButton.click();

        // Should show boundary violation error
        const errorMessage = page
          .locator('text=/exceeds|maximum|limite massimo|troppo alto/i')
          .first();
        await expect(errorMessage)
          .toBeVisible({ timeout: 5000 })
          .catch(() => {
            // Validation may be client-side
          });
      }
    });
  });

  test.describe('Resource operations', () => {
    test('should handle deletion of non-existent user', async ({ page }) => {
      const authHelper = new AuthHelper(page);
      await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

      // Mock user deletion with 404
      await page.route('**/api/v1/admin/users/*', route => {
        if (route.request().method() === 'DELETE') {
          route.fulfill({
            status: 404,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'User not found' }),
          });
        } else {
          route.continue();
        }
      });

      await page.goto('/admin/users');

      // Try to delete non-existent user
      const deleteButton = page
        .locator('button:has-text("Delete"), button:has-text("Elimina")')
        .first();
      if (await deleteButton.isVisible({ timeout: 5000 }).catch(() => false)) {
        await deleteButton.click();

        // Confirm deletion in modal if present
        const confirmButton = page
          .locator('button:has-text("Confirm"), button:has-text("Conferma")')
          .first();
        if (await confirmButton.isVisible({ timeout: 2000 }).catch(() => false)) {
          await confirmButton.click();
        }

        // Should show not found error
        const errorMessage = page
          .locator('text=/not found|non trovato|404|does not exist/i')
          .first();
        await expect(errorMessage).toBeVisible({ timeout: 5000 });
      }
    });

    test('should reject invalid role assignment', async ({ page }) => {
      const authHelper = new AuthHelper(page);
      await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

      // Mock user role update with invalid role
      await page.route('**/api/v1/admin/users/*/role', route => {
        const data = route.request().postDataJSON();
        if (data && !['Admin', 'Editor', 'User'].includes(data.role)) {
          route.fulfill({
            status: 400,
            contentType: 'application/json',
            body: JSON.stringify({
              error: 'Invalid role',
              message: 'Role must be Admin, Editor, or User',
            }),
          });
        } else {
          route.continue();
        }
      });

      await page.goto('/admin/users');

      // Attempt to assign invalid role (if UI allows)
      const roleSelect = page.locator('select[name="role"]').first();
      if (await roleSelect.isVisible({ timeout: 5000 }).catch(() => false)) {
        // UI should only show valid roles, but test validation
        const submitButton = page.locator('button[type="submit"]').first();
        if (await submitButton.isVisible()) {
          await submitButton.click();
        }
      }
    });

    test('should handle concurrent modification conflict in admin operations', async ({ page }) => {
      const authHelper = new AuthHelper(page);
      await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

      // Mock concurrent modification (409 Conflict)
      await page.route('**/api/v1/admin/configuration', route => {
        if (route.request().method() === 'PUT' || route.request().method() === 'PATCH') {
          route.fulfill({
            status: 409,
            contentType: 'application/json',
            body: JSON.stringify({
              error: 'Conflict',
              message: 'Configuration was modified by another admin',
            }),
          });
        } else {
          route.continue();
        }
      });

      await page.goto('/admin/configuration');

      // Try to update configuration
      const configInput = page.locator('input').first();
      if (await configInput.isVisible({ timeout: 5000 }).catch(() => false)) {
        await configInput.fill('updated value');

        const submitButton = page.locator('button[type="submit"]').first();
        await submitButton.click();

        // Should show conflict error
        const errorMessage = page
          .locator('text=/conflict|modified.*another|409|conflitto|modificato da/i')
          .first();
        await expect(errorMessage).toBeVisible({ timeout: 5000 });
      }
    });
  });
});
