/**
 * E2E Tests for Admin Reports Email Delivery - Issue #922
 *
 * Comprehensive end-to-end testing for:
 * - Complete report generation workflow
 * - Email delivery via Mailpit
 * - HTML rendering verification
 * - Attachment validation
 * - Visual regression testing
 *
 * Dependencies:
 * - Mailpit running on localhost:8025
 * - EmailService configured with Mailpit SMTP (localhost:1025)
 * - Backend API with report generation endpoints
 *
 * @see https://mailpit.axllent.org/docs/api/
 * @see apps/web/e2e/admin-reports.spec.ts (base report tests)
 */

import { test, expect, type Page } from '@playwright/test';
import type { APIRequestContext } from '@playwright/test';

// ============================================================================
// Test Data & Configuration
// ============================================================================

const TEST_USER_ADMIN = {
  email: 'admin@test.com',
  password: 'Admin123!',
  role: 'Admin',
};

const MAILPIT_API_BASE = 'http://localhost:8025/api/v1';
const MAILPIT_WEB_UI = 'http://localhost:8025';

const REPORT_TEMPLATES = [
  { value: 'SystemHealth', label: 'System Health' },
  { value: 'UserActivity', label: 'User Activity' },
  { value: 'AIUsage', label: 'AI/LLM Usage' },
  { value: 'ContentMetrics', label: 'Content Metrics' },
] as const;

// Mailpit API response types
interface MailpitMessage {
  ID: string;
  From: { Address: string; Name: string };
  To: Array<{ Address: string; Name: string }>;
  Subject: string;
  Created: string;
  Size: number;
}

interface MailpitMessageDetail extends MailpitMessage {
  HTML: string;
  Text: string;
  Attachments: Array<{
    FileName: string;
    ContentType: string;
    Size: number;
  }>;
}

interface MailpitMessagesResponse {
  total: number;
  unread: number;
  count: number;
  start: number;
  messages: MailpitMessage[];
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Login as admin user
 */
async function loginAsAdmin(page: Page): Promise<void> {
  await page.goto('/login');
  await page.getByLabel(/email/i).fill(TEST_USER_ADMIN.email);
  await page.getByLabel(/password/i).fill(TEST_USER_ADMIN.password);
  await page.getByRole('button', { name: /sign in/i }).click();
  await page.waitForURL('/admin/**');
}

/**
 * Delete all messages from Mailpit (cleanup)
 */
async function clearMailpitMessages(request: APIRequestContext): Promise<void> {
  await request.delete(`${MAILPIT_API_BASE}/messages`);
}

/**
 * Get all messages from Mailpit
 */
async function getMailpitMessages(request: APIRequestContext): Promise<MailpitMessage[]> {
  const response = await request.get(`${MAILPIT_API_BASE}/messages`);
  expect(response.ok()).toBeTruthy();
  const data: MailpitMessagesResponse = await response.json();
  return data.messages || [];
}

/**
 * Get message detail from Mailpit by ID
 */
async function getMailpitMessageDetail(
  request: APIRequestContext,
  messageId: string
): Promise<MailpitMessageDetail> {
  const response = await request.get(`${MAILPIT_API_BASE}/message/${messageId}`);
  expect(response.ok()).toBeTruthy();
  return await response.json();
}

/**
 * Wait for email to arrive in Mailpit (with retry)
 * Uses environment-aware polling interval for test reliability
 */
async function waitForEmail(
  request: APIRequestContext,
  subjectPattern: string | RegExp,
  timeoutMs: number = 10000
): Promise<MailpitMessage> {
  const startTime = Date.now();
  const pattern =
    typeof subjectPattern === 'string' ? new RegExp(subjectPattern, 'i') : subjectPattern;

  // Environment-aware polling: slower in CI for stability, faster locally for speed
  const pollInterval = process.env.CI === 'true' ? 1000 : 300;

  while (Date.now() - startTime < timeoutMs) {
    const messages = await getMailpitMessages(request);
    const matchingMessage = messages.find(m => pattern.test(m.Subject));

    if (matchingMessage) {
      return matchingMessage;
    }

    // Wait before retry (adaptive based on environment)
    await new Promise(resolve => setTimeout(resolve, pollInterval));
  }

  throw new Error(
    `Email with subject pattern "${subjectPattern}" not received within ${timeoutMs}ms`
  );
}

/**
 * Generate date string for report filters
 */
function getDateString(daysOffset: number): string {
  const date = new Date();
  date.setDate(date.getDate() + daysOffset);
  return date.toISOString().split('T')[0];
}

// ============================================================================
// Test Suite: Complete Report Flow with Email Delivery
// ============================================================================

test.describe('Admin Reports - Email Delivery Flow (Issue #922)', () => {
  test.beforeEach(async ({ page, request }) => {
    // Clear Mailpit before each test
    await clearMailpitMessages(request);

    // Login and navigate to reports
    await loginAsAdmin(page);
    await page.goto('/admin/reports');
    await expect(page.locator('h1')).toContainText('Reports');
  });

  // --------------------------------------------------------------------------
  // SCENARIO 1: Complete Report Flow with Email
  // --------------------------------------------------------------------------

  test('should complete full report generation and email delivery flow', async ({
    page,
    request,
  }) => {
    // STEP 1: Navigate to scheduled reports
    await page.getByRole('tab', { name: /scheduled/i }).click();
    await expect(page.getByRole('button', { name: /schedule new report/i })).toBeVisible();

    // STEP 2: Create scheduled report
    await page.getByRole('button', { name: /schedule new report/i }).click();

    await page.getByLabel(/report name/i).fill('Daily System Health Report');
    await page.getByLabel(/description/i).fill('Automated daily health check for E2E testing');

    // STEP 3: Configure parameters
    await page.getByLabel(/report template/i).click();
    await page.getByRole('option', { name: 'System Health' }).click();

    await page.getByLabel(/format/i).click();
    await page.getByRole('option', { name: 'PDF' }).click();

    // STEP 4: Schedule configuration
    await page.getByLabel(/schedule/i).click();
    await page.getByRole('option', { name: /daily \(9 am\)/i }).click();

    // STEP 5: Set email recipients
    const testRecipients = ['admin@test.com', 'ops@test.com'];
    await page.getByLabel(/email recipients/i).fill(testRecipients.join(', '));

    // STEP 6: Create scheduled report
    await page.getByRole('button', { name: /schedule report/i }).click();
    await expect(page.getByText(/report scheduled successfully/i)).toBeVisible();

    // STEP 7: Trigger immediate execution
    const reportRow = page.getByRole('row', { name: /daily system health/i }).first();
    await reportRow.getByRole('button', { name: /execute now/i }).click();

    // Wait for execution to complete
    await expect(page.getByText(/report generated successfully/i)).toBeVisible({ timeout: 15000 });

    // STEP 8: Verify email was sent to Mailpit
    const email = await waitForEmail(request, /Report Ready.*Daily System Health/i, 15000);

    expect(email.Subject).toContain('Report Ready');
    expect(email.Subject).toContain('Daily System Health');
    expect(email.From.Address).toBe('noreply@meepleai.dev');
    expect(email.To).toHaveLength(2);
    expect(email.To.map(t => t.Address)).toEqual(expect.arrayContaining(testRecipients));

    // STEP 9: Verify email contains attachment
    const emailDetail = await getMailpitMessageDetail(request, email.ID);
    expect(emailDetail.Attachments).toHaveLength(1);

    const attachment = emailDetail.Attachments[0];
    expect(attachment.FileName).toMatch(/SystemHealth_.*\.pdf/);
    expect(attachment.ContentType).toContain('application/pdf');
    expect(attachment.Size).toBeGreaterThan(0);
  });

  // --------------------------------------------------------------------------
  // SCENARIO 2: Email HTML Rendering Verification
  // --------------------------------------------------------------------------

  test('should render email HTML with correct structure and styling', async ({ page, request }) => {
    // Generate and trigger report (simplified flow)
    await page.getByRole('tab', { name: /scheduled/i }).click();
    await page.getByRole('button', { name: /schedule new report/i }).click();

    await page.getByLabel(/report name/i).fill('Test HTML Rendering');
    await page.getByLabel(/report template/i).click();
    await page.getByRole('option', { name: 'User Activity' }).click();
    await page.getByLabel(/email recipients/i).fill('test@example.com');
    await page.getByRole('button', { name: /schedule report/i }).click();

    await page
      .getByRole('row', { name: /test html rendering/i })
      .first()
      .getByRole('button', { name: /execute now/i })
      .click();
    await expect(page.getByText(/report generated successfully/i)).toBeVisible({ timeout: 15000 });

    // Wait for email
    const email = await waitForEmail(request, /Report Ready.*Test HTML Rendering/i, 15000);
    const emailDetail = await getMailpitMessageDetail(request, email.ID);

    // Verify HTML structure
    expect(emailDetail.HTML).toContain('MeepleAI');
    expect(emailDetail.HTML).toContain('Report Ready');
    expect(emailDetail.HTML).toContain('Test HTML Rendering');
    expect(emailDetail.HTML).toContain('Automated daily health check');

    // Verify HTML styling presence (inline styles)
    expect(emailDetail.HTML).toContain('background-color');
    expect(emailDetail.HTML).toContain('padding');
    expect(emailDetail.HTML).toContain('border-radius');

    // Navigate to Mailpit Web UI for visual verification
    await page.goto(MAILPIT_WEB_UI);
    await expect(page.locator('h1')).toContainText('Mailpit');

    // Find and open email
    await page
      .getByText(/Report Ready.*Test HTML Rendering/i)
      .first()
      .click();

    // Wait for email preview to load
    await expect(page.locator('iframe[title="HTML preview"]')).toBeVisible();

    // Switch to HTML preview iframe
    const htmlFrame = page.frameLocator('iframe[title="HTML preview"]');

    // Verify rendered HTML content
    await expect(htmlFrame.locator('h1')).toContainText('MeepleAI');
    await expect(htmlFrame.locator('h2')).toContainText('Report Ready');
    await expect(htmlFrame.getByText('Test HTML Rendering')).toBeVisible();

    // Screenshot for manual visual verification
    await page.screenshot({
      path: 'test-results/email-html-rendering-verification.png',
      fullPage: true,
    });
  });

  // --------------------------------------------------------------------------
  // SCENARIO 3: Email Attachment Validation
  // --------------------------------------------------------------------------

  test('should include valid PDF attachment in report email', async ({ page, request }) => {
    // Generate report with PDF format
    await page.getByRole('tab', { name: /scheduled/i }).click();
    await page.getByRole('button', { name: /schedule new report/i }).click();

    await page.getByLabel(/report name/i).fill('PDF Attachment Test');
    await page.getByLabel(/report template/i).click();
    await page.getByRole('option', { name: 'Content Metrics' }).click();
    await page.getByLabel(/format/i).click();
    await page.getByRole('option', { name: 'PDF' }).click();
    await page.getByLabel(/email recipients/i).fill('test@example.com');
    await page.getByRole('button', { name: /schedule report/i }).click();

    await page
      .getByRole('row', { name: /pdf attachment test/i })
      .first()
      .getByRole('button', { name: /execute now/i })
      .click();
    await expect(page.getByText(/report generated successfully/i)).toBeVisible({ timeout: 15000 });

    // Wait for email
    const email = await waitForEmail(request, /Report Ready.*PDF Attachment Test/i, 15000);
    const emailDetail = await getMailpitMessageDetail(request, email.ID);

    // Verify attachment properties
    expect(emailDetail.Attachments).toHaveLength(1);

    const attachment = emailDetail.Attachments[0];

    // Filename validation
    expect(attachment.FileName).toMatch(/ContentMetrics_\d{8}_\d{6}\.pdf/);

    // Content type validation
    expect(attachment.ContentType).toContain('application/pdf');

    // Size validation (should be > 1KB for valid PDF)
    expect(attachment.Size).toBeGreaterThan(1024);

    // File size should be reasonable (< 10MB for test reports)
    expect(attachment.Size).toBeLessThan(10 * 1024 * 1024);
  });

  // --------------------------------------------------------------------------
  // SCENARIO 4: Multiple Email Recipients
  // --------------------------------------------------------------------------

  test('should send email to multiple recipients', async ({ page, request }) => {
    const recipients = ['admin@test.com', 'ops@test.com', 'devteam@example.org'];

    await page.getByRole('tab', { name: /scheduled/i }).click();
    await page.getByRole('button', { name: /schedule new report/i }).click();

    await page.getByLabel(/report name/i).fill('Multi-Recipient Test');
    await page.getByLabel(/report template/i).click();
    await page.getByRole('option', { name: 'AI/LLM Usage' }).click();
    await page.getByLabel(/email recipients/i).fill(recipients.join(', '));
    await page.getByRole('button', { name: /schedule report/i }).click();

    await page
      .getByRole('row', { name: /multi-recipient test/i })
      .first()
      .getByRole('button', { name: /execute now/i })
      .click();
    await expect(page.getByText(/report generated successfully/i)).toBeVisible({ timeout: 15000 });

    // Wait for email
    const email = await waitForEmail(request, /Report Ready.*Multi-Recipient Test/i, 15000);

    // Verify all recipients received the email
    expect(email.To).toHaveLength(3);
    const recipientAddresses = email.To.map(t => t.Address);

    recipients.forEach(recipient => {
      expect(recipientAddresses).toContain(recipient);
    });
  });

  // --------------------------------------------------------------------------
  // SCENARIO 5: Email Failure Notification
  // --------------------------------------------------------------------------

  test('should handle email delivery failures gracefully', async ({ page, request }) => {
    // This test verifies the system handles SMTP errors without breaking report generation
    // In a real scenario, SMTP might be down or misconfigured

    // For this test, we verify the execution history shows proper status
    await page.getByRole('tab', { name: /history/i }).click();

    // If there are any failed email deliveries, they should be visible
    // and the report itself should still be marked as completed
    const historyTable = page.getByRole('table');
    await expect(historyTable).toBeVisible();

    // Check if any "Completed with Email Failure" status exists
    const emailFailureStatus = page.getByText(/email.*failed/i);

    if (await emailFailureStatus.isVisible()) {
      // If email failure exists, verify error details are accessible
      const failedRow = page.getByRole('row', { name: /email.*failed/i }).first();
      await failedRow.getByRole('button', { name: /details/i }).click();

      await expect(page.getByText(/smtp|email|delivery/i)).toBeVisible();
    }
  });

  // --------------------------------------------------------------------------
  // SCENARIO 6: Visual Regression - Email Rendering
  // --------------------------------------------------------------------------

  test('should render email with consistent visual appearance', async ({ page, request }) => {
    // Generate report
    await page.getByRole('tab', { name: /scheduled/i }).click();
    await page.getByRole('button', { name: /schedule new report/i }).click();

    await page.getByLabel(/report name/i).fill('Visual Regression Test');
    await page.getByLabel(/report template/i).click();
    await page.getByRole('option', { name: 'System Health' }).click();
    await page.getByLabel(/email recipients/i).fill('visual-test@example.com');
    await page.getByRole('button', { name: /schedule report/i }).click();

    await page
      .getByRole('row', { name: /visual regression test/i })
      .first()
      .getByRole('button', { name: /execute now/i })
      .click();
    await expect(page.getByText(/report generated successfully/i)).toBeVisible({ timeout: 15000 });

    // Wait for email
    await waitForEmail(request, /Report Ready.*Visual Regression Test/i, 15000);

    // Navigate to Mailpit and open email
    await page.goto(MAILPIT_WEB_UI);
    await page
      .getByText(/Report Ready.*Visual Regression Test/i)
      .first()
      .click();

    // Wait for HTML preview
    const htmlFrame = page.frameLocator('iframe[title="HTML preview"]');
    await expect(htmlFrame.locator('h1')).toBeVisible();

    // Screenshot entire email for visual regression
    await page.screenshot({
      path: 'test-results/email-visual-regression-full.png',
      fullPage: true,
    });

    // Screenshot just the email body (HTML iframe)
    const iframeElement = page.locator('iframe[title="HTML preview"]');
    const boundingBox = await iframeElement.boundingBox();

    if (boundingBox) {
      await page.screenshot({
        path: 'test-results/email-visual-regression-body.png',
        clip: boundingBox,
      });
    }

    // Verify color scheme consistency
    const headerBg = await htmlFrame
      .locator('.header, h1')
      .first()
      .evaluate(el => window.getComputedStyle(el).backgroundColor);

    // Should use brand colors (verify not default browser colors)
    expect(headerBg).not.toBe('rgba(0, 0, 0, 0)'); // Not transparent
    expect(headerBg).not.toBe('rgb(255, 255, 255)'); // Not white
  });
});

// ============================================================================
// Test Suite: Mailpit Service Health
// ============================================================================

test.describe('Mailpit Service Health', () => {
  test('should have Mailpit service running and accessible', async ({ request }) => {
    const response = await request.get(`${MAILPIT_API_BASE}/messages`);
    expect(response.ok()).toBeTruthy();
    expect(response.status()).toBe(200);
  });

  test('should be able to clear messages via API', async ({ request }) => {
    // Clear all messages
    const deleteResponse = await request.delete(`${MAILPIT_API_BASE}/messages`);
    expect(deleteResponse.ok()).toBeTruthy();

    // Verify messages cleared
    const messages = await getMailpitMessages(request);
    expect(messages).toHaveLength(0);
  });
});
