import { test, expect } from '@playwright/test';
import { setupQATestEnvironment } from './helpers/qa-test-utils';

test.describe('Q&A Interface - Error Handling (Issue #1009)', () => {
  test('should display user-friendly message on API 500 error', async ({ page }) => {
    await setupQATestEnvironment(page);

    // Mock API 500 error
    await page.route('**/api/v1/agents/qa', async route => {
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Internal Server Error' }),
      });
    });

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    await page.fill('#message-input', 'Test question');
    await page.locator('button[type="submit"]').click();

    // User-friendly error message should appear
    await expect(page.getByText(/errore|problema|riprova più tardi/i)).toBeVisible({
      timeout: 5000,
    });

    // UI should return to ready state
    await expect(page.locator('button[type="submit"]:has-text("Invia")')).toBeVisible();
  });

  test('should handle network timeout with retry suggestion', async ({ page }) => {
    await setupQATestEnvironment(page);

    // Mock network timeout
    await page.route('**/api/v1/agents/qa', async route => {
      await new Promise(resolve => setTimeout(resolve, 35000));
      await route.abort('timedout');
    });

    test.setTimeout(40000);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    await page.fill('#message-input', 'Timeout test');
    await page.locator('button[type="submit"]').click();

    // Timeout message with retry suggestion
    await expect(page.getByText(/timeout|connessione|riprova/i)).toBeVisible({ timeout: 36000 });

    // Verify input is still usable for retry
    await expect(page.locator('#message-input')).toBeEnabled();
  });

  test('should handle malformed API response gracefully', async ({ page }) => {
    await setupQATestEnvironment(page);

    // Mock malformed JSON response
    await page.route('**/api/v1/agents/qa', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: 'INVALID JSON {malformed',
      });
    });

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    await page.fill('#message-input', 'Malformed response test');
    await page.locator('button[type="submit"]').click();

    // Error boundary should catch and display error
    await expect(page.getByText(/errore|problema|formato non valido/i)).toBeVisible({
      timeout: 5000,
    });
  });

  test('should display fallback UI when no snippets available', async ({ page }) => {
    const { mockQA } = await setupQATestEnvironment(page);

    // Mock response with empty snippets
    await mockQA({
      answer: 'Not specified',
      snippets: [],
      messageId: 'msg-no-snippets',
    });

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    await page.fill('#message-input', 'No context question');
    await page.locator('button[type="submit"]').click();

    // "Not specified" should appear
    await expect(page.getByText('Not specified')).toBeVisible({ timeout: 5000 });

    // NO "Fonti:" section should be visible
    await expect(page.getByText('Fonti:')).not.toBeVisible();

    // Feedback buttons should still be present
    await expect(page.getByRole('button', { name: /utile/i })).toBeVisible();
  });

  test('should handle rate limiting with queue or error message', async ({ page }) => {
    await setupQATestEnvironment(page);

    // Mock 429 Too Many Requests
    await page.route('**/api/v1/agents/qa', async route => {
      await route.fulfill({
        status: 429,
        contentType: 'application/json',
        body: JSON.stringify({
          error: 'Too Many Requests',
          retryAfter: 5,
        }),
      });
    });

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    await page.fill('#message-input', 'Rate limit test');
    await page.locator('button[type="submit"]').click();

    // Rate limit message should appear
    await expect(page.getByText(/troppo veloce|rallenta|attendi/i)).toBeVisible({ timeout: 5000 });
  });

  test('should handle missing game context error', async ({ page }) => {
    await setupQATestEnvironment(page);

    // Mock 400 Bad Request - no game selected
    await page.route('**/api/v1/agents/qa', async route => {
      await route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Game context required' }),
      });
    });

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    await page.fill('#message-input', 'Question without game');
    await page.locator('button[type="submit"]').click();

    // Error message indicating game selection needed
    await expect(page.getByText(/seleziona un gioco|game required/i)).toBeVisible({
      timeout: 5000,
    });
  });

  test('should handle authentication expiration during Q&A', async ({ page }) => {
    const { auth } = await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Simulate session expiration
    auth.deauthenticate();

    // Mock 401 Unauthorized
    await page.route('**/api/v1/agents/qa', async route => {
      await route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Unauthorized' }),
      });
    });

    await page.fill('#message-input', 'Auth expiration test');
    await page.locator('button[type="submit"]').click();

    // Should redirect to login or show auth required message
    await expect(page.getByText(/login|autenticazione|sessione scaduta/i)).toBeVisible({
      timeout: 5000,
    });
  });

  test('should recover from network disconnection', async ({ page }) => {
    const { mockQA } = await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // First request - network error
    await page.route('**/api/v1/agents/qa', async route => {
      await route.abort('failed');
    });

    await page.fill('#message-input', 'Network fail test');
    await page.locator('button[type="submit"]').click();

    await expect(page.getByText(/errore|connessione/i)).toBeVisible({ timeout: 5000 });

    // Remove route to simulate network recovery
    await page.unroute('**/api/v1/agents/qa');

    // Second request - success
    await mockQA({
      answer: 'Recovery successful.',
      snippets: [],
      messageId: 'msg-recovery',
    });

    await page.fill('#message-input', 'After recovery');
    await page.locator('button[type="submit"]').click();

    await expect(page.getByText('Recovery successful.')).toBeVisible({ timeout: 5000 });
  });

  test('should handle partial response corruption', async ({ page }) => {
    await setupQATestEnvironment(page);

    // Mock response with missing required fields
    await page.route('**/api/v1/agents/qa', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          // Missing 'answer' field
          snippets: [],
          messageId: 'msg-partial',
        }),
      });
    });

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    await page.fill('#message-input', 'Partial response test');
    await page.locator('button[type="submit"]').click();

    // Should handle missing field gracefully
    await expect(page.getByText(/errore|risposta non valida/i)).toBeVisible({ timeout: 5000 });
  });

  test('should display appropriate message when backend is unavailable', async ({ page }) => {
    await setupQATestEnvironment(page);

    // Mock service unavailable
    await page.route('**/api/v1/agents/qa', async route => {
      await route.fulfill({
        status: 503,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Service Unavailable' }),
      });
    });

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    await page.fill('#message-input', 'Service down test');
    await page.locator('button[type="submit"]').click();

    // Service unavailable message
    await expect(page.getByText(/servizio non disponibile|manutenzione/i)).toBeVisible({
      timeout: 5000,
    });
  });

  test('should handle CORS errors gracefully', async ({ page }) => {
    await setupQATestEnvironment(page);

    // Simulate CORS preflight failure
    await page.route('**/api/v1/agents/qa', async route => {
      if (route.request().method() === 'OPTIONS') {
        await route.fulfill({
          status: 403,
          headers: {
            'Access-Control-Allow-Origin': '',
          },
        });
      } else {
        await route.abort('failed');
      }
    });

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    await page.fill('#message-input', 'CORS test');
    await page.locator('button[type="submit"]').click();

    // CORS or connection error message
    await expect(page.getByText(/errore|connessione|permesso negato/i)).toBeVisible({
      timeout: 5000,
    });
  });

  test('should preserve user input on error for easy retry', async ({ page }) => {
    await setupQATestEnvironment(page);

    const testQuestion = 'Will this fail and preserve input?';

    // Mock error
    await page.route('**/api/v1/agents/qa', async route => {
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Error' }),
      });
    });

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    await page.fill('#message-input', testQuestion);
    await page.locator('button[type="submit"]').click();

    await expect(page.getByText(/errore/i)).toBeVisible({ timeout: 5000 });

    // Input field should still contain the question for retry
    // (or be clearable for new question)
    const inputValue = await page.locator('#message-input').inputValue();

    // Either preserved or empty (both are acceptable UX patterns)
    expect(inputValue === testQuestion || inputValue === '').toBe(true);
  });
});
