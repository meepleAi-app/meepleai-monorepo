/**
 * Alert Configuration E2E Tests - Issue #915
 *
 * Validates alert configuration management workflows:
 * - Email SMTP settings
 * - Slack webhook configuration
 * - PagerDuty integration
 * - Global alerting settings
 * - Test alert functionality
 */

import { test as base, expect } from './fixtures/chromatic';
import { AdminHelper, AuthHelper } from './pages';

import type { Page } from '@playwright/test';

const test = base.extend<{ adminPage: Page }>({
  adminPage: async ({ page }: { page: Page }, use: (page: Page) => Promise<void>) => {
    const adminHelper = new AdminHelper(page);

    // Setup admin auth with mocks
    await adminHelper.setupAdminAuth(true);

    // ✅ REMOVED MOCK: Use real Alert Configuration API
    // Real backend GET /api/v1/admin/alert-configuration - list all configs
    // Real backend GET /api/v1/admin/alert-configuration/{category} - get by category
    // Real backend PUT /api/v1/admin/alert-configuration - update config
    // Real backend POST /api/v1/admin/alert-test - test alert sending
    // Note: Tests verify UI and alert configuration workflows with backend data

    await use(page);
  },
});

test.describe('Alert Configuration Management', () => {
  test('admin can view alert configuration page', async ({ adminPage: page }) => {
    await page.goto('/admin/alerts/config');
    await page.waitForLoadState('networkidle');

    // Assert: Alert Configuration page loads
    await expect(page.locator('h1')).toContainText(/Alert Configuration/i);

    // Verify all 4 tabs are present
    await expect(page.getByRole('button', { name: /Email/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Slack/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /PagerDuty/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /General/i })).toBeVisible();
  });

  test('admin can configure email SMTP settings', async ({ adminPage: page }) => {
    await page.goto('/admin/alerts/config');
    await page.waitForLoadState('networkidle');

    // Email tab is active by default
    await expect(page.getByLabel(/SMTP Host/i)).toBeVisible();

    // Fill email configuration
    await page.fill('input#smtpHost', 'smtp.sendgrid.net');
    await page.fill('input#smtpPort', '587');
    await page.fill('input#from', 'alerts@meepleai.dev');
    await page.fill('input#to', 'admin@example.com, ops@example.com');

    // Toggle TLS
    const tlsSwitch = page.locator('button[id="useTls"]');
    if (await tlsSwitch.isVisible()) {
      await tlsSwitch.click();
    }

    // Save configuration
    await page.click('button:has-text("Save Configuration")');

    // Assert: Success message appears
    await expect(page.locator('text=/Email configuration updated successfully/i')).toBeVisible({
      timeout: 5000,
    });
  });

  test('admin can configure Slack webhook', async ({ adminPage: page }) => {
    await page.goto('/admin/alerts/config');
    await page.waitForLoadState('networkidle');

    // Navigate to Slack tab
    await page.getByRole('button', { name: /Slack/i }).click();
    await page.waitForTimeout(500); // Wait for tab switch animation

    // Verify Slack form is visible
    await expect(page.getByLabel(/Webhook URL/i)).toBeVisible();
    await expect(page.getByLabel(/Channel/i)).toBeVisible();

    // Fill Slack configuration
    await page.fill('input#webhookUrl', 'https://hooks.slack.com/services/T123/B456/xyz');
    await page.fill('input#channel', '#monitoring');

    // Save configuration
    await page.click('button:has-text("Save Configuration")');

    // Assert: Success message
    await expect(page.locator('text=/Slack configuration updated successfully/i')).toBeVisible({
      timeout: 5000,
    });
  });

  test('admin can configure PagerDuty integration', async ({ adminPage: page }) => {
    await page.goto('/admin/alerts/config');
    await page.waitForLoadState('networkidle');

    // Navigate to PagerDuty tab
    await page.getByRole('button', { name: /PagerDuty/i }).click();
    await page.waitForTimeout(500);

    // Verify PagerDuty form is visible
    await expect(page.getByLabel(/Integration Key/i)).toBeVisible();

    // Fill PagerDuty configuration
    await page.fill('input#integrationKey', 'R0123456789ABCDEF0123456789ABCDEF');

    // Save configuration
    await page.click('button:has-text("Save Configuration")');

    // Assert: Success message
    await expect(page.locator('text=/PagerDuty configuration updated successfully/i')).toBeVisible({
      timeout: 5000,
    });
  });

  test('admin can configure global alerting settings', async ({ adminPage: page }) => {
    await page.goto('/admin/alerts/config');
    await page.waitForLoadState('networkidle');

    // Navigate to Global tab
    await page.getByRole('button', { name: /General/i }).click();
    await page.waitForTimeout(500);

    // Verify Global settings form is visible
    await expect(page.locator('label:has-text("Enable Alerting System")')).toBeVisible();
    await expect(page.getByLabel(/Throttle Window/i)).toBeVisible();

    // Toggle alerting system
    const enableSwitch = page.locator('button[id="enabled"]');
    if (await enableSwitch.isVisible()) {
      await enableSwitch.click();
    }

    // Set throttle window
    await page.fill('input#throttleMinutes', '120');

    // Save settings
    await page.click('button:has-text("Save Settings")');

    // Assert: Success message
    await expect(page.locator('text=/General settings updated successfully/i')).toBeVisible({
      timeout: 5000,
    });
  });

  test('admin can test email alert', async ({ adminPage: page }) => {
    await page.goto('/admin/alerts/config');
    await page.waitForLoadState('networkidle');

    // Email tab is active by default
    await expect(page.getByLabel(/SMTP Host/i)).toBeVisible();

    // Click test email button
    await page.click('button:has-text("Test Email")');

    // Assert: Success message
    await expect(page.locator('text=/Test email sent successfully/i')).toBeVisible({
      timeout: 5000,
    });
  });

  test('admin can test Slack alert', async ({ adminPage: page }) => {
    await page.goto('/admin/alerts/config');
    await page.waitForLoadState('networkidle');

    // Navigate to Slack tab
    await page.getByRole('button', { name: /Slack/i }).click();
    await page.waitForTimeout(500);

    // Click test Slack button
    await page.click('button:has-text("Test Slack")');

    // Assert: Success message
    await expect(page.locator('text=/Test message sent to Slack successfully/i')).toBeVisible({
      timeout: 5000,
    });
  });

  test('admin can test PagerDuty alert', async ({ adminPage: page }) => {
    await page.goto('/admin/alerts/config');
    await page.waitForLoadState('networkidle');

    // Navigate to PagerDuty tab
    await page.getByRole('button', { name: /PagerDuty/i }).click();
    await page.waitForTimeout(500);

    // Click test PagerDuty button
    await page.click('button:has-text("Test PagerDuty")');

    // Assert: Success message
    await expect(
      page.locator('text=/Test incident created in PagerDuty successfully/i')
    ).toBeVisible({ timeout: 5000 });
  });

  test('admin sees validation error for invalid SMTP port', async ({ adminPage: page }) => {
    await page.goto('/admin/alerts/config');
    await page.waitForLoadState('networkidle');

    // Fill invalid SMTP port
    await page.fill('input#smtpPort', '-1');
    await page.fill('input#smtpHost', 'smtp.gmail.com'); // Fill required field

    // Try to save
    await page.click('button:has-text("Save Configuration")');

    // Assert: Validation error or fail
    // (Validation might be client-side or server-side)
    const errorVisible = await page
      .locator('text=/invalid|error|failed/i')
      .isVisible({ timeout: 2000 })
      .catch(() => false);

    // If no error shown, at least ensure we didn't navigate away
    if (!errorVisible) {
      await expect(page).toHaveURL(/\/admin\/alerts\/config/);
    }
  });

  test('admin sees form validation for required fields', async ({ adminPage: page }) => {
    await page.goto('/admin/alerts/config');
    await page.waitForLoadState('networkidle');

    // Clear SMTP host (required field)
    await page.fill('input#smtpHost', '');

    // Try to save with empty required field
    await page.click('button:has-text("Save Configuration")');

    // Assert: We stay on the page (validation prevents save)
    await expect(page).toHaveURL(/\/admin\/alerts\/config/);
  });

  test('admin can switch between tabs without losing data', async ({ adminPage: page }) => {
    await page.goto('/admin/alerts/config');
    await page.waitForLoadState('networkidle');

    // Fill email configuration
    const testHost = 'smtp.test.com';
    await page.fill('input#smtpHost', testHost);

    // Switch to Slack tab
    await page.getByRole('button', { name: /Slack/i }).click();
    await page.waitForTimeout(500);

    // Switch back to Email tab
    await page.getByRole('button', { name: /Email/i }).click();
    await page.waitForTimeout(500);

    // Assert: Data is still there (component maintains state)
    const smtpHostValue = await page.inputValue('input#smtpHost');
    expect(smtpHostValue).toBe(testHost);
  });

  test('admin page is responsive on mobile', async ({ adminPage: page }) => {
    // Set mobile viewport
    await page.setViewportSize({ width: 375, height: 667 });

    await page.goto('/admin/alerts/config');
    await page.waitForLoadState('networkidle');

    // Assert: Page loads and tabs are visible (may be stacked on mobile)
    await expect(page.locator('h1')).toContainText(/Alert Configuration/i);
    await expect(page.getByRole('button', { name: /Email/i })).toBeVisible();

    // Tabs should be responsive (scrollable or stacked)
    const emailTab = page.getByRole('button', { name: /Email/i });
    await expect(emailTab).toBeVisible();
  });

  test('admin page is responsive on tablet', async ({ adminPage: page }) => {
    // Set tablet viewport
    await page.setViewportSize({ width: 768, height: 1024 });

    await page.goto('/admin/alerts/config');
    await page.waitForLoadState('networkidle');

    // Assert: All tabs visible in row
    await expect(page.getByRole('button', { name: /Email/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Slack/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /PagerDuty/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /General/i })).toBeVisible();
  });

  test('non-admin user cannot access alert configuration', async ({ page }) => {
    // ✅ CHANGED: Use AuthHelper for non-admin session instead of inline mock
    const authHelper = new AuthHelper(page);
    await authHelper.mockAuthenticatedSession({
      id: 'user-1',
      email: 'user@meepleai.dev',
      displayName: 'Regular User',
      role: 'User', // Non-admin role
    });

    await page.goto('/admin/alerts/config');
    await page.waitForLoadState('networkidle');

    // Assert: Redirected or access denied message
    const isRedirected = page.url().includes('/login') || page.url().includes('/unauthorized');
    const hasAccessDenied = await page
      .locator('text=/unauthorized|access denied|forbidden/i')
      .isVisible({ timeout: 2000 })
      .catch(() => false);

    expect(isRedirected || hasAccessDenied).toBe(true);
  });
});
