import { test, expect } from '@playwright/test';
import { getTextMatcher, t } from './fixtures/i18n';

test.describe('Home Page', () => {
  test('should load home page successfully', async ({ page }) => {
    await page.goto('/');

    // Check main heading - English text "Your AI-Powered Board Game Rules Assistant"
    await expect(page.getByRole('heading', { level: 1 })).toContainText('AI-Powered');
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Board Game Rules');

    // Check MeepleAI brand link in header
    await expect(page.getByRole('link', { name: /MeepleAI/i })).toBeVisible();

    // Check Get Started button is visible when not logged in (using testId for stability)
    await expect(page.getByTestId('nav-get-started')).toBeVisible();
  });

  test('should display registration modal when clicking Get Started', async ({ page }) => {
    await page.goto('/');

    // Click Get Started to open auth modal (using testId for stability)
    await page.getByTestId('nav-get-started').click();

    // Wait for modal to open and tabs to be visible
    const modal = page.getByRole('dialog');
    await expect(modal).toBeVisible();

    // Wait for tablist to be rendered and accessible
    await expect(page.getByRole('tablist')).toBeVisible();
    await expect(page.getByRole('tab', { name: getTextMatcher('home.registerTab') })).toBeVisible();

    // Default is Login tab, so click Register tab to switch
    await page.getByRole('tab', { name: getTextMatcher('home.registerTab') }).click();

    // Check modal has Register tab active after clicking
    await expect(page.getByRole('tab', { name: getTextMatcher('home.registerTab') })).toHaveAttribute('aria-selected', 'true');

    // Check registration form elements in modal
    await expect(modal.getByLabel(getTextMatcher('home.registerEmail'))).toBeVisible();
    await expect(modal.getByLabel(getTextMatcher('home.registerPassword'))).toBeVisible();
    await expect(modal.getByLabel(getTextMatcher('home.registerDisplayName'))).toBeVisible();
    await expect(modal.getByLabel(getTextMatcher('auth.confirmPassword'))).toBeVisible();
    await expect(modal.getByRole('button', { name: getTextMatcher('home.registerButton') })).toBeVisible();
  });

  test('should switch to login form in modal', async ({ page }) => {
    await page.goto('/');

    // Click Get Started to open auth modal (using testId for stability)
    await page.getByTestId('nav-get-started').click();

    // Wait for modal and tabs to be visible
    const modal = page.getByRole('dialog');
    await expect(modal).toBeVisible();

    // Wait for tablist to be rendered
    await expect(page.getByRole('tablist')).toBeVisible();
    await expect(page.getByRole('tab', { name: getTextMatcher('home.loginTab') })).toBeVisible();

    // Login tab should be default selected
    await expect(page.getByRole('tab', { name: getTextMatcher('home.loginTab') })).toHaveAttribute('aria-selected', 'true');

    // Check login form elements in modal
    await expect(modal.getByLabel(getTextMatcher('home.loginEmail'))).toBeVisible();
    await expect(modal.getByLabel(getTextMatcher('home.loginPassword'))).toBeVisible();
    await expect(modal.getByRole('button', { name: getTextMatcher('home.loginButton') })).toBeVisible();
  });

  test('should display key features section', async ({ page }) => {
    await page.goto('/');

    // Check "Come Funziona" (How It Works) heading is visible
    await expect(page.getByRole('heading', { name: 'Come Funziona' })).toBeVisible();

    // Check for the three main feature cards (using heading text without numbers)
    await expect(page.getByRole('heading', { name: '1. Carica' })).toBeVisible();
    await expect(page.getByRole('heading', { name: '2. Chiedi' })).toBeVisible();
    await expect(page.getByRole('heading', { name: '3. Gioca' })).toBeVisible();
  });
});