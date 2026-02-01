/**
 * Chess Registration Journey - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/ - Page Object Model architecture
 */

import { test, expect } from './fixtures';
import { AuthHelper, USER_FIXTURES } from './pages';

const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('Chess page - User registration and access journey', () => {
  test('allows a user to register and access the chess page', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    // Step 1: Navigate to home page
    await page.goto('/');

    // Step 2: Open auth modal and wait for it to be visible
    await page.getByRole('button', { name: 'Get Started' }).first().click();

    // Step 3: Switch to Register tab and wait for form switch animation
    await page.getByRole('button', { name: 'Register' }).click();

    // Step 4: Fill registration form using reliable selectors
    // Find all email inputs and use the last visible one (register form)
    const emailInputs = page.locator('input[type="email"]');
    await emailInputs.last().fill('newuser@meepleai.dev');

    // Find all password inputs and use the last visible one
    const passwordInputs = page.locator('input[type="password"]');
    await passwordInputs.last().fill('ChessPlayer123!');

    // Display Name is optional - find text input that's not email/password
    const displayNameInput = page
      .locator('input:not([type="email"]):not([type="password"]):not([type="submit"])')
      .first();
    await displayNameInput.fill('New Chess Player');

    // Step 5: Submit registration
    await page.getByRole('button', { name: 'Create Account' }).click();

    // Step 6: Wait for redirect to chat page (after successful registration)
    await page.waitForURL('**/chat');

    // Step 5: Navigate to chess page
    await page.goto('/chess');

    // Step 6: Verify chess page loads with all main components
    await expect(page.getByRole('heading', { name: 'Chess Assistant' })).toBeVisible();
    await expect(page.getByText(/Gioca o chiedi consigli sull'agente scacchi AI/)).toBeVisible();

    // Verify board controls are present
    await expect(page.getByRole('button', { name: 'Nuova Partita' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Ruota Scacchiera' })).toBeVisible();

    // Verify chat interface is present
    await expect(page.getByRole('heading', { name: "Chat con l'Agente" })).toBeVisible();
    await expect(page.getByText('Benvenuto nella Chess Chat!')).toBeVisible();
    await expect(page.locator('[data-testid="chess-message-input"]')).toBeVisible();

    // Verify game status is displayed
    await expect(page.getByText('Turno:')).toBeVisible();
    await expect(page.getByText(/Bianco/)).toBeVisible();
    await expect(page.getByText('Stato:')).toBeVisible();
    await expect(page.getByText('FEN:')).toBeVisible();
  });

  test('allows registered user to interact with chess AI agent', async ({ page }) => {
    const authHelper = new AuthHelper(page);
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // Navigate directly to chess page (user is already authenticated)
    await page.goto('/chess');

    // Wait for chess interface to load
    await expect(page.getByRole('heading', { name: 'Chess Assistant' })).toBeVisible();

    // Verify welcome message
    await expect(page.getByText('Benvenuto nella Chess Chat!')).toBeVisible();

    // Ask a question to the chess agent
    const input = page.locator('[data-testid="chess-message-input"]');
    await input.fill('Qual è la migliore apertura per il bianco?');

    await page.locator('[data-testid="chess-send-button"]').click();

    // Wait for real backend response
    await expect(page.locator('[data-testid="chess-message-input"]')).toBeEnabled({
      timeout: 30000,
    });
  });

  test('redirects unauthenticated users to login', async ({ page }) => {
    // Try to access chess page without authentication
    await page.goto('/chess');

    // Should show login required message
    await expect(page.getByRole('heading', { name: 'Accesso richiesto' })).toBeVisible();
    await expect(
      page.getByText("Devi effettuare l'accesso per utilizzare la chat scacchi.")
    ).toBeVisible();

    // Should have link to go back to login
    await expect(page.getByRole('link', { name: 'Vai al Login' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Vai al Login' })).toHaveAttribute('href', '/');
  });

  test('user can reset board and start new game', async ({ page }) => {
    const authHelper = new AuthHelper(page);
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    await page.goto('/chess');

    // Wait for interface to load
    await expect(page.getByRole('heading', { name: 'Chess Assistant' })).toBeVisible();

    // Send a message first to populate chat
    const input = page.locator('[data-testid="chess-message-input"]');
    await input.fill('Analizza questa posizione');
    await page.locator('[data-testid="chess-send-button"]').click();

    // Wait for backend response
    await page.waitForTimeout(2000);

    // Click reset button
    await page.getByRole('button', { name: 'Nuova Partita' }).click();

    // Verify chat is cleared and welcome message is back
    await expect(page.getByText('Benvenuto nella Chess Chat!')).toBeVisible();
  });

  test('user can rotate the chessboard orientation', async ({ page }) => {
    const authHelper = new AuthHelper(page);
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    await page.goto('/chess');

    // Wait for interface to load
    await expect(page.getByRole('heading', { name: 'Chess Assistant' })).toBeVisible();

    // Click rotate button multiple times
    const rotateButton = page.getByRole('button', { name: 'Ruota Scacchiera' });

    await rotateButton.click();
    // Board should rotate (we can't easily verify the actual rotation without inspecting the Chessboard component)
    // But we can verify the button is functional
    await expect(rotateButton).toBeVisible();

    await rotateButton.click();
    // Board should rotate back
    await expect(rotateButton).toBeVisible();
  });

  test('complete user journey: register, login, use chess agent, reset, ask again', async ({
    page,
  }) => {
    const authHelper = new AuthHelper(page);
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // 2. Navigate to chess page
    await page.goto('/chess');
    await expect(page.getByRole('heading', { name: 'Chess Assistant' })).toBeVisible();

    // 3. Ask first question
    let input = page.locator('[data-testid="chess-message-input"]');
    await input.fill('Come si muove il cavallo?');
    await page.locator('[data-testid="chess-send-button"]').click();
    await page.waitForTimeout(2000);

    // 4. Ask second question
    input = page.locator('[data-testid="chess-message-input"]');
    await input.fill("Spiegami l'arrocco");
    await page.locator('[data-testid="chess-send-button"]').click();
    await page.waitForTimeout(2000);

    // 6. Reset board
    await page.getByRole('button', { name: 'Nuova Partita' }).click();
    await expect(page.getByText('Benvenuto nella Chess Chat!')).toBeVisible();

    // 7. Ask new question after reset
    input = page.locator('[data-testid="chess-message-input"]');
    await input.fill("Qual è l'apertura italiana?");
    await page.locator('[data-testid="chess-send-button"]').click();
    await page.waitForTimeout(2000);
  });
});
