import { Page } from '@playwright/test';

const API_BASE = 'http://localhost:8080';

/**
 * Mock email service for password reset testing
 *
 * Simulates email delivery without actually sending emails.
 * Captures reset tokens for testing purposes.
 */

/**
 * Storage for captured email data
 */
interface EmailData {
  to: string;
  subject: string;
  resetToken?: string;
  timestamp: number;
}

const emailStorage: EmailData[] = [];

/**
 * Setup mock email service
 * Intercepts password reset request and extracts token
 */
export async function setupMockEmailService(page: Page): Promise<void> {
  // Clear previous emails
  emailStorage.length = 0;

  // Mock password reset request endpoint
  await page.route(`${API_BASE}/api/v1/auth/password-reset/request`, async (route) => {
    const request = route.request();
    if (request.method() === 'POST') {
      const postData = JSON.parse(request.postData() || '{}');
      const email = postData.email;

      // Generate a mock reset token
      const resetToken = generateMockResetToken();

      // Store email data
      emailStorage.push({
        to: email,
        subject: 'Password Reset Request',
        resetToken,
        timestamp: Date.now(),
      });

      // Return success response
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true }),
      });
    } else {
      await route.continue();
    }
  });
}

/**
 * Setup mock token verification endpoint
 * @param isValid - Whether token should be valid
 */
export async function setupMockTokenVerification(
  page: Page,
  isValid: boolean = true
): Promise<void> {
  // Use a function matcher for more flexible route matching
  await page.route(
    (url) => url.href.includes('/api/v1/auth/password-reset/verify'),
    async (route) => {
      console.log(`[MOCK] Token verification called: ${route.request().url()}, isValid: ${isValid}`);
      if (isValid) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ valid: true }),
        });
      } else {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Invalid or expired token' }),
        });
      }
    }
  );
}

/**
 * Setup mock password reset confirmation endpoint
 * @param shouldSucceed - Whether reset should succeed
 */
export async function setupMockPasswordResetConfirm(
  page: Page,
  shouldSucceed: boolean = true
): Promise<void> {
  // Use function matcher for more reliable route interception
  await page.route(
    (url) => url.href.includes('/api/v1/auth/password-reset/confirm'),
    async (route) => {
      const request = route.request();
      if (request.method() === 'PUT') {
        console.log(`[MOCK] Password reset confirm called, shouldSucceed: ${shouldSucceed}`);
        if (shouldSucceed) {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ success: true }),
          });
        } else {
          await route.fulfill({
            status: 400,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'Failed to reset password' }),
          });
        }
      } else {
        await route.continue();
      }
    }
  );
}

/**
 * Get the most recent reset token for an email
 * @param email - Email address
 * @returns Reset token or null if not found
 */
export function getResetTokenForEmail(email: string): string | null {
  const emailData = emailStorage
    .filter((e) => e.to.toLowerCase() === email.toLowerCase())
    .sort((a, b) => b.timestamp - a.timestamp)[0];

  return emailData?.resetToken || null;
}

/**
 * Get all captured emails
 */
export function getCapturedEmails(): EmailData[] {
  return [...emailStorage];
}

/**
 * Clear all captured emails
 */
export function clearCapturedEmails(): void {
  emailStorage.length = 0;
}

/**
 * Generate a mock reset token (mimics backend format)
 * Format: URL-safe base64 string
 */
function generateMockResetToken(): string {
  const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_';
  let token = '';
  for (let i = 0; i < 43; i++) {
    token += chars.charAt(Math.floor(Math.random() * chars.length));
  }
  return token;
}

/**
 * Check if email was sent to address
 * @param email - Email address
 * @returns True if email was sent
 */
export function wasEmailSentTo(email: string): boolean {
  return emailStorage.some((e) => e.to.toLowerCase() === email.toLowerCase());
}

/**
 * Get number of emails sent
 */
export function getEmailCount(): number {
  return emailStorage.length;
}
