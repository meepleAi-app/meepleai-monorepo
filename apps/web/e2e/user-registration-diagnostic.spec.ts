/**
 * User Registration Diagnostic Test with Screenshots
 *
 * Purpose: Diagnose registration issues and capture visual evidence
 * Captures: Full registration flow with screenshots at each step
 */

import { test, expect } from '@playwright/test';

test.describe('User Registration Diagnostic', () => {
  test('complete registration flow with screenshots', async ({ page }) => {
    // Step 1: Navigate to home page
    await page.goto('http://localhost:3000');
    await page.screenshot({
      path: 'test-results/registration-01-homepage.png',
      fullPage: true,
    });

    // Step 2: Navigate to login page and switch to register tab
    await page.goto('http://localhost:3000/login');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000); // Wait for modal to open
    await page.screenshot({
      path: 'test-results/registration-02-login-page-loaded.png',
      fullPage: true,
    });

    // Step 3: Click on "Register" tab
    const registerTab = page.locator('button[role="tab"]').filter({ hasText: /^Register$/i });
    await registerTab.waitFor({ state: 'visible', timeout: 10000 });
    await registerTab.click();
    await page.waitForTimeout(500); // Wait for tab switch animation
    await page.screenshot({
      path: 'test-results/registration-03-register-tab-active.png',
      fullPage: true,
    });

    console.log('Current URL after tab click:', page.url());

    // Step 4: Fill registration form
    const timestamp = Date.now();
    const testEmail = `test-${timestamp}@meepleai.dev`;
    const testPassword = 'Test123!Pass';
    const testDisplayName = 'Test User';

    // Find and fill email
    const emailInput = page.locator('input[type="email"]').last();
    await emailInput.waitFor({ state: 'visible', timeout: 5000 });
    await emailInput.fill(testEmail);
    await page.screenshot({
      path: 'test-results/registration-04-email-filled.png',
      fullPage: true,
    });

    // Find and fill password
    const passwordInputs = page.locator('input[type="password"]');
    const passwordCount = await passwordInputs.count();

    if (passwordCount >= 1) {
      await passwordInputs.nth(0).fill(testPassword);
      await page.screenshot({
        path: 'test-results/registration-05-password-filled.png',
        fullPage: true,
      });
    }

    // Fill confirm password if exists
    if (passwordCount >= 2) {
      await passwordInputs.nth(1).fill(testPassword);
      await page.screenshot({
        path: 'test-results/registration-06-confirm-password-filled.png',
        fullPage: true,
      });
    }

    // Fill display name if exists
    const displayNameInput = page
      .locator('input:not([type="email"]):not([type="password"]):not([type="submit"])')
      .first();
    if (await displayNameInput.isVisible({ timeout: 2000 })) {
      await displayNameInput.fill(testDisplayName);
      await page.screenshot({
        path: 'test-results/registration-07-displayname-filled.png',
        fullPage: true,
      });
    }

    // Step 5: Submit registration
    const submitButton = page.getByRole('button', {
      name: /create account|crea account|register|registrati/i,
    });
    await submitButton.waitFor({ state: 'visible', timeout: 5000 });
    await page.screenshot({
      path: 'test-results/registration-08-before-submit.png',
      fullPage: true,
    });

    await submitButton.click();

    // Wait for response and capture
    await page.waitForTimeout(2000);
    await page.screenshot({
      path: 'test-results/registration-09-after-submit.png',
      fullPage: true,
    });

    // Step 6: Check for success or error
    const hasError = await page
      .locator('[role="alert"], .error, [class*="error"]')
      .isVisible({ timeout: 3000 })
      .catch(() => false);

    if (hasError) {
      await page.screenshot({
        path: 'test-results/registration-10-ERROR-detected.png',
        fullPage: true,
      });

      const errorText = await page
        .locator('[role="alert"], .error, [class*="error"]')
        .first()
        .textContent();
      console.log('❌ Registration Error:', errorText);
    }

    // Check if redirected to success page
    await page.waitForTimeout(1000);
    const currentUrl = page.url();
    console.log('Current URL after registration:', currentUrl);

    await page.screenshot({
      path: 'test-results/registration-11-final-state.png',
      fullPage: true,
    });

    // Diagnostic info
    console.log('=== Registration Diagnostic Info ===');
    console.log('Test Email:', testEmail);
    console.log('Final URL:', currentUrl);
    console.log('Has Error:', hasError);

    // Network requests log
    page.on('response', response => {
      if (response.url().includes('register') || response.url().includes('auth')) {
        console.log('API Response:', response.status(), response.url());
      }
    });
  });
});
