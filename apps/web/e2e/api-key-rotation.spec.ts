/**
 * API Key Rotation E2E Tests - Issue #2193
 *
 * Tests the API key rotation flow:
 * - View existing API keys
 * - Rotate API key
 * - Verify old key is invalidated
 * - Copy new key
 *
 * @see apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/ApiKeys/
 * @see apps/web/e2e/api/api-keys-flow.spec.ts (related tests)
 * @see docs/02-development/testing/test-coverage-gaps.md
 */

import { test, expect } from './fixtures';
import { AuthHelper, AdminHelper, USER_FIXTURES } from './pages';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Mock API key data
const MOCK_API_KEYS = [
  {
    id: 'key-1',
    keyName: 'Production API Key',
    keyPrefix: 'mpl_prod_abc',
    scopes: 'read:games,read:rules',
    createdAt: '2025-01-01T10:00:00Z',
    expiresAt: '2026-01-01T10:00:00Z',
    lastUsedAt: '2025-01-10T10:00:00Z',
    isActive: true,
  },
  {
    id: 'key-2',
    keyName: 'Development Key',
    keyPrefix: 'mpl_dev_xyz',
    scopes: 'read:games,write:games,read:rules,write:rules',
    createdAt: '2025-01-02T10:00:00Z',
    expiresAt: '2025-06-01T10:00:00Z',
    lastUsedAt: null,
    isActive: true,
  },
];

/**
 * API Key Rotation E2E Tests
 *
 * Tests the complete API key rotation flow with proper auth mocking.
 * Uses AdminHelper for catch-all mocks that prevent unmocked route errors.
 *
 * @see apps/web/src/app/admin/api-keys/client.tsx
 */
test.describe('API Key Rotation Flow - Issue #2193', () => {
  let apiKeys: typeof MOCK_API_KEYS;

  test.beforeEach(async ({ page }) => {
    // Reset keys for each test
    apiKeys = [...MOCK_API_KEYS];

    const authHelper = new AuthHelper(page);
    const adminHelper = new AdminHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Mock admin session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

    // Catch-all for admin endpoints (prevents unmocked route errors)
    await page.route(`${API_BASE}/api/v1/admin/**`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: [] }),
      });
    });

    // Mock admin stats (commonly needed by admin pages)
    await adminHelper.mockAdminStats({
      totalUsers: 10,
      totalGames: 5,
      totalQueries: 100,
      avgLatencyMs: 350,
      totalTokens: 50000,
      successRate: 0.95,
    });

    // Mock API keys list endpoint
    await page.route(`${API_BASE}/api/v1/api-keys*`, async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: apiKeys,
            total: apiKeys.length,
          }),
        });
      } else if (route.request().method() === 'POST') {
        // Create new key
        const body = route.request().postDataJSON() as {
          keyName: string;
          scopes: string;
          expiresAt?: string;
        };
        const newKey = {
          id: `key-${Date.now()}`,
          keyName: body.keyName,
          keyPrefix: `mpl_dev_${Math.random().toString(36).substring(7)}`,
          scopes: body.scopes,
          createdAt: new Date().toISOString(),
          expiresAt:
            body.expiresAt || new Date(Date.now() + 365 * 24 * 60 * 60 * 1000).toISOString(),
          lastUsedAt: null,
          isActive: true,
          plaintextKey: `mpl_dev_${Math.random().toString(36).substring(2, 15)}${Math.random().toString(36).substring(2, 15)}`,
        };
        apiKeys.push(newKey);
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify(newKey),
        });
      } else {
        await route.continue();
      }
    });

    // Mock single API key endpoint
    await page.route(new RegExp(`${API_BASE}/api/v1/api-keys/[^/]+$`), async route => {
      const url = new URL(route.request().url());
      const keyId = url.pathname.split('/').pop()!;
      const key = apiKeys.find(k => k.id === keyId);

      if (!key) {
        await route.fulfill({
          status: 404,
          body: JSON.stringify({ error: 'API key not found' }),
        });
        return;
      }

      if (route.request().method() === 'DELETE') {
        const index = apiKeys.findIndex(k => k.id === keyId);
        if (index >= 0) {
          apiKeys.splice(index, 1);
        }
        await route.fulfill({ status: 204 });
      } else if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(key),
        });
      } else {
        await route.continue();
      }
    });

    // Mock API key rotation endpoint
    await page.route(new RegExp(`${API_BASE}/api/v1/api-keys/[^/]+/rotate$`), async route => {
      const url = new URL(route.request().url());
      const pathParts = url.pathname.split('/');
      const keyId = pathParts[pathParts.length - 2];
      const oldKey = apiKeys.find(k => k.id === keyId);

      if (!oldKey) {
        await route.fulfill({
          status: 404,
          body: JSON.stringify({ error: 'API key not found' }),
        });
        return;
      }

      const body =
        (route.request().postDataJSON() as {
          reason?: string;
          preserveScopes?: boolean;
          newScopes?: string;
          newExpiresAt?: string;
        }) || {};

      // Create new key
      const newKey = {
        id: `key-rotated-${Date.now()}`,
        keyName: `${oldKey.keyName} (Rotated)`,
        keyPrefix: `mpl_dev_${Math.random().toString(36).substring(7)}`,
        scopes: body.preserveScopes !== false ? oldKey.scopes : body.newScopes || oldKey.scopes,
        createdAt: new Date().toISOString(),
        expiresAt: body.newExpiresAt || oldKey.expiresAt,
        lastUsedAt: null,
        isActive: true,
        plaintextKey: `mpl_dev_${Math.random().toString(36).substring(2, 15)}${Math.random().toString(36).substring(2, 15)}`,
        rotatedFrom: keyId,
      };

      // Mark old key as inactive
      oldKey.isActive = false;

      // Add new key
      apiKeys.push(newKey);

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          message: 'API key rotated successfully',
          oldKeyId: keyId,
          newApiKey: newKey,
        }),
      });
    });
  });

  test('should display existing API keys', async ({ page }) => {
    await page.goto('/admin/api-keys');

    // Wait for keys to load
    await expect(page.getByRole('heading', { name: /API Keys|Chiavi API/i })).toBeVisible({
      timeout: 10000,
    });

    // Verify keys are displayed
    await expect(page.getByText('Production API Key')).toBeVisible();
    await expect(page.getByText('Development Key')).toBeVisible();

    // Verify key prefixes are shown
    await expect(page.getByText('mpl_prod_abc')).toBeVisible();
    await expect(page.getByText('mpl_dev_xyz')).toBeVisible();
  });

  test('should rotate API key successfully', async ({ page }) => {
    await page.goto('/admin/api-keys');

    // Wait for keys to load
    await expect(page.getByText('Production API Key')).toBeVisible();

    // Find rotate button on first key
    const keyRow = page.locator('[data-testid="api-key-row"], .api-key-row, tr').filter({
      hasText: 'Production API Key',
    });

    const rotateButton = keyRow
      .getByRole('button', { name: /Rotate|Ruota/i })
      .or(keyRow.locator('[data-testid="rotate-key-button"]'));

    await rotateButton.click();

    // Confirm rotation in dialog
    const confirmDialog = page.locator('[role="dialog"], [role="alertdialog"]');
    if (await confirmDialog.isVisible({ timeout: 2000 }).catch(() => false)) {
      const confirmButton = confirmDialog.getByRole('button', { name: /Confirm|Conferma|Rotate/i });
      await confirmButton.click();
    }

    // Verify success message
    await expect(
      page.locator('text=rotated|ruotata|success|successo', { exact: false })
    ).toBeVisible({ timeout: 5000 });

    // Verify new key is shown (with rotated suffix)
    await expect(page.getByText(/Rotated|ruotata/i)).toBeVisible();
  });

  test('should show new key after rotation (copy functionality)', async ({ page }) => {
    await page.goto('/admin/api-keys');

    // Wait for keys
    await expect(page.getByText('Development Key')).toBeVisible();

    // Rotate the key
    const keyRow = page.locator('[data-testid="api-key-row"], .api-key-row, tr').filter({
      hasText: 'Development Key',
    });

    const rotateButton = keyRow
      .getByRole('button', { name: /Rotate|Ruota/i })
      .or(keyRow.locator('[data-testid="rotate-key-button"]'));

    await rotateButton.click();

    // Handle confirmation
    const confirmButton = page.getByRole('button', { name: /Confirm|Conferma|Rotate/i });
    if (await confirmButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await confirmButton.click();
    }

    // After rotation, new key should be visible with copy button
    await expect(page.locator('text=mpl_dev_|new key|nuova chiave', { exact: false })).toBeVisible({
      timeout: 5000,
    });

    // Verify copy button exists
    const copyButton = page
      .getByRole('button', { name: /Copy|Copia/i })
      .or(page.locator('[data-testid="copy-key-button"]'));

    if (await copyButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await expect(copyButton).toBeEnabled();
    }
  });

  test('should preserve scopes during rotation', async ({ page }) => {
    await page.goto('/admin/api-keys');

    // Wait for keys with specific scopes
    await expect(page.getByText('read:games,write:games')).toBeVisible();

    // Rotate the Development Key (has write scopes)
    const keyRow = page.locator('[data-testid="api-key-row"], .api-key-row, tr').filter({
      hasText: 'Development Key',
    });

    const rotateButton = keyRow.getByRole('button', { name: /Rotate|Ruota/i });
    await rotateButton.click();

    // Verify "preserve scopes" option is checked by default
    const preserveScopesCheckbox = page
      .locator('input[name="preserveScopes"]')
      .or(page.getByLabel(/preserve|mantieni|scopes/i));

    if (await preserveScopesCheckbox.isVisible({ timeout: 2000 }).catch(() => false)) {
      await expect(preserveScopesCheckbox).toBeChecked();
    }

    // Confirm
    const confirmButton = page.getByRole('button', { name: /Confirm|Conferma|Rotate/i });
    if (await confirmButton.isVisible()) {
      await confirmButton.click();
    }

    // Verify scopes are preserved
    await expect(page.getByText('read:games,write:games,read:rules,write:rules')).toBeVisible({
      timeout: 5000,
    });
  });

  test('should show old key as inactive after rotation', async ({ page }) => {
    await page.goto('/admin/api-keys');

    // Wait for keys
    await expect(page.getByText('Production API Key')).toBeVisible();

    // Rotate
    const keyRow = page.locator('[data-testid="api-key-row"], .api-key-row, tr').filter({
      hasText: 'Production API Key',
    });

    const rotateButton = keyRow.getByRole('button', { name: /Rotate|Ruota/i });
    await rotateButton.click();

    // Confirm
    const confirmButton = page.getByRole('button', { name: /Confirm|Conferma|Rotate/i });
    if (await confirmButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await confirmButton.click();
    }

    // Wait for rotation to complete
    await expect(page.locator('text=rotated|ruotata|success', { exact: false })).toBeVisible({
      timeout: 5000,
    });

    // The old key should show as inactive or be removed
    // Depending on UI implementation, either:
    // 1. Key is marked as inactive
    // 2. Key is removed from the list
    const inactiveIndicator = page.locator('text=inactive|inattiva|revoked|revocata', {
      exact: false,
    });

    const oldKeyStillVisible = await page
      .getByText('mpl_prod_abc')
      .isVisible()
      .catch(() => false);

    if (oldKeyStillVisible) {
      // If old key is still visible, it should be marked as inactive
      await expect(inactiveIndicator).toBeVisible();
    }
  });

  test('should allow setting new expiration during rotation', async ({ page }) => {
    await page.goto('/admin/api-keys');

    await expect(page.getByText('Production API Key')).toBeVisible();

    // Rotate
    const keyRow = page.locator('[data-testid="api-key-row"], .api-key-row, tr').filter({
      hasText: 'Production API Key',
    });

    const rotateButton = keyRow.getByRole('button', { name: /Rotate|Ruota/i });
    await rotateButton.click();

    // Look for expiration date input
    const expirationInput = page
      .locator('input[name="newExpiresAt"], input[type="date"], input[type="datetime-local"]')
      .or(page.locator('[data-testid="new-expiration-input"]'));

    if (await expirationInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      // Set new expiration to 1 year from now
      const newDate = new Date(Date.now() + 365 * 24 * 60 * 60 * 1000);
      await expirationInput.fill(newDate.toISOString().split('T')[0]);
    }

    // Confirm
    const confirmButton = page.getByRole('button', { name: /Confirm|Conferma|Rotate/i });
    if (await confirmButton.isVisible()) {
      await confirmButton.click();
    }

    // Verify success
    await expect(page.locator('text=rotated|ruotata|success', { exact: false })).toBeVisible({
      timeout: 5000,
    });
  });

  test('should handle rotation error gracefully', async ({ page }) => {
    // Override rotation to return error
    await page.route(new RegExp(`${API_BASE}/api/v1/api-keys/[^/]+/rotate$`), async route => {
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Failed to rotate key: internal error' }),
      });
    });

    await page.goto('/admin/api-keys');

    await expect(page.getByText('Production API Key')).toBeVisible();

    // Try to rotate
    const keyRow = page.locator('[data-testid="api-key-row"], .api-key-row, tr').filter({
      hasText: 'Production API Key',
    });

    const rotateButton = keyRow.getByRole('button', { name: /Rotate|Ruota/i });
    await rotateButton.click();

    // Confirm
    const confirmButton = page.getByRole('button', { name: /Confirm|Conferma|Rotate/i });
    if (await confirmButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await confirmButton.click();
    }

    // Verify error message
    await expect(page.locator('text=error|errore|failed|fallito', { exact: false })).toBeVisible({
      timeout: 5000,
    });
  });

  test('should require confirmation before rotation', async ({ page }) => {
    await page.goto('/admin/api-keys');

    await expect(page.getByText('Production API Key')).toBeVisible();

    // Click rotate
    const keyRow = page.locator('[data-testid="api-key-row"], .api-key-row, tr').filter({
      hasText: 'Production API Key',
    });

    const rotateButton = keyRow.getByRole('button', { name: /Rotate|Ruota/i });
    await rotateButton.click();

    // Verify confirmation dialog appears
    const dialog = page.locator('[role="dialog"], [role="alertdialog"]');
    await expect(dialog).toBeVisible({ timeout: 2000 });

    // Verify dialog has warning text
    await expect(
      dialog.locator('text=confirm|conferma|sure|sicuro|irreversible|irreversibile', {
        exact: false,
      })
    ).toBeVisible();

    // Cancel button should close dialog
    const cancelButton = dialog.getByRole('button', { name: /Cancel|Annulla/i });
    if (await cancelButton.isVisible()) {
      await cancelButton.click();
      await expect(dialog).not.toBeVisible({ timeout: 2000 });
    }
  });

  test('should show empty state when no API keys exist', async ({ page }) => {
    // Override to return empty list
    await page.route(`${API_BASE}/api/v1/api-keys*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [],
          total: 0,
        }),
      });
    });

    await page.goto('/admin/api-keys');

    // Verify empty state
    await expect(
      page.locator('text=nessuna|no api keys|empty|crea|create', { exact: false })
    ).toBeVisible({ timeout: 10000 });
  });

  test('should require admin access for key rotation', async ({ page }) => {
    // Mock as regular user (not admin)
    const authHelper = new AuthHelper(page);
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // Override to return 403
    await page.route(new RegExp(`${API_BASE}/api/v1/api-keys/[^/]+/rotate$`), async route => {
      await route.fulfill({
        status: 403,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Admin access required' }),
      });
    });

    await page.goto('/admin/api-keys');

    // Should either redirect or show access denied
    const accessDenied = await page
      .locator('text=403|forbidden|denied|accesso negato|unauthorized', { exact: false })
      .isVisible({ timeout: 5000 })
      .catch(() => false);

    const redirected = page.url().includes('/login') || page.url().includes('/unauthorized');

    // If user can see the page, rotation should be blocked
    if (!accessDenied && !redirected) {
      // Try to access rotation - should fail
      const rotateButton = page.getByRole('button', { name: /Rotate|Ruota/i }).first();
      if (await rotateButton.isVisible().catch(() => false)) {
        await rotateButton.click();
        // Should show error or be disabled
        const confirmButton = page.getByRole('button', { name: /Confirm|Conferma/i });
        if (await confirmButton.isVisible({ timeout: 2000 }).catch(() => false)) {
          await confirmButton.click();
          await expect(
            page.locator('text=forbidden|denied|error|errore', { exact: false })
          ).toBeVisible({ timeout: 5000 });
        }
      }
    }
  });

  test('should display key metadata (created, last used, expiration)', async ({ page }) => {
    await page.goto('/admin/api-keys');

    // Wait for keys to load
    await expect(page.getByText('Production API Key')).toBeVisible();

    // Verify metadata is displayed
    // Created date
    await expect(page.getByText(/2025-01-01|01\/01\/2025|January/i)).toBeVisible();

    // Expiration date
    await expect(page.getByText(/2026-01-01|01\/01\/2026|January 2026/i)).toBeVisible();

    // Last used (for Production key)
    await expect(page.getByText(/2025-01-10|10\/01\/2025|January 10/i)).toBeVisible();
  });
});
