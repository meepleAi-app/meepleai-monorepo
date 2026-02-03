/**
 * E2E Login Test with Screenshots - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/auth/LoginPage.ts
 */

import { test, expect } from './fixtures';

const WEB_BASE = 'http://localhost:3000';

/**
 * Login Flow with Screenshot Capture
 *
 * Tests the complete login flow with screenshot capture at each step:
 * 1. Navigate to homepage
 * 2. Open login modal
 * 3. Fill credentials
 * 4. Submit and verify redirect
 */
test.describe('Login Flow with Screenshots', () => {
  test.beforeEach(async ({ page }) => {
    // Disable animations for stable screenshots
    await page.emulateMedia({ reducedMotion: 'reduce' });
  });

  test('user login with screenshots', async ({ page }) => {
    // Step 1: Navigate to homepage
    await page.goto(WEB_BASE);
    await page.waitForLoadState('domcontentloaded');
    await page.screenshot({ path: 'test-results/01-homepage.png', fullPage: true });
    console.log('✓ Screenshot 1: Homepage');

    // Step 2: Navigate directly to login page
    await page.goto(`${WEB_BASE}/login`);
    await page.waitForLoadState('domcontentloaded');

    // Wait for login form using accessible selectors
    await expect(page.getByLabel(/email/i)).toBeVisible({ timeout: 10000 });
    await page.screenshot({ path: 'test-results/02-login-form.png', fullPage: true });
    console.log('✓ Screenshot 2: Login Form');

    // Step 3: Fill credentials using getByLabel (accessible selectors)
    await page.getByLabel(/email/i).fill('user@meepleai.dev');
    await page.screenshot({ path: 'test-results/03-email-filled.png', fullPage: true });
    console.log('✓ Screenshot 3: Email Filled');

    await page.getByLabel(/password/i).fill('Demo123!');
    await page.screenshot({ path: 'test-results/04-password-filled.png', fullPage: true });
    console.log('✓ Screenshot 4: Password Filled');

    // Step 4: Submit login using data-testid (locale-independent selector)
    const loginButton = page.locator('[data-testid="login-submit"]');
    await expect(loginButton).toBeVisible({ timeout: 5000 });
    await loginButton.click();

    await page.screenshot({ path: 'test-results/05-login-submitted.png', fullPage: true });
    console.log('✓ Screenshot 5: Login Submitted');

    // Step 5: Wait for redirect and verify
    await page.waitForURL('**/chat', { timeout: 15000 });
    await page.waitForLoadState('domcontentloaded');

    // Verify we're on chat page
    await expect(page.url()).toContain('/chat');
    await page.screenshot({ path: 'test-results/06-chat-page.png', fullPage: true });
    console.log('✓ Screenshot 6: Chat Page (Logged In)');

    // Verify chat UI elements are present using getByRole
    const chatHeading = page.getByRole('heading', { name: /chat/i });
    await expect(chatHeading).toBeVisible({ timeout: 5000 });

    await page.screenshot({ path: 'test-results/07-final-state.png', fullPage: true });
    console.log('✓ Screenshot 7: Final State');

    console.log('\n✅ Login test completed successfully');
    console.log('📸 Screenshots saved in test-results/ directory');
  });
});
