import { test, expect } from '@playwright/test';
import { getTextMatcher, t } from './fixtures/i18n';

test.describe('Home Page', () => {
  test('should load home page successfully', async ({ page }) => {
    await page.goto('/');

    // Check main heading - h1 contains "Your AI-Powered" and span contains "Board Game Rules Assistant"
    await expect(page.locator('h1')).toContainText('Your AI-Powered');
    await expect(page.locator('h1 span.gradient-text')).toContainText('Board Game Rules Assistant');

    // Check MeepleAI brand link in header
    await expect(page.getByRole('link', { name: /MeepleAI/i })).toBeVisible();

    // Check Get Started button is visible when not logged in
    await expect(page.getByTestId('nav-get-started')).toBeVisible();
  });

  test('should display registration modal when clicking Get Started', async ({ page }) => {
    await page.goto('/');

    // Click Get Started to open auth modal
    await page.getByTestId('nav-get-started').click();

    // Wait for modal to open
    await expect(page.getByRole('dialog')).toBeVisible();

    // Default is Login tab, so click Register tab to switch
    await page.getByRole('tab', { name: getTextMatcher('home.registerTab') }).click();

    // Check modal has Register tab active after clicking
    await expect(page.getByRole('tab', { name: getTextMatcher('home.registerTab') })).toHaveAttribute('aria-selected', 'true');

    // Check registration form elements in modal
    const modal = page.getByRole('dialog');
    await expect(modal.getByLabel(getTextMatcher('home.registerEmail'))).toBeVisible();
    await expect(modal.getByLabel(getTextMatcher('home.registerPassword'))).toBeVisible();
    await expect(modal.getByLabel(getTextMatcher('home.registerDisplayName'))).toBeVisible();
    await expect(modal.getByLabel(getTextMatcher('home.registerRole'))).toBeVisible();
    await expect(modal.getByRole('button', { name: getTextMatcher('home.registerButton') })).toBeVisible();
  });

  test('should switch to login form in modal', async ({ page }) => {
    await page.goto('/');

    // Click Get Started to open auth modal
    await page.getByTestId('nav-get-started').click();

    // Wait for modal to open
    await expect(page.getByRole('dialog')).toBeVisible();

    // Click Login tab
    await page.getByRole('tab', { name: getTextMatcher('home.loginTab') }).click();

    // Check Login tab is now active
    await expect(page.getByRole('tab', { name: getTextMatcher('home.loginTab') })).toHaveAttribute('aria-selected', 'true');

    // Check login form elements in modal
    const modal = page.getByRole('dialog');
    await expect(modal.getByLabel(getTextMatcher('home.loginEmail'))).toBeVisible();
    await expect(modal.getByLabel(getTextMatcher('home.loginPassword'))).toBeVisible();
    await expect(modal.getByTestId('login-submit-button')).toBeVisible();
  });

  test('should display key features section', async ({ page }) => {
    await page.goto('/');

    // Scroll to features section
    await page.locator('#features').scrollIntoViewIfNeeded();

    // Check "How It Works" heading
    await expect(page.getByRole('heading', { name: 'How It Works' })).toBeVisible();

    // Check for the three main feature cards
    await expect(page.getByText('1. Upload')).toBeVisible();
    await expect(page.getByText('2. Ask')).toBeVisible();
    await expect(page.getByText('3. Play')).toBeVisible();
  });
});