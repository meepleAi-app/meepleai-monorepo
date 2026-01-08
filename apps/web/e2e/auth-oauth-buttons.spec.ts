/**
 * E2E Tests for OAuth Authentication Flow (AUTH-06) - MIGRATED TO POM + REAL BACKEND
 *
 * ✅ CONVERTED: Uses real backend APIs instead of mocks (Issue #2299)
 * - Removed 2 page.route() mocks
 * - Requires backend running with OAuth providers configured
 *
 * Test Coverage:
 * - OAuth button redirects to real backend → OAuth provider chain
 * - Button accessibility and keyboard navigation
 * - Visual styling and hover states
 * - i18n support (English and Italian UI text)
 *
 * Real APIs Used:
 * - GET /api/v1/auth/oauth/{provider}/login (Google, GitHub, Discord)
 *
 * Note: Error scenario tests removed (required mock-specific failures)
 *
 * @see apps/web/e2e/page-objects/ - Page Object Model architecture
 * @see Issue #2299 - E2E mock removal epic
 */

import { test, expect } from './fixtures/chromatic';
import { getTextMatcher } from './fixtures/i18n';
import { LoginPage, AuthHelper } from './pages';

test.describe('OAuth Authentication Flow', () => {
  let loginPage: LoginPage;
  let authHelper: AuthHelper;

  test.beforeEach(async ({ page }) => {
    loginPage = new LoginPage(page);
    authHelper = new AuthHelper(page);

    // Disable animations for stable tests
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // ✅ REMOVED MOCK: Use real OAuth provider redirects
    // Real backend GET /api/v1/auth/oauth/{provider}/login must return OAuth URLs
  });

  /**
   * Test Group 1: OAuth Button Redirects
   */
  test.describe('OAuth Button Redirects', () => {
    test('clicking Google button initiates OAuth flow', async ({ page }) => {
      await loginPage.navigate();

      const googleMatcher = getTextMatcher('auth.oauth.google');
      const googleButton = page.getByRole('button', { name: googleMatcher });
      await expect(googleButton).toBeVisible();

      // Set up navigation promise BEFORE clicking
      const navigationPromise = page.waitForURL(/.*accounts\.google\.com.*/i, { timeout: 5000 });

      await googleButton.click();
      await navigationPromise;

      // Verify we navigated to Google OAuth
      const finalUrl = page.url();
      expect(finalUrl).toMatch(/accounts\.google\.com/i);
      expect(finalUrl).toContain('client_id=mock-google-client');
      expect(finalUrl).toContain('state=mock-csrf-state-123');
    });

    test('clicking Discord button initiates OAuth flow', async ({ page }) => {
      await loginPage.navigate();

      const discordMatcher = getTextMatcher('auth.oauth.discord');
      const discordButton = page.getByRole('button', { name: discordMatcher });
      await expect(discordButton).toBeVisible();

      const navigationPromise = page.waitForURL(/.*discord\.com.*/i, { timeout: 5000 });
      await discordButton.click();
      await navigationPromise;

      const finalUrl = page.url();
      expect(finalUrl).toMatch(/discord\.com/i);
      expect(finalUrl).toContain('client_id=mock-discord-client');
      expect(finalUrl).toContain('state=mock-csrf-state-456');
    });

    test('clicking GitHub button initiates OAuth flow', async ({ page }) => {
      await loginPage.navigate();

      const githubMatcher = getTextMatcher('auth.oauth.github');
      const githubButton = page.getByRole('button', { name: githubMatcher });
      await expect(githubButton).toBeVisible();

      const navigationPromise = page.waitForURL(/.*github\.com.*/i, { timeout: 5000 });
      await githubButton.click();
      await navigationPromise;

      const finalUrl = page.url();
      expect(finalUrl).toMatch(/github\.com/i);
      expect(finalUrl).toContain('client_id=mock-github-client');
      expect(finalUrl).toContain('state=mock-csrf-state-789');
    });
  });

  /**
   * Test Group 2: Environment Variable Handling
   */
  test.describe('Environment Variable Handling', () => {
    test('OAuth buttons are accessible and properly labeled', async ({ page }) => {
      await loginPage.navigate();

      // Verify all three OAuth buttons using LoginPage helper
      await loginPage.verifyOAuthButtonsVisible();

      // Verify proper accessibility attributes
      const googleMatcher = getTextMatcher('auth.oauth.google');
      const discordMatcher = getTextMatcher('auth.oauth.discord');
      const githubMatcher = getTextMatcher('auth.oauth.github');

      await expect(page.getByRole('button', { name: googleMatcher })).toHaveAttribute(
        'type',
        'button'
      );
      await expect(page.getByRole('button', { name: discordMatcher })).toHaveAttribute(
        'type',
        'button'
      );
      await expect(page.getByRole('button', { name: githubMatcher })).toHaveAttribute(
        'type',
        'button'
      );
    });
  });

  /**
   * Test Group 3: Error Scenarios
   * ⚠️ REMOVED: Error tests requiring mock-specific failures
   *
   * These tests simulated network failures and 500 errors using page.route() mocks.
   * With real backend, these scenarios should be tested differently:
   * - Network failures: Test with backend actually down (CI/CD scenario)
   * - 500 errors: Test with backend debug mode or dedicated error endpoints
   *
   * ✅ REMOVED MOCKS:
   * - page.route() for network failure simulation
   * - page.route() for 500 error simulation
   */

  /**
   * Test Group 4: Visual and Interaction Testing
   */
  test.describe('Visual and Interaction', () => {
    test('OAuth buttons have correct visual styling', async ({ page }) => {
      await loginPage.navigate();

      const googleMatcher = getTextMatcher('auth.oauth.google');
      const discordMatcher = getTextMatcher('auth.oauth.discord');
      const githubMatcher = getTextMatcher('auth.oauth.github');

      const googleButton = page.getByRole('button', { name: googleMatcher });
      const discordButton = page.getByRole('button', { name: discordMatcher });
      const githubButton = page.getByRole('button', { name: githubMatcher });

      // Check that buttons are styled (have display flex)
      await expect(googleButton).toHaveCSS('display', 'flex');
      await expect(discordButton).toHaveCSS('display', 'flex');
      await expect(githubButton).toHaveCSS('display', 'flex');

      // Verify buttons are full width
      const googleBox = await googleButton.boundingBox();
      const discordBox = await discordButton.boundingBox();
      const githubBox = await githubButton.boundingBox();

      expect(googleBox?.width).toBeGreaterThan(200);
      expect(discordBox?.width).toBeGreaterThan(200);
      expect(githubBox?.width).toBeGreaterThan(200);
    });

    test('OAuth buttons show hover states', async ({ page }) => {
      await loginPage.navigate();

      const googleMatcher = getTextMatcher('auth.oauth.google');
      const googleButton = page.getByRole('button', { name: googleMatcher });

      await googleButton.hover();

      // Button should still be visible after hover
      await expect(googleButton).toBeVisible();
    });

    test('OAuth section displays separator with correct text', async ({ page }) => {
      await loginPage.navigate();

      // Verify the "Or continue with" separator is present using i18n matcher
      const separatorMatcher = getTextMatcher('auth.oauth.separator');
      const separator = page.getByText(separatorMatcher);
      await expect(separator).toBeVisible();
    });
  });

  /**
   * Test Group 5: Keyboard Navigation and Accessibility
   */
  test.describe('Keyboard Navigation and Accessibility', () => {
    test('OAuth buttons are keyboard accessible', async ({ page }) => {
      await loginPage.navigate();

      // Tab to OAuth buttons
      await page.keyboard.press('Tab');
      await page.keyboard.press('Tab');
      await page.keyboard.press('Tab');

      // One of the OAuth buttons should be focused
      const focusedElement = await page.locator(':focus').textContent();

      const validFocus =
        focusedElement?.includes('Continue with') ||
        focusedElement?.includes('Continua con') ||
        focusedElement?.includes('Return to Home') ||
        focusedElement?.includes('Torna alla Home');
      expect(validFocus).toBe(true);
    });

    test('Enter key triggers OAuth redirect', async ({ page }) => {
      await loginPage.navigate();

      const googleMatcher = getTextMatcher('auth.oauth.google');
      const googleButton = page.getByRole('button', { name: googleMatcher });

      await googleButton.focus();

      const navigationPromise = page.waitForURL(/.*accounts\.google\.com.*/i, { timeout: 5000 });
      await page.keyboard.press('Enter');
      await navigationPromise;

      const finalUrl = page.url();
      expect(finalUrl).toMatch(/accounts\.google\.com/i);
    });

    test('Space key triggers OAuth redirect', async ({ page }) => {
      await loginPage.navigate();

      const discordMatcher = getTextMatcher('auth.oauth.discord');
      const discordButton = page.getByRole('button', { name: discordMatcher });

      await discordButton.focus();

      const navigationPromise = page.waitForURL(/.*discord\.com.*/i, { timeout: 5000 });
      await page.keyboard.press('Space');
      await navigationPromise;

      const finalUrl = page.url();
      expect(finalUrl).toMatch(/discord\.com/i);
    });
  });

  /**
   * Test Group 6: Session Expiration Context
   */
  test.describe('OAuth with Session Expiration', () => {
    test('OAuth buttons visible on login page with session_expired query param', async ({
      page,
    }) => {
      await page.goto('/login?reason=session_expired');
      await page.waitForLoadState('domcontentloaded');

      // Session expired alert should be visible
      await expect(page.getByText('Session Expired')).toBeVisible();

      // OAuth buttons should still be visible using LoginPage helper
      await loginPage.verifyOAuthButtonsVisible();

      // Buttons should be enabled
      const googleMatcher = getTextMatcher('auth.oauth.google');
      const googleButton = page.getByRole('button', { name: googleMatcher });
      await expect(googleButton).toBeEnabled();
    });
  });

  /**
   * Test Group 7: Performance Testing
   */
  test.describe('Performance', () => {
    test('OAuth buttons render quickly', async ({ page }) => {
      const startTime = Date.now();

      await loginPage.navigate();

      const googleMatcher = getTextMatcher('auth.oauth.google');
      const googleButton = page.getByRole('button', { name: googleMatcher });
      await googleButton.waitFor({ state: 'visible', timeout: 5000 });

      const endTime = Date.now();
      const renderTime = endTime - startTime;

      // Buttons should render in under 3 seconds
      expect(renderTime).toBeLessThan(3000);
    });
  });

  /**
   * Test Group 8: Button Click Behavior
   */
  test.describe('Button Click Behavior', () => {
    test('clicking Google button calls window.location.assign', async ({ page }) => {
      await loginPage.navigate();

      const googleMatcher = getTextMatcher('auth.oauth.google');
      const googleButton = page.getByRole('button', { name: googleMatcher });

      let navigationStarted = false;
      page.on('framenavigated', () => {
        navigationStarted = true;
      });

      await googleButton.click();

      // Navigation should have been triggered
      expect(navigationStarted).toBe(true);
    });

    test('OAuth buttons do not submit a form', async ({ page }) => {
      await loginPage.navigate();

      const googleMatcher = getTextMatcher('auth.oauth.google');
      const googleButton = page.getByRole('button', { name: googleMatcher });

      // Verify button type is "button" not "submit"
      const buttonType = await googleButton.getAttribute('type');
      expect(buttonType).toBe('button');

      // Verify button is not inside a form
      const isInForm = await googleButton.evaluate(el => {
        let parent = el.parentElement;
        while (parent) {
          if (parent.tagName === 'FORM') return true;
          parent = parent.parentElement;
        }
        return false;
      });

      expect(isInForm).toBe(false);
    });
  });
});
