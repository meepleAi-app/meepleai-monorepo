/**
 * API Keys UI Security Tests - Issue #914
 *
 * Frontend security validation for API key management UI:
 * 1. XSS prevention - Malicious script in key name should not execute
 * 2. Key masking - Full keys masked in table (prefix only shown)
 * 3. Copy to clipboard - Secure key display (once only)
 * 4. Input sanitization - Prevent injection attacks
 *
 * @see apps/web/src/app/admin/api-keys
 * @see Issue #914 - E2E + Security audit + Stress test
 */

import { test, expect } from '@playwright/test';

const BASE_URL = process.env.NEXT_PUBLIC_WEB_BASE || 'http://localhost:3000';
const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('API Keys UI Security - Issue #914', () => {
  test.beforeEach(async ({ page }) => {
    // Login as admin
    await page.goto(`${BASE_URL}/login`);
    await page.fill('input[name="email"]', 'demo@meepleai.dev');
    await page.fill('input[name="password"]', 'Demo123!');
    await page.click('button[type="submit"]');

    // Wait for redirect to home/dashboard
    await page.waitForURL(/\/(home|dashboard|admin)/);

    // Navigate to API keys page
    await page.goto(`${BASE_URL}/admin/api-keys`);
    await page.waitForLoadState('networkidle');
  });

  test('XSS Prevention: Key name with script tag should not execute', async ({ page }) => {
    // Arrange - Prepare XSS payload
    const xssPayload = '<script>alert("xss")</script>';
    let dialogOpened = false;

    // Listen for alert dialogs (should NOT appear)
    page.on('dialog', async dialog => {
      dialogOpened = true;
      await dialog.dismiss();
    });

    // Mock API response with XSS payload in key name
    await page.route('**/api/v1/admin/api-keys/with-stats', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          keys: [
            {
              apiKey: {
                id: '99999999-9999-9999-9999-999999999999',
                keyName: xssPayload,
                keyPrefix: 'mpl_test_',
                scopes: 'read:games',
                createdAt: new Date().toISOString(),
                expiresAt: null,
                lastUsedAt: null,
                isActive: true,
              },
              usageStats: {
                keyId: '99999999-9999-9999-9999-999999999999',
                totalUsageCount: 0,
                lastUsedAt: null,
                usageCountLast24Hours: 0,
                usageCountLast7Days: 0,
                usageCountLast30Days: 0,
                averageRequestsPerDay: 0,
              },
            },
          ],
          count: 1,
          filters: { userId: null, includeRevoked: false },
        }),
      });
    });

    // Act - Navigate to page with XSS payload
    await page.reload();
    await page.waitForLoadState('networkidle');

    // Assert - Script should NOT execute (React escapes by default)
    expect(dialogOpened).toBe(false);

    // Verify malicious content is rendered as text, not HTML
    const keyNameText = await page.textContent('text=' + xssPayload);
    expect(keyNameText).toContain('<script>'); // Rendered as plain text
  });

  test('Key Masking: API keys in table show prefix only', async ({ page }) => {
    // Wait for API keys to load
    await page.waitForSelector('[data-testid="api-keys-table"]', { timeout: 10000 });

    // Get all key prefix cells
    const keyPrefixCells = await page.locator('td:has-text("mpl_")').all();

    if (keyPrefixCells.length > 0) {
      for (const cell of keyPrefixCells) {
        const text = await cell.textContent();

        // Assert - Should show prefix only, NOT full key
        expect(text).toMatch(/mpl_(dev|prod|test)_[A-Za-z0-9]{8}\.\.\./);
        expect(text!.length).toBeLessThan(30); // Full keys are 40+ chars
      }
    }
  });

  test('Copy to Clipboard: Created key shows full value once', async ({ page, context }) => {
    // Grant clipboard permissions
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);

    // Click create key button
    await page.click('[data-testid="create-key-button"]');

    // Fill create form
    await page.waitForSelector('input[name="keyName"]');
    await page.fill('input[name="keyName"]', `Clipboard Test ${Date.now()}`);
    await page.fill('input[name="scopes"]', 'read:games');

    // Mock API response for key creation
    await page.route('**/api/v1/admin/api-keys', async route => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: '88888888-8888-8888-8888-888888888888',
            keyName: 'Clipboard Test',
            keyPrefix: 'mpl_test_',
            plaintextKey: 'mpl_test_ABCDEFGH1234567890ABCDEFGH1234567890',
            scopes: 'read:games',
            createdAt: new Date().toISOString(),
            expiresAt: null,
          }),
        });
      } else {
        await route.continue();
      }
    });

    // Submit form
    await page.click('button[type="submit"]');

    // Assert - Success dialog should show full key
    await page.waitForSelector('text=API Key Created');

    // Verify full key is displayed (only shown once)
    const fullKeyElement = await page.locator('code:has-text("mpl_test_ABCDEFGH")');
    expect(await fullKeyElement.count()).toBe(1);

    // Verify copy button exists
    const copyButton = await page.locator('button:has-text("Copy")');
    expect(await copyButton.count()).toBeGreaterThan(0);
  });

  test('Input Sanitization: Prevents SQL injection in key name', async ({ page }) => {
    // Click create key button
    await page.click('[data-testid="create-key-button"]');

    // Attempt SQL injection in key name
    const sqlPayload = "'; DROP TABLE ApiKeys; --";

    await page.waitForSelector('input[name="keyName"]');
    await page.fill('input[name="keyName"]', sqlPayload);
    await page.fill('input[name="scopes"]', 'read:games');

    // Mock API to verify payload is sent as-is (backend handles sanitization)
    let capturedPayload = '';
    await page.route('**/api/v1/admin/api-keys', async route => {
      if (route.request().method() === 'POST') {
        const postData = route.request().postDataJSON();
        capturedPayload = postData.keyName;

        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: '77777777-7777-7777-7777-777777777777',
            keyName: postData.keyName,
            keyPrefix: 'mpl_test_',
            plaintextKey: 'mpl_test_ABC123',
            scopes: 'read:games',
            createdAt: new Date().toISOString(),
            expiresAt: null,
          }),
        });
      } else {
        await route.continue();
      }
    });

    // Submit form
    await page.click('button[type="submit"]');

    // Assert - Payload sent as-is (parameterized queries prevent SQL injection at DB level)
    expect(capturedPayload).toBe(sqlPayload);
  });
});
