import { test, expect } from '@playwright/test';

/**
 * E2E Tests for Demo User Login Flow
 *
 * Tests verify that all three demo users can successfully log in
 * and access their role-specific features.
 *
 * Demo users (seeded in database):
 * - user@meepleai.dev / Demo123! (Role: User)
 * - editor@meepleai.dev / Demo123! (Role: Editor)
 * - admin@meepleai.dev / Demo123! (Role: Admin)
 *
 * WORKAROUND: These tests use `force: true` on button clicks to bypass
 * a Next.js portal element that intercepts pointer events in the headless
 * Chromium environment. This is a known issue in the test environment and
 * doesn't affect real user interactions.
 */

test.describe('Demo User Login', () => {
  test.beforeEach(async ({ page }) => {
    // Disable animations for stable tests
    await page.emulateMedia({ reducedMotion: 'reduce' });
  });

  /**
   * Test: Regular User can log in and access chat
   */
  test('user@meepleai.dev can log in successfully', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('domcontentloaded');

    // Try using the navigation button instead of the hero button
    const navGetStarted = page.locator('[data-testid="nav-get-started"]');
    if (await navGetStarted.isVisible()) {
      await navGetStarted.click();
    } else {
      // Fallback to hero button with a different approach
      await page.getByTestId('hero-get-started').click({ timeout: 5000 }).catch(async () => {
        // If that fails, try force clicking after a delay
        await page.waitForTimeout(1000);
        await page.getByTestId('hero-get-started').click({ force: true });
      });
    }

    // Wait for and fill the login form
    await expect(page.getByLabel('Email')).toBeVisible({ timeout: 10000 });
    await page.getByLabel('Email').fill('user@meepleai.dev');
    await page.getByLabel('Password').fill('Demo123!');

    // Click login button (force to bypass nextjs-portal)
    await page.locator('form button[type="submit"]:has-text("Login")').click({ force: true });

    // Verify redirect to chat
    await expect(page).toHaveURL('/chat', { timeout: 15000 });
    await expect(page.getByRole('heading', { name: /MeepleAI Chat/i })).toBeVisible();
  });

  /**
   * Test: Editor can log in and access editor features
   */
  test('editor@meepleai.dev can log in and access editor features', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('domcontentloaded');

    // Use nav button approach
    const navGetStarted = page.locator('[data-testid="nav-get-started"]');
    if (await navGetStarted.isVisible()) {
      await navGetStarted.click();
    } else {
      await page.getByTestId('hero-get-started').click({ force: true });
    }

    // Fill login form
    await expect(page.getByLabel('Email')).toBeVisible({ timeout: 10000 });
    await page.getByLabel('Email').fill('editor@meepleai.dev');
    await page.getByLabel('Password').fill('Demo123!');
    await page.locator('form button[type="submit"]:has-text("Login")').click({ force: true });

    // Verify redirect and access to upload (editor feature)
    await expect(page).toHaveURL('/chat', { timeout: 15000 });

    // Navigate to upload page (editor access)
    await page.goto('/upload');
    await expect(page.getByRole('heading', { name: /PDF Import Wizard|Upload/i })).toBeVisible();
  });

  /**
   * Test: Admin can log in and access admin panel
   */
  test('admin@meepleai.dev can log in and access admin panel', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('domcontentloaded');

    // Use nav button approach
    const navGetStarted = page.locator('[data-testid="nav-get-started"]');
    if (await navGetStarted.isVisible()) {
      await navGetStarted.click();
    } else {
      await page.getByTestId('hero-get-started').click({ force: true });
    }

    // Fill login form
    await expect(page.getByLabel('Email')).toBeVisible({ timeout: 10000 });
    await page.getByLabel('Email').fill('admin@meepleai.dev');
    await page.getByLabel('Password').fill('Demo123!');
    await page.locator('form button[type="submit"]:has-text("Login")').click({ force: true });

    // Verify redirect
    await expect(page).toHaveURL('/chat', { timeout: 15000 });

    // Navigate to admin panel (admin-only access)
    await page.goto('/admin');
    await expect(page.getByRole('heading', { name: /Admin|Dashboard/i })).toBeVisible();
  });

  /**
   * Test: Invalid credentials show error message
   */
  test('shows error for invalid credentials', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('domcontentloaded');

    // Open login modal
    const navGetStarted = page.locator('[data-testid="nav-get-started"]');
    if (await navGetStarted.isVisible()) {
      await navGetStarted.click();
    } else {
      await page.getByTestId('hero-get-started').click({ force: true });
    }

    // Try invalid credentials
    await expect(page.getByLabel('Email')).toBeVisible({ timeout: 10000 });
    await page.getByLabel('Email').fill('invalid@example.com');
    await page.getByLabel('Password').fill('wrongpassword');
    await page.locator('form button[type="submit"]:has-text("Login")').click({ force: true });

    // Verify error message
    await expect(page.getByText(/Accesso non riuscito|Invalid credentials|Login failed/i)).toBeVisible({ timeout: 5000 });
  });
});

