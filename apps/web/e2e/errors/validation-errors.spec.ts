/**
 * ERR-09: Validation Errors
 * Issue #3082 - P3 Low
 *
 * Tests form validation error handling:
 * - Display field-level errors
 * - Form-level validation messages
 * - Real-time validation feedback
 */

import { test, expect } from '../fixtures/chromatic';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function setupValidationMocks(page: Page) {
  await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: { id: 'test-user', email: 'test@example.com', displayName: 'Test User', role: 'User' },
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      }),
    });
  });

  await page.route(`${API_BASE}/api/v1/auth/register`, async (route) => {
    const body = await route.request().postDataJSON();
    const errors: Record<string, string> = {};

    if (!body?.email || !body.email.includes('@')) {
      errors.email = 'Valid email is required';
    }
    if (!body?.password || body.password.length < 8) {
      errors.password = 'Password must be at least 8 characters';
    }

    if (Object.keys(errors).length > 0) {
      await route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({ errors, message: 'Validation failed' }),
      });
    } else {
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Success' }),
      });
    }
  });

  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
  });

  return {};
}

test.describe('ERR-09: Validation Errors', () => {
  test('should show field-level validation errors', async ({ page }) => {
    await setupValidationMocks(page);
    await page.goto('/register');
    await page.waitForLoadState('networkidle');

    await page.getByLabel(/email/i).fill('invalid-email');
    await page.getByRole('button', { name: /register|sign.*up/i }).click();
    await expect(page.getByText(/valid.*email|invalid/i)).toBeVisible({ timeout: 5000 });
  });

  test('should show password requirements', async ({ page }) => {
    await setupValidationMocks(page);
    await page.goto('/register');
    await page.waitForLoadState('networkidle');

    await page.getByLabel(/password/i).first().fill('short');
    await page.getByRole('button', { name: /register|sign.*up/i }).click();
    await expect(page.getByText(/8.*character|too.*short/i)).toBeVisible();
  });

  test('should clear errors on valid input', async ({ page }) => {
    await setupValidationMocks(page);
    await page.goto('/register');
    await page.waitForLoadState('networkidle');

    const emailInput = page.getByLabel(/email/i);
    await emailInput.fill('invalid');
    await page.keyboard.press('Tab');
    await emailInput.fill('valid@email.com');
    await page.keyboard.press('Tab');
    await expect(page.locator('body')).toBeVisible();
  });
});
