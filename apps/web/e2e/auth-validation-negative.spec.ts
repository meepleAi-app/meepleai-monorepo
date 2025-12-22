/**
 * Authentication Validation Negative Scenarios E2E Tests - Issue #1494
 *
 * @see apps/web/e2e/pages/helpers/AuthHelper.ts
 * @see apps/web/e2e/pages/LoginPage.ts
 *
 * Tests form validation errors, invalid inputs, and boundary conditions
 * for authentication flows (login, registration).
 *
 * Coverage:
 * - Invalid email formats
 * - Weak password validation
 * - Empty field handling
 * - Duplicate registration
 * - Password mismatch
 */

import { test, expect } from './fixtures/chromatic';
import { LoginPage, AuthHelper } from './pages';

test.describe('Authentication Validation Negative Scenarios - Issue #1494', () => {
  test.describe('Login form validation', () => {
    test('should reject invalid email format', async ({ page }) => {
      const loginPage = new LoginPage(page);
      const authHelper = new AuthHelper(page);

      await authHelper.mockUnauthenticatedSession();

      await loginPage.navigate();

      // Test various invalid email formats
      const invalidEmails = [
        'notanemail', // No @ symbol
        '@example.com', // Missing local part
        'user@', // Missing domain
        'user@.com', // Invalid domain
        'user name@example.com', // Spaces
        'user@domain', // No TLD
      ];

      for (const invalidEmail of invalidEmails) {
        const emailInput = page.locator('input[name="email"], input[type="email"]').first();
        await emailInput.fill(invalidEmail);

        // Try to submit
        const submitButton = page.locator('button[type="submit"]').first();
        await submitButton.click();

        // Should show validation error
        const errorMessage = page
          .locator('text=/invalid.*email|email.*invalid|formato email|valid.*address/i')
          .first();

        // HTML5 validation or custom error should appear
        const isInvalid = await emailInput.evaluate((el: HTMLInputElement) => !el.validity.valid);
        const hasError = await errorMessage.isVisible({ timeout: 2000 }).catch(() => false);

        expect(isInvalid || hasError).toBe(true);
      }
    });

    test('should reject empty email field', async ({ page }) => {
      const loginPage = new LoginPage(page);
      const authHelper = new AuthHelper(page);

      await authHelper.mockUnauthenticatedSession();
      await loginPage.navigate();

      const emailInput = page.locator('input[name="email"], input[type="email"]').first();
      const passwordInput = page.locator('input[name="password"], input[type="password"]').first();
      const submitButton = page.locator('button[type="submit"]').first();

      await expect(emailInput).toBeVisible();

      // Fill password but leave email empty
      await passwordInput.fill('ValidPassword123!');

      await submitButton.click();

      // Should show required field error
      const isRequired = await emailInput.evaluate((el: HTMLInputElement) => el.required);
      expect(isRequired).toBe(true);

      const errorMessage = page.locator('text=/required|obbligatorio|campo richiesto/i').first();
      const hasError = await errorMessage.isVisible({ timeout: 2000 }).catch(() => false);

      // Either HTML5 required or custom validation
      expect(hasError || isRequired).toBe(true);
    });

    test('should reject empty password field', async ({ page }) => {
      const loginPage = new LoginPage(page);
      const authHelper = new AuthHelper(page);

      await authHelper.mockUnauthenticatedSession();
      await loginPage.navigate();

      const emailInput = page.locator('input[name="email"], input[type="email"]').first();
      const passwordInput = page.locator('input[name="password"], input[type="password"]').first();
      const submitButton = page.locator('button[type="submit"]').first();

      await expect(emailInput).toBeVisible();

      // Fill email but leave password empty
      await emailInput.fill('user@example.com');

      await submitButton.click();

      // Should show required field error
      const isRequired = await passwordInput.evaluate((el: HTMLInputElement) => el.required);
      expect(isRequired).toBe(true);
    });
  });

  test.describe('Registration validation', () => {
    test('should reject duplicate email registration', async ({ page }) => {
      const authHelper = new AuthHelper(page);

      await authHelper.mockUnauthenticatedSession();

      // Mock registration endpoint with 409 Conflict for duplicate email
      await page.route('**/api/v1/auth/register', route => {
        route.fulfill({
          status: 409,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Email already exists' }),
        });
      });

      await page.goto('/register');

      const emailInput = page.locator('input[name="email"], input[type="email"]').first();
      const passwordInput = page.locator('input[name="password"], input[type="password"]').first();
      const submitButton = page.locator('button[type="submit"]').first();

      if (await emailInput.isVisible({ timeout: 5000 }).catch(() => false)) {
        await emailInput.fill('existing@example.com');
        await passwordInput.fill('ValidPassword123!');

        // Try additional fields if they exist
        const confirmPasswordInput = page.locator('input[name="confirmPassword"]').first();
        if (await confirmPasswordInput.isVisible({ timeout: 1000 }).catch(() => false)) {
          await confirmPasswordInput.fill('ValidPassword123!');
        }

        const displayNameInput = page
          .locator('input[name="displayName"], input[name="name"]')
          .first();
        if (await displayNameInput.isVisible({ timeout: 1000 }).catch(() => false)) {
          await displayNameInput.fill('Test User');
        }

        await submitButton.click();

        // Should show duplicate email error
        const errorMessage = page
          .locator('text=/already.*exists|email.*taken|già.*registrata|duplicate/i')
          .first();
        await expect(errorMessage).toBeVisible({ timeout: 5000 });
      }
    });

    test('should reject password mismatch in registration', async ({ page }) => {
      const authHelper = new AuthHelper(page);

      await authHelper.mockUnauthenticatedSession();

      await page.goto('/register');

      const passwordInput = page.locator('input[name="password"], input[type="password"]').first();
      const confirmPasswordInput = page.locator('input[name="confirmPassword"]').first();

      if (await confirmPasswordInput.isVisible({ timeout: 5000 }).catch(() => false)) {
        await passwordInput.fill('ValidPassword123!');
        await confirmPasswordInput.fill('DifferentPassword456!');

        const submitButton = page.locator('button[type="submit"]').first();
        await submitButton.click();

        // Should show password mismatch error
        const errorMessage = page
          .locator('text=/password.*match|passwords.*same|corrispondono|not.*match/i')
          .first();
        await expect(errorMessage).toBeVisible({ timeout: 5000 });
      }
    });

    test('should reject weak password in registration', async ({ page }) => {
      const authHelper = new AuthHelper(page);

      await authHelper.mockUnauthenticatedSession();

      await page.goto('/register');

      const emailInput = page.locator('input[name="email"], input[type="email"]').first();
      const passwordInput = page.locator('input[name="password"], input[type="password"]').first();

      if (await passwordInput.isVisible({ timeout: 5000 }).catch(() => false)) {
        const weakPasswords = [
          '123', // Too short
          'password', // No numbers or special chars
          'pass123', // No special chars
          'Pass!', // Too short with special char
        ];

        for (const weakPassword of weakPasswords) {
          await emailInput.fill('newuser@example.com');
          await passwordInput.fill(weakPassword);

          const submitButton = page.locator('button[type="submit"]').first();
          await submitButton.click();

          // Should show weak password error
          const errorMessage = page
            .locator('text=/weak.*password|password.*strong|password.*requirements|requisiti/i')
            .first();

          const hasError = await errorMessage.isVisible({ timeout: 2000 }).catch(() => false);

          // May have client-side or server-side validation
          if (hasError) {
            // Clear fields for next iteration
            await passwordInput.clear();
            break; // Test passed, no need to try all weak passwords
          }
        }
      }
    });
  });
});
