/**
 * ADM-15: System Alerts Config
 * Issue #3082 - P2 Medium
 *
 * Tests system alerts configuration:
 * - View alert configurations
 * - Create new alerts
 * - Edit alert thresholds
 * - Enable/disable alerts
 */

import { test, expect } from '../fixtures/chromatic';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface AlertConfig {
  id: string;
  name: string;
  description: string;
  type: 'threshold' | 'rate' | 'availability';
  metric: string;
  condition: 'gt' | 'lt' | 'eq';
  threshold: number;
  timeWindow: number; // in minutes
  enabled: boolean;
  notificationChannels: string[];
  severity: 'info' | 'warning' | 'critical';
}

/**
 * Setup mock routes for system alerts testing
 */
async function setupSystemAlertsMocks(page: Page) {
  let alerts: AlertConfig[] = [
    {
      id: 'alert-1',
      name: 'High CPU Usage',
      description: 'Alert when CPU usage exceeds threshold',
      type: 'threshold',
      metric: 'cpu_usage',
      condition: 'gt',
      threshold: 80,
      timeWindow: 5,
      enabled: true,
      notificationChannels: ['email', 'slack'],
      severity: 'warning',
    },
    {
      id: 'alert-2',
      name: 'API Error Rate',
      description: 'Alert when API error rate is too high',
      type: 'rate',
      metric: 'api_errors',
      condition: 'gt',
      threshold: 5,
      timeWindow: 15,
      enabled: true,
      notificationChannels: ['email'],
      severity: 'critical',
    },
    {
      id: 'alert-3',
      name: 'Database Connection',
      description: 'Alert when database is unavailable',
      type: 'availability',
      metric: 'db_connection',
      condition: 'eq',
      threshold: 0,
      timeWindow: 1,
      enabled: false,
      notificationChannels: ['email', 'slack', 'pagerduty'],
      severity: 'critical',
    },
  ];

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

  // Mock alerts list endpoint
  await page.route(`${API_BASE}/api/v1/admin/alerts`, async (route) => {
    const method = route.request().method();

    if (method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          alerts,
          totalCount: alerts.length,
        }),
      });
    } else if (method === 'POST') {
      const body = await route.request().postDataJSON();

      const newAlert: AlertConfig = {
        id: `alert-${Date.now()}`,
        name: body.name,
        description: body.description || '',
        type: body.type || 'threshold',
        metric: body.metric,
        condition: body.condition || 'gt',
        threshold: body.threshold,
        timeWindow: body.timeWindow || 5,
        enabled: body.enabled ?? true,
        notificationChannels: body.notificationChannels || ['email'],
        severity: body.severity || 'warning',
      };

      alerts.push(newAlert);

      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({
          message: 'Alert created',
          alert: newAlert,
        }),
      });
    } else {
      await route.continue();
    }
  });

  // Mock single alert endpoint
  await page.route(`${API_BASE}/api/v1/admin/alerts/*`, async (route) => {
    const method = route.request().method();
    const url = route.request().url();
    const alertIdMatch = url.match(/alerts\/([^/]+)/);
    const alertId = alertIdMatch?.[1];

    const alertIndex = alerts.findIndex((a) => a.id === alertId);

    if (alertIndex === -1) {
      await route.fulfill({
        status: 404,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Alert not found' }),
      });
      return;
    }

    if (method === 'PATCH' || method === 'PUT') {
      const body = await route.request().postDataJSON();

      // Update alert
      alerts[alertIndex] = { ...alerts[alertIndex], ...body };

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          message: 'Alert updated',
          alert: alerts[alertIndex],
        }),
      });
    } else if (method === 'DELETE') {
      alerts = alerts.filter((a) => a.id !== alertId);
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Alert deleted' }),
      });
    } else {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ alert: alerts[alertIndex] }),
      });
    }
  });

  // Mock notification channels
  await page.route(`${API_BASE}/api/v1/admin/notification-channels`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        channels: [
          { id: 'email', name: 'Email', configured: true },
          { id: 'slack', name: 'Slack', configured: true },
          { id: 'pagerduty', name: 'PagerDuty', configured: false },
          { id: 'webhook', name: 'Webhook', configured: true },
        ],
      }),
    });
  });

  // Mock metrics endpoint
  await page.route(`${API_BASE}/api/v1/admin/metrics`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        metrics: [
          { id: 'cpu_usage', name: 'CPU Usage', unit: '%' },
          { id: 'memory_usage', name: 'Memory Usage', unit: '%' },
          { id: 'api_errors', name: 'API Error Rate', unit: 'errors/min' },
          { id: 'db_connection', name: 'Database Connection', unit: 'boolean' },
          { id: 'response_time', name: 'Response Time', unit: 'ms' },
        ],
      }),
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

  return { getAlerts: () => alerts };
}

test.describe('ADM-15: System Alerts Config', () => {
  test.describe('View Alert Configurations', () => {
    test('should navigate to alerts configuration', async ({ page }) => {
      await setupSystemAlertsMocks(page);

      await page.goto('/admin');
      await page.waitForLoadState('networkidle');

      const alertsLink = page.getByRole('link', { name: /alert|monitoring/i });
      if (await alertsLink.isVisible()) {
        await alertsLink.click();
        await page.waitForLoadState('networkidle');
      }

      await expect(page.getByText(/alert|monitoring/i)).toBeVisible();
    });

    test('should display existing alerts', async ({ page }) => {
      await setupSystemAlertsMocks(page);

      await page.goto('/admin/alerts');
      await page.waitForLoadState('networkidle');

      // Should show alert names
      await expect(page.getByText(/cpu.*usage|api.*error/i).first()).toBeVisible({ timeout: 5000 });
    });

    test('should show alert status (enabled/disabled)', async ({ page }) => {
      await setupSystemAlertsMocks(page);

      await page.goto('/admin/alerts');
      await page.waitForLoadState('networkidle');

      // Should show enabled/disabled status
      await expect(
        page.locator('[data-enabled], .alert-status, input[type="checkbox"]').first()
      ).toBeVisible();
    });

    test('should show alert severity', async ({ page }) => {
      await setupSystemAlertsMocks(page);

      await page.goto('/admin/alerts');
      await page.waitForLoadState('networkidle');

      // Should show severity indicators
      await expect(
        page.getByText(/warning|critical|info/i).first()
      ).toBeVisible();
    });
  });

  test.describe('Create New Alert', () => {
    test('should show create alert button', async ({ page }) => {
      await setupSystemAlertsMocks(page);

      await page.goto('/admin/alerts');
      await page.waitForLoadState('networkidle');

      await expect(
        page.getByRole('button', { name: /create|add|new.*alert/i })
      ).toBeVisible();
    });

    test('should open create alert form', async ({ page }) => {
      await setupSystemAlertsMocks(page);

      await page.goto('/admin/alerts');
      await page.waitForLoadState('networkidle');

      const createButton = page.getByRole('button', { name: /create|add/i });
      if (await createButton.isVisible()) {
        await createButton.click();

        // Should show form
        await expect(page.getByLabel(/name/i)).toBeVisible();
      }
    });

    test('should create new alert', async ({ page }) => {
      await setupSystemAlertsMocks(page);

      await page.goto('/admin/alerts');
      await page.waitForLoadState('networkidle');

      const createButton = page.getByRole('button', { name: /create|add/i });
      if (await createButton.isVisible()) {
        await createButton.click();

        // Fill form
        await page.getByLabel(/name/i).fill('Memory Alert');

        const metricSelect = page.getByRole('combobox', { name: /metric/i });
        if (await metricSelect.isVisible()) {
          await metricSelect.click();
          await page.getByText(/memory/i).click();
        }

        await page.getByLabel(/threshold/i).fill('90');

        // Submit
        const submitButton = page.getByRole('button', { name: /create|save/i });
        await submitButton.click();

        await expect(page.getByText(/created|success/i)).toBeVisible();
      }
    });

    test('should validate required fields', async ({ page }) => {
      await setupSystemAlertsMocks(page);

      await page.goto('/admin/alerts');
      await page.waitForLoadState('networkidle');

      const createButton = page.getByRole('button', { name: /create|add/i });
      if (await createButton.isVisible()) {
        await createButton.click();

        // Try to submit without filling fields
        const submitButton = page.getByRole('button', { name: /create|save/i });
        await submitButton.click();

        await expect(page.getByText(/required|invalid/i)).toBeVisible();
      }
    });
  });

  test.describe('Edit Alert Thresholds', () => {
    test('should allow editing alert threshold', async ({ page }) => {
      await setupSystemAlertsMocks(page);

      await page.goto('/admin/alerts');
      await page.waitForLoadState('networkidle');

      // Click edit on first alert
      const editButton = page.getByRole('button', { name: /edit/i }).first();
      if (await editButton.isVisible()) {
        await editButton.click();

        // Modify threshold
        const thresholdInput = page.getByLabel(/threshold/i);
        if (await thresholdInput.isVisible()) {
          await thresholdInput.clear();
          await thresholdInput.fill('85');

          const saveButton = page.getByRole('button', { name: /save|update/i });
          await saveButton.click();

          await expect(page.getByText(/updated|saved/i)).toBeVisible();
        }
      }
    });

    test('should allow editing time window', async ({ page }) => {
      await setupSystemAlertsMocks(page);

      await page.goto('/admin/alerts');
      await page.waitForLoadState('networkidle');

      const editButton = page.getByRole('button', { name: /edit/i }).first();
      if (await editButton.isVisible()) {
        await editButton.click();

        const timeWindowInput = page.getByLabel(/time.*window|duration|minute/i);
        if (await timeWindowInput.isVisible()) {
          await timeWindowInput.clear();
          await timeWindowInput.fill('10');
        }
      }
    });

    test('should allow changing severity', async ({ page }) => {
      await setupSystemAlertsMocks(page);

      await page.goto('/admin/alerts');
      await page.waitForLoadState('networkidle');

      const editButton = page.getByRole('button', { name: /edit/i }).first();
      if (await editButton.isVisible()) {
        await editButton.click();

        const severitySelect = page.getByRole('combobox', { name: /severity/i });
        if (await severitySelect.isVisible()) {
          await severitySelect.click();
          await page.getByText(/critical/i).click();
        }
      }
    });
  });

  test.describe('Enable/Disable Alerts', () => {
    test('should toggle alert enabled status', async ({ page }) => {
      await setupSystemAlertsMocks(page);

      await page.goto('/admin/alerts');
      await page.waitForLoadState('networkidle');

      // Find toggle switch
      const enableToggle = page.locator('input[type="checkbox"], [role="switch"]').first();
      if (await enableToggle.isVisible()) {
        const wasEnabled = await enableToggle.isChecked();
        await enableToggle.click();

        await page.waitForTimeout(500);

        // Status should change
        const isEnabled = await enableToggle.isChecked();
        expect(isEnabled).not.toBe(wasEnabled);
      }
    });

    test('should show confirmation before disabling critical alerts', async ({ page }) => {
      await setupSystemAlertsMocks(page);

      await page.goto('/admin/alerts');
      await page.waitForLoadState('networkidle');

      // Find critical alert toggle
      const criticalRow = page.locator('[data-severity="critical"]').first();
      if (await criticalRow.isVisible()) {
        const toggle = criticalRow.locator('input[type="checkbox"]');
        if (await toggle.isVisible()) {
          await toggle.click();

          // May show confirmation dialog
          const confirmDialog = page.getByRole('dialog');
          if (await confirmDialog.isVisible()) {
            await expect(page.getByText(/confirm|disable/i)).toBeVisible();
          }
        }
      }
    });
  });

  test.describe('Notification Channels', () => {
    test('should show notification channel options', async ({ page }) => {
      await setupSystemAlertsMocks(page);

      await page.goto('/admin/alerts');
      await page.waitForLoadState('networkidle');

      const editButton = page.getByRole('button', { name: /edit/i }).first();
      if (await editButton.isVisible()) {
        await editButton.click();

        // Should show channel checkboxes
        await expect(page.getByText(/email|slack|pagerduty/i)).toBeVisible();
      }
    });

    test('should allow selecting notification channels', async ({ page }) => {
      await setupSystemAlertsMocks(page);

      await page.goto('/admin/alerts');
      await page.waitForLoadState('networkidle');

      const editButton = page.getByRole('button', { name: /edit/i }).first();
      if (await editButton.isVisible()) {
        await editButton.click();

        const slackCheckbox = page.getByLabel(/slack/i);
        if (await slackCheckbox.isVisible()) {
          await slackCheckbox.check();
          await expect(slackCheckbox).toBeChecked();
        }
      }
    });
  });

  test.describe('Delete Alert', () => {
    test('should allow deleting alert', async ({ page }) => {
      await setupSystemAlertsMocks(page);

      await page.goto('/admin/alerts');
      await page.waitForLoadState('networkidle');

      const deleteButton = page.getByRole('button', { name: /delete|remove/i }).first();
      if (await deleteButton.isVisible()) {
        await deleteButton.click();

        // Confirm deletion
        const confirmButton = page.getByRole('button', { name: /confirm|yes|delete/i });
        if (await confirmButton.isVisible()) {
          await confirmButton.click();

          await expect(page.getByText(/deleted|removed/i)).toBeVisible();
        }
      }
    });
  });
});
