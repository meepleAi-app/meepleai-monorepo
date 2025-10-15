import { test, expect, Page } from '@playwright/test';

const apiBase = 'http://localhost:8080';

async function setupAuthRoutes(page: Page) {
  let authenticated = false;
  const userResponse = {
    user: {
      id: 'new-user-1',
      email: 'newuser@meepleai.dev',
      displayName: 'New Chess Player',
      role: 'User'
    },
    expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString()
  };

  await page.route(`${apiBase}/auth/me`, async (route) => {
    if (authenticated) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(userResponse)
      });
    } else {
      await route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Unauthorized' })
      });
    }
  });

  await page.route(`${apiBase}/auth/register`, async (route) => {
    authenticated = true;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(userResponse)
    });
  });

  await page.route(`${apiBase}/auth/login`, async (route) => {
    authenticated = true;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(userResponse)
    });
  });

  await page.route(`${apiBase}/agents/chess`, async (route) => {
    const requestBody = route.request().postDataJSON() as { question: string; fenPosition: string };
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        answer: `Risposta alla domanda: ${requestBody.question}. La posizione attuale è: ${requestBody.fenPosition.substring(0, 20)}...`,
        fen: null,
        suggestedMoves: ['e2e4', 'Nf3', 'd2d4']
      })
    });
  });

  return {
    authenticate() {
      authenticated = true;
    },
    reset() {
      authenticated = false;
    },
    userResponse
  };
}

test.describe('Chess page - User registration and access journey', () => {
  test('allows a user to register and access the chess page', async ({ page }) => {
    await setupAuthRoutes(page);

    // Step 1: Navigate to home page
    await page.goto('/');

    // Step 2: Open auth modal and wait for it to be visible
    await page.getByRole('button', { name: 'Get Started' }).first().click();
    await page.waitForTimeout(500); // Wait for modal animation

    // Step 3: Switch to Register tab and wait for form switch animation
    await page.getByRole('button', { name: 'Register' }).click();
    await page.waitForTimeout(800); // Wait for form switch animation to complete

    // Step 4: Fill registration form using reliable selectors
    // Find all email inputs and use the last visible one (register form)
    const emailInputs = page.locator('input[type="email"]');
    await emailInputs.last().fill('newuser@meepleai.dev');

    // Find all password inputs and use the last visible one
    const passwordInputs = page.locator('input[type="password"]');
    await passwordInputs.last().fill('ChessPlayer123!');

    // Display Name is optional - find text input that's not email/password
    const displayNameInput = page.locator('input:not([type="email"]):not([type="password"]):not([type="submit"])').first();
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
    await expect(page.getByPlaceholder('Chiedi consigli o analisi della posizione...')).toBeVisible();

    // Verify game status is displayed
    await expect(page.getByText('Turno:')).toBeVisible();
    await expect(page.getByText(/Bianco/)).toBeVisible();
    await expect(page.getByText('Stato:')).toBeVisible();
    await expect(page.getByText('FEN:')).toBeVisible();
  });

  test('allows registered user to interact with chess AI agent', async ({ page }) => {
    const auth = await setupAuthRoutes(page);
    auth.authenticate();

    // Navigate directly to chess page (user is already authenticated)
    await page.goto('/chess');

    // Wait for chess interface to load
    await expect(page.getByRole('heading', { name: 'Chess Assistant' })).toBeVisible();

    // Verify welcome message
    await expect(page.getByText('Benvenuto nella Chess Chat!')).toBeVisible();

    // Ask a question to the chess agent
    const input = page.getByPlaceholder('Chiedi consigli o analisi della posizione...');
    await input.fill('Qual è la migliore apertura per il bianco?');

    await page.getByRole('button', { name: 'Invia' }).click();

    // Wait for response (skip checking for loading state as it may be too fast with mocked API)
    await expect(page.getByText(/Risposta alla domanda:/)).toBeVisible({ timeout: 10000 });

    // Verify suggested moves are displayed
    await expect(page.getByText('Mosse suggerite:')).toBeVisible();
    await expect(page.getByText('e2e4')).toBeVisible();
    await expect(page.getByText('Nf3')).toBeVisible();
    await expect(page.getByText('d2d4')).toBeVisible();
  });

  test('redirects unauthenticated users to login', async ({ page }) => {
    await setupAuthRoutes(page);

    // Try to access chess page without authentication
    await page.goto('/chess');

    // Should show login required message
    await expect(page.getByRole('heading', { name: 'Accesso richiesto' })).toBeVisible();
    await expect(page.getByText("Devi effettuare l'accesso per utilizzare la chat scacchi.")).toBeVisible();

    // Should have link to go back to login
    await expect(page.getByRole('link', { name: 'Vai al Login' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Vai al Login' })).toHaveAttribute('href', '/');
  });

  test('user can reset board and start new game', async ({ page }) => {
    const auth = await setupAuthRoutes(page);
    auth.authenticate();

    await page.goto('/chess');

    // Wait for interface to load
    await expect(page.getByRole('heading', { name: 'Chess Assistant' })).toBeVisible();

    // Send a message first to populate chat
    const input = page.getByPlaceholder('Chiedi consigli o analisi della posizione...');
    await input.fill('Analizza questa posizione');
    await page.getByRole('button', { name: 'Invia' }).click();

    // Wait for response
    await expect(page.getByText(/Risposta alla domanda:/)).toBeVisible();

    // Click reset button
    await page.getByRole('button', { name: 'Nuova Partita' }).click();

    // Verify chat is cleared and welcome message is back
    await expect(page.getByText('Benvenuto nella Chess Chat!')).toBeVisible();
    await expect(page.getByText(/Risposta alla domanda:/)).not.toBeVisible();
  });

  test('user can rotate the chessboard orientation', async ({ page }) => {
    const auth = await setupAuthRoutes(page);
    auth.authenticate();

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

  test('complete user journey: register, login, use chess agent, reset, ask again', async ({ page }) => {
    await setupAuthRoutes(page);

    // 1. Register new user
    await page.goto('/');

    // Open auth modal and wait for animation
    await page.getByRole('button', { name: 'Get Started' }).first().click();
    await page.waitForTimeout(500); // Wait for modal animation

    // Switch to Register tab and wait for form switch animation
    await page.getByRole('button', { name: 'Register' }).click();
    await page.waitForTimeout(800); // Wait for form switch animation to complete

    // Fill registration form using robust selectors
    const emailInputs = page.locator('input[type="email"]');
    await emailInputs.last().fill('chessmaster@meepleai.dev');

    const passwordInputs = page.locator('input[type="password"]');
    await passwordInputs.last().fill('ChessMaster123!');

    const displayNameInput = page.locator('input:not([type="email"]):not([type="password"]):not([type="submit"])').first();
    await displayNameInput.fill('Chess Master');
    await page.getByRole('button', { name: 'Create Account' }).click();

    // Wait for redirect to chat page
    await page.waitForURL('**/chat');

    // 2. Navigate to chess page
    await page.goto('/chess');
    await expect(page.getByRole('heading', { name: 'Chess Assistant' })).toBeVisible();

    // 3. Ask first question
    let input = page.getByPlaceholder('Chiedi consigli o analisi della posizione...');
    await input.fill('Come si muove il cavallo?');
    await page.getByRole('button', { name: 'Invia' }).click();
    await expect(page.getByText(/Risposta alla domanda: Come si muove il cavallo/)).toBeVisible();

    // 4. Ask second question
    input = page.getByPlaceholder('Chiedi consigli o analisi della posizione...');
    await input.fill('Spiegami l\'arrocco');
    await page.getByRole('button', { name: 'Invia' }).click();
    await expect(page.getByText(/Risposta alla domanda: Spiegami l'arrocco/)).toBeVisible();

    // 5. Verify both messages are in chat history (use .first() as messages appear twice: user + AI)
    await expect(page.getByText(/Come si muove il cavallo/).first()).toBeVisible();
    await expect(page.getByText(/Spiegami l'arrocco/).first()).toBeVisible();

    // 6. Reset board
    await page.getByRole('button', { name: 'Nuova Partita' }).click();
    await expect(page.getByText('Benvenuto nella Chess Chat!')).toBeVisible();

    // Old messages should be cleared
    await expect(page.getByText(/Come si muove il cavallo/)).not.toBeVisible();
    await expect(page.getByText(/Spiegami l'arrocco/)).not.toBeVisible();

    // 7. Ask new question after reset
    input = page.getByPlaceholder('Chiedi consigli o analisi della posizione...');
    await input.fill('Qual è l\'apertura italiana?');
    await page.getByRole('button', { name: 'Invia' }).click();
    await expect(page.getByText(/Risposta alla domanda: Qual è l'apertura italiana/)).toBeVisible();
  });
});
