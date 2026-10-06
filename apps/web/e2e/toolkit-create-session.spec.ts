/**
 * Toolkit Create Session E2E Tests (Issue #3163)
 *
 * End-to-end tests for creating a generic toolkit session
 */

import { test, expect } from '@playwright/test';

test.describe('Toolkit - Create Session', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to toolkit landing page
    await page.goto('/toolkit');
  });

  test('should display landing page with create and join options', async ({ page }) => {
    // Verify hero section
    await expect(page.getByRole('heading', { name: /game session toolkit/i })).toBeVisible();

    // Verify create session card
    await expect(page.getByRole('heading', { name: /create new session/i })).toBeVisible();

    // Verify join session card
    await expect(page.getByRole('heading', { name: /join existing session/i })).toBeVisible();
  });

  test('should create a new session and redirect to active session page', async ({ page }) => {
    // Fill in participant names
    const participantInput = page.getByPlaceholder('Participant 1');
    await participantInput.fill('Alice');

    // Add second participant
    await page.getByRole('button', { name: /add participant/i }).click();
    await page.getByPlaceholder('Participant 2').fill('Bob');

    // Submit form
    await page.getByRole('button', { name: /create session/i }).click();

    // Wait for redirect to active session page
    await page.waitForURL(/\/toolkit\/[a-f0-9-]+/);

    // Verify we're on the active session page
    await expect(page.getByText(/ABC|XYZ|[A-Z0-9]{6}/)).toBeVisible(); // Session code
    await expect(page.getByText('Alice')).toBeVisible();
    await expect(page.getByText('Bob')).toBeVisible();
  });

  test('should show validation error if no participants', async ({ page }) => {
    // Clear default participant
    const participantInput = page.getByPlaceholder('Participant 1');
    await participantInput.fill('');

    // Try to submit
    await page.getByRole('button', { name: /create session/i }).click();

    // Verify error toast
    await expect(page.getByText(/please add at least one participant/i)).toBeVisible();
  });

  test('should allow removing participants', async ({ page }) => {
    // Add a second participant
    await page.getByRole('button', { name: /add participant/i }).click();
    await page.getByPlaceholder('Participant 2').fill('Charlie');

    // Verify both participants exist
    await expect(page.getByPlaceholder('Participant 1')).toBeVisible();
    await expect(page.getByPlaceholder('Participant 2')).toBeVisible();

    // Remove second participant (find the × button next to Participant 2)
    const removeButtons = page.getByRole('button', { name: /×/ });
    await removeButtons.last().click();

    // Verify only one participant remains
    await expect(page.getByPlaceholder('Participant 1')).toBeVisible();
    await expect(page.getByPlaceholder('Participant 2')).not.toBeVisible();
  });

  test('should join existing session by code', async ({ page }) => {
    // Enter session code
    const codeInput = page.getByPlaceholder('ABC123');
    await codeInput.fill('TEST01');

    // Submit
    await page.getByRole('button', { name: /join session/i }).click();

    // Wait for redirect
    await page.waitForURL(/\/toolkit\/[a-f0-9-]+/);

    // Verify session page loaded
    await expect(page.getByText('TEST01')).toBeVisible();
  });

  test('should show validation error for invalid session code length', async ({ page }) => {
    // Enter short code
    const codeInput = page.getByPlaceholder('ABC123');
    await codeInput.fill('ABC');

    // Join button should be disabled
    const joinButton = page.getByRole('button', { name: /join session/i });
    await expect(joinButton).toBeDisabled();
  });

  test('should uppercase session codes automatically', async ({ page }) => {
    const codeInput = page.getByPlaceholder('ABC123');
    await codeInput.fill('abc123');

    // Verify input value is uppercased
    await expect(codeInput).toHaveValue('ABC123');
  });

  test('should be responsive on mobile', async ({ page }) => {
    // Set mobile viewport (iPhone SE)
    await page.setViewportSize({ width: 375, height: 667 });

    // Reload page
    await page.goto('/toolkit');

    // Verify layout is mobile-friendly
    await expect(page.getByRole('heading', { name: /game session toolkit/i })).toBeVisible();
    await expect(page.getByRole('heading', { name: /create new session/i })).toBeVisible();
  });

  test('should work in dark mode', async ({ page }) => {
    // Toggle dark mode
    await page.emulateMedia({ colorScheme: 'dark' });

    // Reload page
    await page.goto('/toolkit');

    // Verify page renders
    await expect(page.getByRole('heading', { name: /game session toolkit/i })).toBeVisible();
  });
});
