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

import { test, expect } from '@playwright/test';
import { Buffer } from 'buffer';
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

    // Authenticated session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

    // Mock QA agent endpoint
    await page.route(`${apiBase}/agents/qa`, async route => {
      const requestBody = route.request().postDataJSON() as { query: string };
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          answer: `Risposta per: ${requestBody.query}`,
          sources: [{ title: 'Manuale Demo', snippet: 'Capitolo introduttivo', page: 2 }],
        }),
      });
    });

    await page.goto('/chat');

    await expect(page.getByRole('heading', { name: 'MeepleAI Chat' })).toBeVisible();

    // Send message and verify response
    const input = page.getByPlaceholder('Fai una domanda sul gioco...');
    await input.fill('Qual è la durata media?');
    await page.getByRole('button', { name: 'Invia' }).click();

    await expect(page.getByText('Risposta per: Qual è la durata media?')).toBeVisible();
    await expect(page.getByText('Manuale Demo (Pagina 2)')).toBeVisible();

    // Test feedback button
    await page.getByRole('button', { name: '👍 Utile' }).click();
    await expect(page.getByRole('button', { name: '👍 Utile' })).toHaveCSS(
      'background-color',
      'rgb(52, 168, 83)'
    );
  });

  test('walks through the PDF upload wizard for an authenticated user', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    // Authenticated session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

    // Mock games endpoints
    const games = [
      { id: 'game-1', name: 'Terraforming Mars', createdAt: new Date().toISOString() },
    ];

    await page.route(`${apiBase}/games`, async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(games),
        });
      } else if (route.request().method() === 'POST') {
        const body = route.request().postDataJSON() as { name: string };
        const newGame = { id: 'game-2', name: body.name, createdAt: new Date().toISOString() };
        games.push(newGame);
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(newGame),
        });
      }
    });

    // Mock PDF-related endpoints
    await page.route(`${apiBase}/games/game-1/pdfs`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ pdfs: [] }),
      });
    });

    await page.route(`${apiBase}/ingest/pdf`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ documentId: 'doc-123' }),
      });
    });

    await page.route(`${apiBase}/games/game-1/rulespec`, async route => {
      if (route.request().method() === 'PUT') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: route.request().postData() ?? JSON.stringify({}),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({}),
        });
      }
    });

    // Navigate and interact with PDF upload wizard
    await page.goto('/upload');

    await expect(page.getByRole('heading', { name: 'PDF Import Wizard' })).toBeVisible({
      timeout: 5000,
    });

    // Step 1: Select game
    await page.getByLabel('Existing games').selectOption('game-1');
    await page.getByRole('button', { name: 'Confirm selection' }).click();

    // Step 2: Upload PDF
    await page.setInputFiles('#fileInput', {
      name: 'rules.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('%PDF-1.4 test file'),
    });

    await page.getByRole('button', { name: 'Upload & Continue' }).click();

    await expect(
      page.getByText('✅ PDF uploaded successfully! Document ID: doc-123')
    ).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Step 2: Parse PDF' })).toBeVisible();

    // Step 3: Parse PDF
    await page.getByRole('button', { name: 'Parse PDF' }).click();
    await expect(page.getByRole('heading', { name: 'Step 3: Review & Edit Rules' })).toBeVisible();

    // Step 4: Publish RuleSpec
    await page.getByRole('button', { name: 'Publish RuleSpec' }).click();

    await expect(page.getByText('✅ RuleSpec published successfully!')).toBeVisible();
    await expect(
      page.getByRole('heading', { name: /Step 4: Published Successfully/i })
    ).toBeVisible();
  });

  test('allows filtering logs after navigating as an authenticated user', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    // Mock unauthenticated initially (no setup needed, just navigate)
    await authHelper.mockUnauthenticatedSession();

    await page.goto('/logs');

    await expect(page.getByRole('heading', { name: 'Observability Dashboard' })).toBeVisible();

    // Test log filtering
    const filter = page.getByPlaceholder('Filter logs by message, request ID, or user ID...');
    await filter.fill('req-002');
    await expect(page.getByText('User logged in successfully')).toBeVisible();
    await expect(page.locator('text=Application started')).toHaveCount(0);

    // Test no results
    await filter.fill('no results');
    await expect(
      page.getByText('No logs found. Start using the application to generate logs.')
    ).toBeVisible();
  });
});
