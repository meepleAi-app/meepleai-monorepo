/**
 * Authenticated User Journeys - MIGRATED TO PAGE OBJECT MODEL
 *
 * Tests end-to-end user flows for authenticated users across the application.
 *
 * Test Coverage:
 * 1. Login flow from home page
 * 2. Authenticated chat exchange with QA agent
 * 3. PDF upload wizard complete flow
 * 4. Observability dashboard log filtering
 *
 * @see apps/web/e2e/page-objects/ - Page Object Model architecture
 */

import { Buffer } from 'buffer';

import { test, expect } from './fixtures/chromatic';
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
    if (await gameSelector.isVisible({ timeout: 2000 }).catch(() => false)) {
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

  test('walks through the PDF upload wizard for an authenticated user', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    // Use real authentication - middleware validates session server-side
    await authHelper.setupRealSession('admin');

    // Real backend APIs are used:
    // - GET /games - must return seeded test games
    // - POST /games - must create games
    // - GET /games/{id}/pdfs - must return PDF list
    // - POST /ingest/pdf - must process uploads
    // - PUT /games/{id}/rulespec - must save rule specs
    //
    // Backend must be running with test data seeded

    // Navigate and interact with PDF upload wizard using REAL API
    await page.goto('/upload');

    await expect(page.getByRole('heading', { name: /PDF.*Import.*Wizard/i })).toBeVisible({
      timeout: 5000,
    });

    // Step 1: Select game from real DB
    const gameSelector = page.getByLabel(/existing.*games/i);
    await gameSelector.selectOption({ index: 1 }); // First available game from DB

    await page.getByRole('button', { name: /confirm.*selection/i }).click();

    // Step 2: Upload PDF - real backend will process it
    await page.setInputFiles('#fileInput', {
      name: 'test-rules.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('%PDF-1.4 test file'),
    });

    await page.getByRole('button', { name: /upload.*continue/i }).click();

    // Wait for real upload processing (longer timeout for real API)
    await expect(page.locator('text=/PDF uploaded successfully|Document ID:/i')).toBeVisible({
      timeout: 15000,
    });
    await expect(page.getByRole('heading', { name: /Step 2.*Parse/i })).toBeVisible();

    // Step 3: Parse PDF - real backend extraction
    await page.getByRole('button', { name: /parse.*pdf/i }).click();
    await expect(page.getByRole('heading', { name: /Step 3.*Review/i })).toBeVisible({
      timeout: 20000,
    });

    // Step 4: Publish RuleSpec - real DB write
    await page.getByRole('button', { name: /publish.*rulespec/i }).click();

    await expect(page.locator('text=/RuleSpec published successfully/i')).toBeVisible({
      timeout: 10000,
    });
    await expect(page.getByRole('heading', { name: /Step 4.*Published/i })).toBeVisible();
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
