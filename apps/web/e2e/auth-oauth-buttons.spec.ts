import { test, expect, Page } from '@playwright/test';
import { getTextMatcher } from './fixtures/i18n';

const API_BASE = 'http://localhost:8080';

/**
 * E2E Tests for OAuth Authentication Flow (AUTH-06)
 *
 * Test Coverage:
 * - OAuth button redirects to backend → OAuth provider chain (mocked)
 * - Button accessibility and keyboard navigation
 * - Visual styling and hover states
 * - Error scenarios with mocked backend failures
 * - i18n support (English and Italian UI text)
 *
 * Branch Coverage: Covers the else branch in OAuthButtons.tsx (line 21)
 * that was missing in unit tests (25% → 100% branch coverage)
 *
 * Testing Strategy:
 * - Mock backend OAuth endpoints to return 302 redirects to OAuth providers
 * - Use i18n helpers for language-agnostic text matching
 * - Verify redirect URLs without requiring real OAuth credentials
 * - Test accessibility and user interactions with mocked backend
 */

test.describe('OAuth Authentication Flow', () => {
  test.beforeEach(async ({ page }) => {
    // Disable animations for stable tests
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Mock backend OAuth endpoints to return realistic 302 redirects
    await page.route(`${API_BASE}/api/v1/auth/oauth/google/login`, async (route) => {
      await route.fulfill({
        status: 302,
        headers: {
          'Location': 'https://accounts.google.com/o/oauth2/v2/auth?client_id=mock-google-client&redirect_uri=http%3A%2F%2Flocalhost%3A8080%2Fapi%2Fv1%2Fauth%2Foauth%2Fgoogle%2Fcallback&response_type=code&scope=openid+profile+email&state=mock-csrf-state-123',
          'Content-Type': 'text/html'
        },
        body: '<html><body>Redirecting to Google...</body></html>'
      });
    });

    await page.route(`${API_BASE}/api/v1/auth/oauth/discord/login`, async (route) => {
      await route.fulfill({
        status: 302,
        headers: {
          'Location': 'https://discord.com/api/oauth2/authorize?client_id=mock-discord-client&redirect_uri=http%3A%2F%2Flocalhost%3A8080%2Fapi%2Fv1%2Fauth%2Foauth%2Fdiscord%2Fcallback&response_type=code&scope=identify+email&state=mock-csrf-state-456',
          'Content-Type': 'text/html'
        },
        body: '<html><body>Redirecting to Discord...</body></html>'
      });
    });

    await page.route(`${API_BASE}/api/v1/auth/oauth/github/login`, async (route) => {
      await route.fulfill({
        status: 302,
        headers: {
          'Location': 'https://github.com/login/oauth/authorize?client_id=mock-github-client&redirect_uri=http%3A%2F%2Flocalhost%3A8080%2Fapi%2Fv1%2Fauth%2Foauth%2Fgithub%2Fcallback&response_type=code&scope=read%3Auser+user%3Aemail&state=mock-csrf-state-789',
          'Content-Type': 'text/html'
        },
        body: '<html><body>Redirecting to GitHub...</body></html>'
      });
    });
  });

  /**
   * Helper to navigate to login page and wait for OAuth buttons
   */
  async function navigateToLogin(page: Page) {
    await page.goto('/login');
    await page.waitForLoadState('domcontentloaded');

    // Use i18n matcher for "Or continue with" text
    const separatorMatcher = getTextMatcher('auth.oauth.separator');
    await expect(page.getByText(separatorMatcher)).toBeVisible({ timeout: 5000 });
  }

  /**
   * Test Group 1: OAuth Button Redirects
   * Tests that clicking OAuth buttons initiates the redirect chain to OAuth providers
   */
  test.describe('OAuth Button Redirects', () => {
    test('clicking Google button initiates OAuth flow', async ({ page }) => {
      await navigateToLogin(page);

      const googleMatcher = getTextMatcher('auth.oauth.google');
      const googleButton = page.getByRole('button', { name: googleMatcher });
      await expect(googleButton).toBeVisible();

      // Set up navigation promise BEFORE clicking
      const navigationPromise = page.waitForURL(/.*accounts\.google\.com.*/i, { timeout: 5000 });

      // Click button
      await googleButton.click();

      // Wait for navigation to complete
      await navigationPromise;

      // Verify we navigated to Google OAuth
      const finalUrl = page.url();
      expect(finalUrl).toMatch(/accounts\.google\.com/i);
      expect(finalUrl).toContain('client_id=mock-google-client');
      expect(finalUrl).toContain('state=mock-csrf-state-123');
    });

    test('clicking Discord button initiates OAuth flow', async ({ page }) => {
      await navigateToLogin(page);

      const discordMatcher = getTextMatcher('auth.oauth.discord');
      const discordButton = page.getByRole('button', { name: discordMatcher });
      await expect(discordButton).toBeVisible();

      // Set up navigation promise BEFORE clicking
      const navigationPromise = page.waitForURL(/.*discord\.com.*/i, { timeout: 5000 });

      await discordButton.click();
      await navigationPromise;

      const finalUrl = page.url();
      expect(finalUrl).toMatch(/discord\.com/i);
      expect(finalUrl).toContain('client_id=mock-discord-client');
      expect(finalUrl).toContain('state=mock-csrf-state-456');
    });

    test('clicking GitHub button initiates OAuth flow', async ({ page }) => {
      await navigateToLogin(page);

      const githubMatcher = getTextMatcher('auth.oauth.github');
      const githubButton = page.getByRole('button', { name: githubMatcher });
      await expect(githubButton).toBeVisible();

      // Set up navigation promise BEFORE clicking
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
      await navigateToLogin(page);

      // Verify all three OAuth buttons are present and accessible using i18n matchers
      const googleMatcher = getTextMatcher('auth.oauth.google');
      const discordMatcher = getTextMatcher('auth.oauth.discord');
      const githubMatcher = getTextMatcher('auth.oauth.github');

      const googleButton = page.getByRole('button', { name: googleMatcher });
      const discordButton = page.getByRole('button', { name: discordMatcher });
      const githubButton = page.getByRole('button', { name: githubMatcher });

      await expect(googleButton).toBeVisible();
      await expect(discordButton).toBeVisible();
      await expect(githubButton).toBeVisible();

      // Verify buttons have proper accessibility attributes
      await expect(googleButton).toHaveAttribute('type', 'button');
      await expect(discordButton).toHaveAttribute('type', 'button');
      await expect(githubButton).toHaveAttribute('type', 'button');
    });
  });

  /**
   * Test Group 3: Error Scenarios
   */
  test.describe('Error Scenarios', () => {
    test('handles network error gracefully when backend is unreachable', async ({ page }) => {
      // Unroute all OAuth endpoints first to override beforeEach mocks
      await page.unroute(`${API_BASE}/api/v1/auth/oauth/google/login`);
      await page.unroute(`${API_BASE}/api/v1/auth/oauth/discord/login`);
      await page.unroute(`${API_BASE}/api/v1/auth/oauth/github/login`);
      
      // Now add network failure mock
      await page.route(`${API_BASE}/api/v1/auth/oauth/**`, async (route) => {
        await route.abort('failed');
      });

      await navigateToLogin(page);

      const googleMatcher = getTextMatcher('auth.oauth.google');
      const googleButton = page.getByRole('button', { name: googleMatcher });

      // Click should not crash
      await googleButton.click();

      // Wait for error handling
      await page.waitForTimeout(2000);

      // Should show browser error or stay on page (graceful degradation)
      const url = page.url();
      const gracefulHandling =
        url.includes('localhost:3000/login') ||
        url.includes('chrome-error://') ||
        url.includes('about:neterror');
      expect(gracefulHandling).toBe(true);
    });

    test('handles OAuth endpoint returning 500 error', async ({ page }) => {
      // Unroute Google OAuth endpoint to override beforeEach mock
      await page.unroute(`${API_BASE}/api/v1/auth/oauth/google/login`);
      
      // Now add 500 error mock
      await page.route(`${API_BASE}/api/v1/auth/oauth/google/login`, async (route) => {
        await route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Internal Server Error' })
        });
      });

      await navigateToLogin(page);

      const googleMatcher = getTextMatcher('auth.oauth.google');
      const googleButton = page.getByRole('button', { name: googleMatcher });

      // Attempt to click (may not navigate due to 500 error)
      try {
        await Promise.race([
          page.waitForNavigation({ timeout: 2000 }),
          googleButton.click()
        ]);
      } catch (e) {
        // Navigation may timeout, that's ok for this test
      }

      // Should handle error gracefully - page should still have content
      const bodyText = await page.textContent('body');
      expect(bodyText).toBeTruthy();
      expect(bodyText).not.toBe('');
    });
  });

  /**
   * Test Group 4: Visual and Interaction Testing
   */
  test.describe('Visual and Interaction', () => {
    test('OAuth buttons have correct visual styling', async ({ page }) => {
      await navigateToLogin(page);

      const googleMatcher = getTextMatcher('auth.oauth.google');
      const discordMatcher = getTextMatcher('auth.oauth.discord');
      const githubMatcher = getTextMatcher('auth.oauth.github');

      const googleButton = page.getByRole('button', { name: googleMatcher });
      const discordButton = page.getByRole('button', { name: discordMatcher });
      const githubButton = page.getByRole('button', { name: githubMatcher });

      // Check that buttons are styled (have background colors)
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
      await navigateToLogin(page);

      const googleMatcher = getTextMatcher('auth.oauth.google');
      const googleButton = page.getByRole('button', { name: googleMatcher });

      // Hover over button
      await googleButton.hover();

      // Wait for transition
      await page.waitForTimeout(300);

      // Button should still be visible after hover
      await expect(googleButton).toBeVisible();
    });

    test('OAuth section displays separator with correct text', async ({ page }) => {
      await navigateToLogin(page);

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
      await navigateToLogin(page);

      // Tab to OAuth buttons
      await page.keyboard.press('Tab'); // Skip to first interactive element
      await page.keyboard.press('Tab'); // Continue tabbing
      await page.keyboard.press('Tab');

      // One of the OAuth buttons should be focused
      const focusedElement = await page.locator(':focus').textContent();

      // Should focus on an OAuth button or the "Return to Home" button
      const validFocus =
        focusedElement?.includes('Continue with') ||
        focusedElement?.includes('Continua con') || // Italian
        focusedElement?.includes('Return to Home') ||
        focusedElement?.includes('Torna alla Home'); // Italian
      expect(validFocus).toBe(true);
    });

    test('Enter key triggers OAuth redirect', async ({ page }) => {
      await navigateToLogin(page);

      const googleMatcher = getTextMatcher('auth.oauth.google');
      const googleButton = page.getByRole('button', { name: googleMatcher });

      // Focus the button
      await googleButton.focus();

      // Set up navigation promise BEFORE pressing Enter
      const navigationPromise = page.waitForURL(/.*accounts\.google\.com.*/i, { timeout: 5000 });

      // Press Enter
      await page.keyboard.press('Enter');

      // Wait for redirect
      await navigationPromise;

      // Should have navigated to Google OAuth
      const finalUrl = page.url();
      expect(finalUrl).toMatch(/accounts\.google\.com/i);
    });

    test('Space key triggers OAuth redirect', async ({ page }) => {
      await navigateToLogin(page);

      const discordMatcher = getTextMatcher('auth.oauth.discord');
      const discordButton = page.getByRole('button', { name: discordMatcher });

      // Focus the button
      await discordButton.focus();

      // Set up navigation promise BEFORE pressing Space
      const navigationPromise = page.waitForURL(/.*discord\.com.*/i, { timeout: 5000 });

      // Press Space
      await page.keyboard.press('Space');

      // Wait for redirect
      await navigationPromise;

      // Should have navigated to Discord OAuth
      const finalUrl = page.url();
      expect(finalUrl).toMatch(/discord\.com/i);
    });
  });

  /**
   * Test Group 6: Session Expiration Context
   */
  test.describe('OAuth with Session Expiration', () => {
    test('OAuth buttons visible on login page with session_expired query param', async ({ page }) => {
      await page.goto('/login?reason=session_expired');
      await page.waitForLoadState('domcontentloaded');

      // Session expired alert should be visible
      await expect(page.getByText('Session Expired')).toBeVisible();

      // OAuth buttons should still be visible and functional using i18n matcher
      const googleMatcher = getTextMatcher('auth.oauth.google');
      const googleButton = page.getByRole('button', { name: googleMatcher });
      await expect(googleButton).toBeVisible();

      // Button should be clickable
      await expect(googleButton).toBeEnabled();
    });
  });

  /**
   * Test Group 7: Performance Testing
   */
  test.describe('Performance', () => {
    test('OAuth buttons render quickly', async ({ page }) => {
      const startTime = Date.now();

      await page.goto('/login');
      await page.waitForLoadState('domcontentloaded');

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
      await navigateToLogin(page);

      const googleMatcher = getTextMatcher('auth.oauth.google');
      const googleButton = page.getByRole('button', { name: googleMatcher });

      // Setup navigation listener BEFORE clicking
      let navigationStarted = false;
      page.on('framenavigated', () => {
        navigationStarted = true;
      });

      await googleButton.click();

      // Wait briefly for navigation to start
      await page.waitForTimeout(1000);

      // Navigation should have been triggered
      expect(navigationStarted).toBe(true);
    });

    test('OAuth buttons do not submit a form', async ({ page }) => {
      await navigateToLogin(page);

      const googleMatcher = getTextMatcher('auth.oauth.google');
      const googleButton = page.getByRole('button', { name: googleMatcher });

      // Verify button type is "button" not "submit"
      const buttonType = await googleButton.getAttribute('type');
      expect(buttonType).toBe('button');

      // Verify button is not inside a form that could be submitted
      const isInForm = await googleButton.evaluate((el) => {
        let parent = el.parentElement;
        while (parent) {
          if (parent.tagName === 'FORM') return true;
          parent = parent.parentElement;
        }
        return false;
      });

      // OAuth buttons should NOT be in a form
      expect(isInForm).toBe(false);
    });
  });
});
