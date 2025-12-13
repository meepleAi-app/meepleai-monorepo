/**
 * E2E Tests for Admin Reports - Issue #922
 *
 * Comprehensive end-to-end testing for:
 * - Report generation (all templates and formats)
 * - Report scheduling with cron expressions
 * - Email delivery validation
 * - Report execution history
 * - Error handling and edge cases
 */

import { test, expect, type Page } from '@playwright/test';

// Test data
const TEST_USER_ADMIN = {
  email: 'admin@test.com',
  password: 'Admin123!',
  role: 'Admin',
};

const REPORT_TEMPLATES = [
  { value: 'SystemHealth', label: 'System Health' },
  { value: 'UserActivity', label: 'User Activity' },
  { value: 'AIUsage', label: 'AI/LLM Usage' },
  { value: 'ContentMetrics', label: 'Content Metrics' },
];

const REPORT_FORMATS = ['CSV', 'JSON', 'PDF'];

test.describe('Admin Reports - Report Generation', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('/admin/reports');
    await expect(page.locator('h1')).toContainText('Reports');
  });

  test('should display report generation form', async ({ page }) => {
    await expect(page.getByRole('tab', { name: /generate/i })).toBeVisible();
    await expect(page.getByLabel(/report template/i)).toBeVisible();
    await expect(page.getByLabel(/format/i)).toBeVisible();
    await expect(page.getByLabel(/start date/i)).toBeVisible();
    await expect(page.getByLabel(/end date/i)).toBeVisible();
  });

  test('should generate SystemHealth report in CSV format', async ({ page }) => {
    await page.getByLabel(/report template/i).click();
    await page.getByRole('option', { name: 'System Health' }).click();

    await page.getByLabel(/format/i).click();
    await page.getByRole('option', { name: 'CSV' }).click();

    await page.getByLabel(/start date/i).fill(getDateString(-7));
    await page.getByLabel(/end date/i).fill(getDateString(0));

    const downloadPromise = page.waitForEvent('download');
    await page.getByRole('button', { name: /generate report/i }).click();

    const download = await downloadPromise;
    expect(download.suggestedFilename()).toMatch(/SystemHealth_.*\.csv/);

    await expect(page.getByText(/report generated successfully/i)).toBeVisible();
  });

  test('should generate UserActivity report in JSON format', async ({ page }) => {
    await page.getByLabel(/report template/i).click();
    await page.getByRole('option', { name: 'User Activity' }).click();

    await page.getByLabel(/format/i).click();
    await page.getByRole('option', { name: 'JSON' }).click();

    await page.getByLabel(/start date/i).fill(getDateString(-30));
    await page.getByLabel(/end date/i).fill(getDateString(0));

    const downloadPromise = page.waitForEvent('download');
    await page.getByRole('button', { name: /generate report/i }).click();

    const download = await downloadPromise;
    expect(download.suggestedFilename()).toMatch(/UserActivity_.*\.json/);
  });

  test('should generate AIUsage report in PDF format', async ({ page }) => {
    await page.getByLabel(/report template/i).click();
    await page.getByRole('option', { name: /AI\/LLM Usage/i }).click();

    await page.getByLabel(/format/i).click();
    await page.getByRole('option', { name: 'PDF' }).click();

    await page.getByLabel(/start date/i).fill(getDateString(-14));
    await page.getByLabel(/end date/i).fill(getDateString(0));

    const downloadPromise = page.waitForEvent('download');
    await page.getByRole('button', { name: /generate report/i }).click();

    const download = await downloadPromise;
    expect(download.suggestedFilename()).toMatch(/AIUsage_.*\.pdf/);
  });

  test('should validate date range - end date before start date', async ({ page }) => {
    await page.getByLabel(/report template/i).click();
    await page.getByRole('option', { name: 'System Health' }).click();

    await page.getByLabel(/start date/i).fill(getDateString(0));
    await page.getByLabel(/end date/i).fill(getDateString(-7));

    await page.getByRole('button', { name: /generate report/i }).click();

    await expect(page.getByText(/end date must be after start date/i)).toBeVisible();
  });

  test('should validate date range - missing dates', async ({ page }) => {
    await page.getByLabel(/report template/i).click();
    await page.getByRole('option', { name: 'Content Metrics' }).click();

    await page.getByRole('button', { name: /generate report/i }).click();

    await expect(page.getByText(/start date.*required/i)).toBeVisible();
  });

  test('should handle report generation errors gracefully', async ({ page }) => {
    await page.route('**/api/v1/admin/reports/generate', route => {
      route.fulfill({
        status: 500,
        body: JSON.stringify({ message: 'Database connection error' }),
      });
    });

    await page.getByLabel(/report template/i).click();
    await page.getByRole('option', { name: 'System Health' }).click();

    await page.getByLabel(/start date/i).fill(getDateString(-7));
    await page.getByLabel(/end date/i).fill(getDateString(0));

    await page.getByRole('button', { name: /generate report/i }).click();

    await expect(page.getByText(/failed to generate report/i)).toBeVisible();
  });
});

test.describe('Admin Reports - Report Scheduling', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('/admin/reports');
    await page.getByRole('tab', { name: /scheduled/i }).click();
  });

  test('should display schedule report form', async ({ page }) => {
    await page.getByRole('button', { name: /schedule new report/i }).click();

    await expect(page.getByLabel(/report name/i)).toBeVisible();
    await expect(page.getByLabel(/description/i)).toBeVisible();
    await expect(page.getByLabel(/schedule/i)).toBeVisible();
    await expect(page.getByLabel(/email recipients/i)).toBeVisible();
  });

  test('should schedule daily report with email delivery', async ({ page }) => {
    await page.getByRole('button', { name: /schedule new report/i }).click();

    await page.getByLabel(/report name/i).fill('Daily System Health Report');
    await page.getByLabel(/description/i).fill('Automated daily health check');

    await page.getByLabel(/report template/i).click();
    await page.getByRole('option', { name: 'System Health' }).click();

    await page.getByLabel(/format/i).click();
    await page.getByRole('option', { name: 'PDF' }).click();

    await page.getByLabel(/schedule/i).click();
    await page.getByRole('option', { name: /daily \(9 am\)/i }).click();

    await page.getByLabel(/email recipients/i).fill('admin@test.com, ops@test.com');

    await page.getByRole('button', { name: /schedule report/i }).click();

    await expect(page.getByText(/report scheduled successfully/i)).toBeVisible();
    await expect(page.getByText('Daily System Health Report')).toBeVisible();
  });

  test('should validate email recipients format', async ({ page }) => {
    await page.getByRole('button', { name: /schedule new report/i }).click();

    await page.getByLabel(/report name/i).fill('Test Report');
    await page.getByLabel(/email recipients/i).fill('invalid-email, not@email');

    await page.getByRole('button', { name: /schedule report/i }).click();

    await expect(page.getByText(/invalid email address/i)).toBeVisible();
  });

  test('should schedule weekly report with custom cron', async ({ page }) => {
    await page.getByRole('button', { name: /schedule new report/i }).click();

    await page.getByLabel(/report name/i).fill('Weekly Activity Summary');
    await page.getByLabel(/description/i).fill('Every Monday at 8 AM');

    await page.getByLabel(/report template/i).click();
    await page.getByRole('option', { name: 'User Activity' }).click();

    await page.getByLabel(/schedule/i).click();
    await page.getByRole('option', { name: /weekly \(monday\)/i }).click();

    await page.getByLabel(/email recipients/i).fill('admin@test.com');

    await page.getByRole('button', { name: /schedule report/i }).click();

    await expect(page.getByText(/report scheduled successfully/i)).toBeVisible();
  });

  test('should display scheduled reports list', async ({ page }) => {
    await expect(page.getByRole('table')).toBeVisible();
    await expect(page.getByRole('columnheader', { name: /name/i })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: /template/i })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: /schedule/i })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: /recipients/i })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: /status/i })).toBeVisible();
  });

  test('should pause and resume scheduled report', async ({ page }) => {
    const reportRow = page.getByRole('row', { name: /daily system health/i }).first();

    await reportRow.getByRole('button', { name: /pause/i }).click();
    await expect(page.getByText(/report paused/i)).toBeVisible();
    await expect(reportRow.getByText(/inactive/i)).toBeVisible();

    await reportRow.getByRole('button', { name: /resume/i }).click();
    await expect(page.getByText(/report resumed/i)).toBeVisible();
    await expect(reportRow.getByText(/active/i)).toBeVisible();
  });

  test('should delete scheduled report', async ({ page }) => {
    const reportRow = page.getByRole('row', { name: /daily system health/i }).first();

    await reportRow.getByRole('button', { name: /delete/i }).click();
    await page.getByRole('button', { name: /confirm/i }).click();

    await expect(page.getByText(/report deleted successfully/i)).toBeVisible();
    await expect(page.getByText('Daily System Health Report')).not.toBeVisible();
  });
});

test.describe('Admin Reports - Execution History', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('/admin/reports');
    await page.getByRole('tab', { name: /history/i }).click();
  });

  test('should display execution history table', async ({ page }) => {
    await expect(page.getByRole('table')).toBeVisible();
    await expect(page.getByRole('columnheader', { name: /execution/i })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: /report/i })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: /status/i })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: /duration/i })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: /size/i })).toBeVisible();
  });

  test('should filter executions by report', async ({ page }) => {
    await page.getByLabel(/filter by report/i).click();
    await page.getByRole('option', { name: /daily system health/i }).click();

    const rows = page.getByRole('row');
    await expect(rows).not.toHaveCount(0);

    const firstRow = rows.nth(1);
    await expect(firstRow.getByText(/daily system health/i)).toBeVisible();
  });

  test('should display execution status badges', async ({ page }) => {
    await expect(page.getByText(/completed/i).first()).toBeVisible();

    const completedBadge = page.locator('[data-status="completed"]').first();
    await expect(completedBadge).toHaveClass(/success|green/);
  });

  test('should show failed executions with error details', async ({ page }) => {
    const failedRow = page.getByRole('row', { name: /failed/i }).first();
    await failedRow.getByRole('button', { name: /details/i }).click();

    await expect(page.getByText(/error message/i)).toBeVisible();
    await expect(page.getByText(/stack trace/i)).toBeVisible();
  });

  test('should download report from execution history', async ({ page }) => {
    const successRow = page.getByRole('row', { name: /completed/i }).first();

    const downloadPromise = page.waitForEvent('download');
    await successRow.getByRole('button', { name: /download/i }).click();

    const download = await downloadPromise;
    expect(download.suggestedFilename()).toMatch(/\.(csv|json|pdf)$/);
  });
});

test.describe('Admin Reports - Email Validation', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('/admin/reports');
  });

  test('should send test email successfully', async ({ page }) => {
    await page.getByRole('tab', { name: /scheduled/i }).click();
    await page.getByRole('button', { name: /schedule new report/i }).click();

    await page.getByLabel(/email recipients/i).fill('test@example.com');
    await page.getByRole('button', { name: /send test email/i }).click();

    await expect(page.getByText(/test email sent/i)).toBeVisible();
  });

  test('should validate multiple email recipients', async ({ page }) => {
    await page.getByRole('tab', { name: /scheduled/i }).click();
    await page.getByRole('button', { name: /schedule new report/i }).click();

    const validEmails = ['admin@test.com', 'user1@example.org', 'ops-team@company.io'];

    await page.getByLabel(/email recipients/i).fill(validEmails.join(', '));

    const recipientBadges = page.locator('[data-testid="email-badge"]');
    await expect(recipientBadges).toHaveCount(validEmails.length);
  });

  test('should reject invalid email formats', async ({ page }) => {
    await page.getByRole('tab', { name: /scheduled/i }).click();
    await page.getByRole('button', { name: /schedule new report/i }).click();

    const invalidEmails = ['notanemail', '@example.com', 'user@', 'user @example.com'];

    for (const invalidEmail of invalidEmails) {
      await page.getByLabel(/email recipients/i).clear();
      await page.getByLabel(/email recipients/i).fill(invalidEmail);
      await page.getByLabel(/email recipients/i).blur();

      await expect(page.getByText(/invalid email/i)).toBeVisible();
    }
  });

  test('should handle email delivery failures gracefully', async ({ page }) => {
    await page.route('**/api/v1/admin/reports/schedule', route => {
      route.fulfill({
        status: 200,
        body: JSON.stringify({ reportId: 'test-id', message: 'Scheduled' }),
      });
    });

    await page.route('**/api/v1/admin/reports/executions', route => {
      route.fulfill({
        status: 200,
        body: JSON.stringify([
          {
            id: '1',
            reportId: 'test-id',
            status: 'CompletedWithEmailFailure',
            errorMessage: 'SMTP connection timeout',
            startedAt: new Date().toISOString(),
            completedAt: new Date().toISOString(),
          },
        ]),
      });
    });

    await page.getByRole('tab', { name: /history/i }).click();

    await expect(page.getByText(/email.*failed/i)).toBeVisible();
    await expect(page.getByText(/smtp.*timeout/i)).toBeVisible();
  });

  test('should display email sent confirmation in execution details', async ({ page }) => {
    await page.getByRole('tab', { name: /history/i }).click();

    const completedRow = page.getByRole('row', { name: /completed/i }).first();
    await completedRow.getByRole('button', { name: /details/i }).click();

    await expect(page.getByText(/email sent to/i)).toBeVisible();
    await expect(page.getByText(/admin@test\.com/)).toBeVisible();
  });
});

test.describe('Admin Reports - Accessibility', () => {
  test('should be keyboard navigable', async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('/admin/reports');

    await page.keyboard.press('Tab');
    await expect(page.getByRole('tab', { name: /generate/i })).toBeFocused();

    await page.keyboard.press('Tab');
    await expect(page.getByLabel(/report template/i)).toBeFocused();
  });

  test('should have proper ARIA labels', async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('/admin/reports');

    await expect(page.getByRole('region', { name: /report generation/i })).toBeVisible();
    await expect(page.getByRole('tablist')).toHaveAttribute('aria-label', /report tabs/i);
  });
});

// Helper functions

async function loginAsAdmin(page: Page) {
  await page.goto('/login');
  await page.getByLabel(/email/i).fill(TEST_USER_ADMIN.email);
  await page.getByLabel(/password/i).fill(TEST_USER_ADMIN.password);
  await page.getByRole('button', { name: /sign in/i }).click();
  await page.waitForURL('/admin/**');
}

function getDateString(daysOffset: number): string {
  const date = new Date();
  date.setDate(date.getDate() + daysOffset);
  return date.toISOString().split('T')[0];
}
