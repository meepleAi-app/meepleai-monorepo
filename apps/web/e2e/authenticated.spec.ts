import { test, expect, Page, Route } from '@playwright/test';
import { Buffer } from 'buffer';

const apiBase = 'http://localhost:8080';

async function setupAuthRoutes(page: Page) {
  let authenticated = false;
  const userResponse = {
    user: {
      id: 'admin-1',
      email: 'admin@example.com',
      displayName: 'Admin Example',
      role: 'Admin'
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

  const handleAuthSuccess = async (route: Route) => {
    authenticated = true;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(userResponse)
    });
  };

  await page.route(`${apiBase}/auth/login`, handleAuthSuccess);
  await page.route(`${apiBase}/auth/register`, handleAuthSuccess);
  await page.route(`${apiBase}/auth/logout`, async (route) => {
    authenticated = false;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ success: true })
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

test.describe('Authenticated journeys', () => {
  test('allows a user to log in from the home page', async ({ page }) => {
    await setupAuthRoutes(page);

    await page.goto('/');

    const loginForm = page
      .locator('form')
      .filter({ has: page.getByRole('heading', { name: 'Accesso' }) });

    await loginForm.getByLabel('Email').fill('admin@example.com');
    await loginForm.getByLabel('Password').fill('supersecure');
    await loginForm.getByRole('button', { name: 'Entra' }).click();

    await expect(page.getByText('Accesso eseguito.')).toBeVisible();
    await expect(page.getByText(/Email:\s*admin@example.com/)).toBeVisible();
    await expect(page.getByText(/Ruolo:\s*Admin/)).toBeVisible();

  });

  test('supports an authenticated chat exchange', async ({ page }) => {
    const auth = await setupAuthRoutes(page);
    auth.authenticate();

    await page.route(`${apiBase}/agents/qa`, async (route) => {
      const requestBody = route.request().postDataJSON() as { query: string };
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          answer: `Risposta per: ${requestBody.query}`,
          sources: [
            { title: 'Manuale Demo', snippet: 'Capitolo introduttivo', page: 2 }
          ]
        })
      });
    });

    await page.goto('/chat');

    await expect(page.getByRole('heading', { name: 'MeepleAI Chat' })).toBeVisible();

    const input = page.getByPlaceholder('Fai una domanda sul gioco...');
    await input.fill('Qual è la durata media?');
    await page.getByRole('button', { name: 'Invia' }).click();

    await expect(page.getByText('Risposta per: Qual è la durata media?')).toBeVisible();
    await expect(page.getByText('Manuale Demo (Pagina 2)')).toBeVisible();
    await page.getByRole('button', { name: '👍 Utile' }).click();
    await expect(page.getByRole('button', { name: '👍 Utile' })).toHaveCSS('background-color', 'rgb(52, 168, 83)');
  });

  test('walks through the PDF upload wizard for an authenticated user', async ({ page }) => {
    const auth = await setupAuthRoutes(page);
    auth.authenticate();

    const games = [
      { id: 'game-1', name: 'Terraforming Mars', createdAt: new Date().toISOString() }
    ];

    await page.route(`${apiBase}/games`, async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(games)
        });
      } else if (route.request().method() === 'POST') {
        const body = route.request().postDataJSON() as { name: string };
        const newGame = { id: 'game-2', name: body.name, createdAt: new Date().toISOString() };
        games.push(newGame);
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(newGame)
        });
      }
    });

    await page.route(`${apiBase}/games/game-1/pdfs`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ pdfs: [] })
      });
    });

    await page.route(`${apiBase}/ingest/pdf`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ documentId: 'doc-123' })
      });
    });

    await page.route(`${apiBase}/games/game-1/rulespec`, async (route) => {
      if (route.request().method() === 'PUT') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: route.request().postData() ?? JSON.stringify({})
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({})
        });
      }
    });

    await page.goto('/upload');

    await expect(page.getByRole('heading', { name: 'PDF Import Wizard' })).toBeVisible({ timeout: 5000 });
    await page.getByLabel('Existing games').selectOption('game-1');
    await page.getByRole('button', { name: 'Confirm selection' }).click();

    await page.setInputFiles('#fileInput', {
      name: 'rules.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('%PDF-1.4 test file')
    });

    await page.getByRole('button', { name: 'Upload & Continue' }).click();

    await expect(page.getByText('✅ PDF uploaded successfully! Document ID: doc-123')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Step 2: Parse PDF' })).toBeVisible();

    await page.getByRole('button', { name: 'Parse PDF' }).click();
    await expect(page.getByRole('heading', { name: 'Step 3: Review & Edit Rules' })).toBeVisible();

    await page.getByRole('button', { name: 'Publish RuleSpec' }).click();

    await expect(page.getByText('✅ RuleSpec published successfully!')).toBeVisible();
    await expect(page.getByRole('heading', { name: /Step 4: Published Successfully/i })).toBeVisible();
  });

  test('allows filtering logs after navigating as an authenticated user', async ({ page }) => {
    await setupAuthRoutes(page);

    await page.goto('/logs');

    await expect(page.getByRole('heading', { name: 'Observability Dashboard' })).toBeVisible();

    const filter = page.getByPlaceholder('Filter logs by message, request ID, or user ID...');
    await filter.fill('req-002');
    await expect(page.getByText('User logged in successfully')).toBeVisible();
    await expect(page.locator('text=Application started')).toHaveCount(0);

    await filter.fill('no results');
    await expect(page.getByText('No logs found. Start using the application to generate logs.')).toBeVisible();
  });
});
