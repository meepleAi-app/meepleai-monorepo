/**
 * Authenticated User Journeys - MIGRATED TO PAGE OBJECT MODEL
 *
 * Tests end-to-end user flows for authenticated users across the application.
 *
 * Test Coverage:
 * 1. Login flow from home page
 * 2. Authenticated chat exchange with QA agent
 * 3. Observability dashboard log filtering
 *
 * @see apps/web/e2e/page-objects/ - Page Object Model architecture
 */

import { test, expect } from './fixtures';
import { AuthHelper, USER_FIXTURES } from './pages';

const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('Authenticated journeys', () => {
  test('allows a user to log in from the home page', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    // Start unauthenticated, then mock successful login
    await authHelper.mockUnauthenticatedSession();
    await authHelper.mockLoginEndpoint(true, USER_FIXTURES.admin);

    await page.goto('/');

    // Login form interaction
    const loginForm = page
      .locator('form')
      .filter({ has: page.getByRole('heading', { name: 'Accesso' }) });

    await loginForm.getByLabel('Email').fill('admin@example.com');
    await loginForm.getByLabel('Password').fill('supersecure');
    await loginForm.getByRole('button', { name: 'Entra' }).click();

    // Verify login success
    await expect(page.getByText('Accesso eseguito.')).toBeVisible();
    await expect(page.getByText(/Email:\s*admin@example.com/)).toBeVisible();
    await expect(page.getByText(/Ruolo:\s*Admin/)).toBeVisible();
  });

  test('supports an authenticated chat exchange', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    // Use real authentication - middleware validates session server-side
    await authHelper.setupRealSession('admin');

    // Real backend must be running with test game data seeded

    await page.goto('/chat');

    await expect(page.getByRole('heading', { name: 'MeepleAI Chat' })).toBeVisible();

    // Send message and verify response using real API
    const input = page.locator('[data-testid="message-input"]');
    await input.fill('What are the basic rules?');

    // Wait for game selector if needed
    const gameSelector = page.getByRole('combobox', { name: /select.*game/i });
    if (await gameSelector.isVisible({ timeout: 2000 })) {
      await gameSelector.click();
      const firstGame = page.getByRole('option').first();
      await firstGame.click();
    }

    await page.getByRole('button', { name: /send|invia/i }).click();

    // Wait for real AI response (may take longer than mock)
    await expect(page.locator('[data-testid="ai-message"]').first()).toBeVisible({
      timeout: 30000,
    });

    // Test feedback button
    const thumbsUp = page.getByRole('button', { name: /thumbs up|utile/i }).first();
    await thumbsUp.click();
  });

  test('allows filtering logs after navigating as an authenticated user', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    // Mock unauthenticated initially (no setup needed, just navigate)
    await authHelper.mockUnauthenticatedSession();

    await page.goto('/logs');

    await expect(page.getByRole('heading', { name: /observability.*dashboard/i })).toBeVisible();

    // Test log filtering - use real logs from backend
    const filter = page.getByPlaceholder(/filter.*logs/i);

    // Try filtering - exact content depends on real backend logs
    await filter.fill('req');

    // Verify filter is working (some results should appear)
    const logEntries = page.locator('[data-testid="log-entry"], .log-item, tbody tr');
    await expect(logEntries.first()).toBeVisible({ timeout: 5000 });

    // Test no results with unlikely search term
    await filter.fill('zzz-nonexistent-xyz-123');
    await expect(page.locator('text=/no.*logs.*found|nessun.*log/i')).toBeVisible({
      timeout: 5000,
    });
  });
});
