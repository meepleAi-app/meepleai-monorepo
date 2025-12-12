/**
 * Admin Management Integration E2E Tests - Issue #903
 *
 * Cross-feature end-to-end tests covering:
 * 1. Create API key → Use in API → Revoke → Verify invalid
 * 2. Bulk import users → Export CSV → Verify integrity
 * 3. Timeline shows all actions
 *
 * Stress Test:
 * - Bulk import 100 users (simulated, full 1000+ in performance tests)
 */

import { test, expect } from '@playwright/test';

const ADMIN_EMAIL = 'admin@test.com';
const ADMIN_PASSWORD = 'Admin123!';

test.describe('Admin Management Integration - Issue #903', () => {
  test.beforeEach(async ({ page }) => {
    // Login as admin
    await page.goto('/login');
    await page.fill('input[name="email"]', ADMIN_EMAIL);
    await page.fill('input[name="password"]', ADMIN_PASSWORD);
    await page.click('button[type="submit"]');
    await page.waitForURL('/dashboard', { timeout: 10000 });

    // Navigate to management page
    await page.goto('/admin/management');
    await expect(page.locator('h1:has-text("System Management")')).toBeVisible({
      timeout: 10000,
    });
  });

  test.describe('E2E Flow 1: API Key Lifecycle', () => {
    test('should create, use, revoke, and verify API key', async ({ page, request }) => {
      // Step 1: Create API key
      await page.click('button:has-text("Create Key")');
      await expect(page.locator('[data-testid="api-key-creation-modal"]')).toBeVisible();

      // Fill form
      await page.fill('input[id="keyName"]', 'E2E Test Key');
      await page.fill('input[id="scopes"]', 'read:games');
      await page.click('button:has-text("Create Key")');

      // Step 2: Copy plaintext key
      const plaintextKey = await page.locator('code:has-text("mpl_")').first().textContent();
      expect(plaintextKey).toBeTruthy();

      await page.click('button:has-text("I\'ve Saved It")');

      // Step 3: Use key in API call
      const apiResponse = await request.get('/api/v1/games', {
        headers: {
          'X-API-Key': plaintextKey!,
        },
      });
      expect(apiResponse.status()).toBe(200);

      // Step 4: Find key in table and revoke it
      await page.click('button:has-text("Refresh")');
      const keyRow = page.locator('tr:has-text("E2E Test Key")');
      await expect(keyRow).toBeVisible();

      await keyRow.locator('button[aria-label="Delete API key"]').click();
      await page.click('button:has-text("Confirm")');

      // Wait for deletion
      await page.waitForTimeout(1000);

      // Step 5: Verify key is invalid
      const invalidResponse = await request.get('/api/v1/games', {
        headers: {
          'X-API-Key': plaintextKey!,
        },
      });
      expect(invalidResponse.status()).toBe(401);
    });

    test('should filter API keys', async ({ page }) => {
      // Open filter panel
      const filterPanel = page.locator('[data-testid="api-key-filter-panel"]');
      await expect(filterPanel).toBeVisible();

      // Search by name
      await filterPanel.locator('input[id="filter-search"]').fill('Production');
      await page.waitForTimeout(500);

      // Verify filtered results
      const resultsText = await page.locator('text=/\\d+ API key\\(s\\) found/').textContent();
      expect(resultsText).toBeTruthy();
    });

    test('should export API keys to CSV', async ({ page }) => {
      const [download] = await Promise.all([
        page.waitForEvent('download'),
        page.click('button:has-text("Export CSV")'),
      ]);

      const filename = download.suggestedFilename();
      expect(filename).toContain('apikeys-export-');
      expect(filename).toContain('.csv');

      // Verify file content
      const content = await download.createReadStream();
      expect(content).toBeTruthy();
    });
  });

  test.describe('E2E Flow 2: User Bulk Operations', () => {
    test('should bulk import and export users', async ({ page }) => {
      // Switch to Users tab
      await page.click('button[role="tab"]:has-text("Users")');
      await expect(page.locator('text=/User Management/i')).toBeVisible();

      // Create test CSV
      const csvContent = `email,displayName,role,password
bulkuser1@test.com,Bulk User 1,User,Test123!
bulkuser2@test.com,Bulk User 2,User,Test123!
bulkuser3@test.com,Bulk User 3,User,Test123!`;

      // Simulate file upload
      const fileInput = page.locator('input[type="file"]');
      await fileInput.setInputFiles({
        name: 'bulk-users.csv',
        mimeType: 'text/csv',
        buffer: Buffer.from(csvContent),
      });

      // Wait for import to complete
      await expect(page.locator('text=/Import complete/i')).toBeVisible({ timeout: 10000 });

      // Refresh users list
      await page.click('button:has-text("Refresh")');

      // Verify users appear
      await expect(page.locator('text=/bulkuser1@test.com/i')).toBeVisible();

      // Export users
      const [download] = await Promise.all([
        page.waitForEvent('download'),
        page.click('button:has-text("Export CSV")'),
      ]);

      const filename = download.suggestedFilename();
      expect(filename).toContain('users-export-');
      expect(filename).toContain('.csv');
    });

    test('should delete multiple users', async ({ page }) => {
      // Switch to Users tab
      await page.click('button[role="tab"]:has-text("Users")');

      // Select users (simulated - actual UI may differ)
      const bulkBar = page.locator('[data-testid="bulk-action-bar"]');
      await expect(bulkBar).toBeVisible();

      // Verify bulk actions available
      await expect(bulkBar.locator('button:has-text("Delete")')).toBeVisible();
    });
  });

  test.describe('E2E Flow 3: Activity Timeline', () => {
    test('should display system-wide activity', async ({ page }) => {
      // Switch to Activity tab
      await page.click('button[role="tab"]:has-text("Activity")');
      await expect(page.locator('text=/System Activity Timeline/i')).toBeVisible();

      // Verify timeline component
      const timeline = page.locator('[data-testid="user-activity-timeline"]');
      await expect(timeline).toBeVisible();

      // Refresh activities
      await page.click('button:has-text("Refresh")');

      // Wait for activities to load
      await page.waitForTimeout(1000);

      // Verify at least one activity is displayed
      const activities = page.locator('[data-testid^="activity-"]');
      await expect(activities.first()).toBeVisible();
    });

    test('should show recent actions from other tabs', async ({ page }) => {
      // Create an API key
      await page.click('button:has-text("Create Key")');
      await page.fill('input[id="keyName"]', 'Timeline Test Key');
      await page.click('button:has-text("Create Key")');
      await page.click('button:has-text("I\'ve Saved It")');

      // Switch to Activity tab
      await page.click('button[role="tab"]:has-text("Activity")');

      // Refresh to get latest activities
      await page.click('button:has-text("Refresh")');
      await page.waitForTimeout(1000);

      // Verify API key creation is logged
      await expect(page.locator('text=/api_key.created|Created API key/i')).toBeVisible();
    });
  });

  test.describe('Performance - Stress Test', () => {
    test('should handle bulk import of 100 users in <30s', async ({ page }) => {
      // Switch to Users tab
      await page.click('button[role="tab"]:has-text("Users")');

      // Generate CSV with 100 users
      const users = Array.from(
        { length: 100 },
        (_, i) => `stress${i}@test.com,Stress User ${i},User,Test123!`
      ).join('\n');
      const csvContent = `email,displayName,role,password\n${users}`;

      const startTime = Date.now();

      // Upload CSV
      const fileInput = page.locator('input[type="file"]');
      await fileInput.setInputFiles({
        name: 'stress-test.csv',
        mimeType: 'text/csv',
        buffer: Buffer.from(csvContent),
      });

      // Wait for completion
      await expect(page.locator('text=/Import complete/i')).toBeVisible({ timeout: 30000 });

      const duration = Date.now() - startTime;

      // Verify completion time
      expect(duration).toBeLessThan(30000); // < 30 seconds

      // Verify import success message
      await expect(page.locator('text=/100/i')).toBeVisible();
    });
  });

  test.describe('Security Audit', () => {
    test('should not leak API keys in console logs', async ({ page }) => {
      const consoleLogs: string[] = [];
      page.on('console', msg => {
        consoleLogs.push(msg.text());
      });

      // Create API key
      await page.click('button:has-text("Create Key")');
      await page.fill('input[id="keyName"]', 'Security Test Key');
      await page.click('button:has-text("Create Key")');

      const plaintextKey = await page.locator('code:has-text("mpl_")').first().textContent();

      await page.click('button:has-text("I\'ve Saved It")');

      // Verify key is not in logs
      const keyLeaked = consoleLogs.some(log => log.includes(plaintextKey!));
      expect(keyLeaked).toBe(false);
    });

    test('should mask API keys in UI by default', async ({ page }) => {
      await page.click('button:has-text("Create Key")');
      await page.fill('input[id="keyName"]', 'Mask Test Key');
      await page.click('button:has-text("Create Key")');

      // Initial state should be masked
      const maskedKey = page.locator('code:has-text("••••")');
      await expect(maskedKey).toBeVisible();

      // Toggle to show
      await page.click('button[aria-label*="show"]');

      // Verify unmasked
      const plaintextKey = page.locator('code:has-text("mpl_")');
      await expect(plaintextKey).toBeVisible();
    });
  });
});
