/**
 * Game FAQ E2E Tests - Issue #2193
 *
 * Tests the complete FAQ lifecycle for games:
 * - Create FAQ for a game
 * - Edit existing FAQ
 * - Delete FAQ
 * - Upvote FAQ
 * - View FAQ list sorted by votes
 *
 * @see apps/api/src/Api/BoundedContexts/GameManagement/Application/Handlers/
 * @see docs/02-development/testing/test-coverage-gaps.md
 */

import { test, expect } from './fixtures/chromatic';
import { AuthHelper, USER_FIXTURES } from './pages';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Mock data for FAQ tests
const MOCK_GAME = {
  id: '550e8400-e29b-41d4-a716-446655440000',
  title: 'Chess',
  description: 'Classic strategy game',
  publisher: 'Various',
  yearPublished: 1475,
};

const MOCK_FAQS = [
  {
    id: '660e8400-e29b-41d4-a716-446655440001',
    gameId: '550e8400-e29b-41d4-a716-446655440000',
    question: 'Come si muove il cavallo?',
    answer: 'Il cavallo si muove a L: due caselle in una direzione e una perpendicolare.',
    upvotes: 15,
    createdAt: '2025-01-01T10:00:00Z',
    updatedAt: null,
  },
  {
    id: '660e8400-e29b-41d4-a716-446655440002',
    gameId: '550e8400-e29b-41d4-a716-446655440000',
    question: "Cos'è l'arrocco?",
    answer: "L'arrocco è una mossa speciale che coinvolge il re e una torre.",
    upvotes: 10,
    createdAt: '2025-01-02T10:00:00Z',
    updatedAt: null,
  },
  {
    id: '660e8400-e29b-41d4-a716-446655440003',
    gameId: '550e8400-e29b-41d4-a716-446655440000',
    question: 'Come si fa scacco matto?',
    answer: 'Lo scacco matto si ottiene quando il re è sotto attacco e non può sfuggire.',
    upvotes: 25,
    createdAt: '2025-01-03T10:00:00Z',
    updatedAt: null,
  },
];

/**
 * SKIPPED: Feature not implemented
 *
 * The game detail page (/games/{id}) currently only has these tabs:
 * - Overview
 * - Rules
 * - Sessions
 * - Notes
 *
 * FAQ functionality is not yet available on the game detail page.
 * These tests are preserved for when the feature is implemented.
 *
 * @see apps/web/src/app/games/[id]/page.tsx
 */
test.describe.skip('Game FAQ Flow - Issue #2193', () => {
  let faqs: typeof MOCK_FAQS;

  test.beforeEach(async ({ page }) => {
    // Reset FAQs for each test
    faqs = [...MOCK_FAQS];

    const authHelper = new AuthHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Mock admin session for CRUD operations
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

    // Mock game details endpoint
    await page.route(`${API_BASE}/api/v1/games/${MOCK_GAME.id}`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_GAME),
      });
    });

    // Mock FAQs list endpoint with sorting
    await page.route(`${API_BASE}/api/v1/games/${MOCK_GAME.id}/faqs*`, async route => {
      if (route.request().method() === 'GET') {
        // Sort by upvotes descending
        const sortedFaqs = [...faqs].sort((a, b) => b.upvotes - a.upvotes);
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: sortedFaqs,
            total: sortedFaqs.length,
          }),
        });
      } else if (route.request().method() === 'POST') {
        // Create new FAQ
        const body = route.request().postDataJSON() as { question: string; answer: string };
        const newFaq = {
          id: `new-faq-${Date.now()}`,
          gameId: MOCK_GAME.id,
          question: body.question,
          answer: body.answer,
          upvotes: 0,
          createdAt: new Date().toISOString(),
          updatedAt: null,
        };
        faqs.push(newFaq);
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify(newFaq),
        });
      } else {
        await route.continue();
      }
    });

    // Mock FAQ update endpoint
    await page.route(new RegExp(`${API_BASE}/api/v1/faqs/[^/]+$`), async route => {
      const url = new URL(route.request().url());
      const faqId = url.pathname.split('/').pop()!;
      const faq = faqs.find(f => f.id === faqId);

      if (!faq) {
        await route.fulfill({ status: 404, body: JSON.stringify({ error: 'FAQ not found' }) });
        return;
      }

      const method = route.request().method();

      if (method === 'PUT') {
        const body = route.request().postDataJSON() as { question: string; answer: string };
        faq.question = body.question;
        faq.answer = body.answer;
        faq.updatedAt = new Date().toISOString();
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(faq),
        });
      } else if (method === 'DELETE') {
        const index = faqs.findIndex(f => f.id === faqId);
        if (index >= 0) {
          faqs.splice(index, 1);
        }
        await route.fulfill({ status: 204 });
      } else {
        await route.continue();
      }
    });

    // Mock FAQ upvote endpoint
    await page.route(new RegExp(`${API_BASE}/api/v1/faqs/[^/]+/upvote$`), async route => {
      const url = new URL(route.request().url());
      const pathParts = url.pathname.split('/');
      const faqId = pathParts[pathParts.length - 2];
      const faq = faqs.find(f => f.id === faqId);

      if (!faq) {
        await route.fulfill({ status: 404, body: JSON.stringify({ error: 'FAQ not found' }) });
        return;
      }

      faq.upvotes += 1;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(faq),
      });
    });
  });

  test('should display FAQ list sorted by votes', async ({ page }) => {
    await page.goto(`/games/${MOCK_GAME.id}`);

    // Wait for FAQs section to load
    await expect(page.getByRole('heading', { name: /FAQ|Domande Frequenti/i })).toBeVisible({
      timeout: 10000,
    });

    // Verify FAQs are displayed
    const faqItems = page.locator('[data-testid="faq-item"], .faq-item');
    await expect(faqItems).toHaveCount(3);

    // Verify sorting by votes (highest first: 25, 15, 10)
    const firstFaq = faqItems.first();
    await expect(firstFaq).toContainText('scacco matto');

    // Verify vote counts are displayed
    await expect(page.getByText('25')).toBeVisible();
    await expect(page.getByText('15')).toBeVisible();
    await expect(page.getByText('10')).toBeVisible();
  });

  test('should create new FAQ for a game', async ({ page }) => {
    await page.goto(`/games/${MOCK_GAME.id}`);

    // Click add FAQ button
    const addButton = page
      .getByRole('button', { name: /Aggiungi FAQ|Nuova Domanda|Add FAQ/i })
      .or(page.locator('[data-testid="add-faq-button"]'));
    await addButton.click();

    // Fill in the FAQ form
    await page.fill(
      'input[name="question"], textarea[name="question"], [data-testid="faq-question-input"]',
      'Come funziona la promozione del pedone?'
    );
    await page.fill(
      'textarea[name="answer"], [data-testid="faq-answer-input"]',
      "Quando un pedone raggiunge l'ultima traversa, può essere promosso a qualsiasi pezzo."
    );

    // Submit
    await page.click('button[type="submit"], button:has-text("Salva"), button:has-text("Crea")');

    // Verify success
    await expect(
      page.locator('text=FAQ creata|FAQ aggiunta|FAQ created', { exact: false })
    ).toBeVisible({ timeout: 5000 });

    // Verify new FAQ appears in list
    await expect(page.getByText('promozione del pedone')).toBeVisible();
  });

  test('should edit existing FAQ', async ({ page }) => {
    await page.goto(`/games/${MOCK_GAME.id}`);

    // Wait for FAQs to load
    await expect(page.getByText('Come si muove il cavallo?')).toBeVisible();

    // Click edit button on first FAQ
    const editButton = page
      .locator('[data-testid="faq-item"], .faq-item')
      .first()
      .getByRole('button', { name: /Edit|Modifica/i })
      .or(page.locator('[data-testid="edit-faq-button"]').first());
    await editButton.click();

    // Update the answer
    const answerInput = page.locator('textarea[name="answer"], [data-testid="faq-answer-input"]');
    await answerInput.clear();
    await answerInput.fill(
      'Lo scacco matto si verifica quando il re nemico è sotto attacco e non ha mosse legali per sfuggire.'
    );

    // Save changes
    await page.click(
      'button[type="submit"], button:has-text("Salva"), button:has-text("Aggiorna")'
    );

    // Verify success message
    await expect(
      page.locator('text=FAQ aggiornata|FAQ modificata|FAQ updated', { exact: false })
    ).toBeVisible({ timeout: 5000 });
  });

  test('should delete FAQ with confirmation', async ({ page }) => {
    // Setup confirm dialog handler
    page.on('dialog', async dialog => {
      expect(dialog.type()).toBe('confirm');
      await dialog.accept();
    });

    await page.goto(`/games/${MOCK_GAME.id}`);

    // Wait for FAQs to load
    await expect(page.getByText('Come si muove il cavallo?')).toBeVisible();

    // Count initial FAQs
    const initialCount = await page.locator('[data-testid="faq-item"], .faq-item').count();
    expect(initialCount).toBe(3);

    // Click delete button on first FAQ
    const deleteButton = page
      .locator('[data-testid="faq-item"], .faq-item')
      .first()
      .getByRole('button', { name: /Delete|Elimina/i })
      .or(page.locator('[data-testid="delete-faq-button"]').first());
    await deleteButton.click();

    // Verify FAQ was removed
    await expect(page.locator('[data-testid="faq-item"], .faq-item')).toHaveCount(initialCount - 1);

    // Verify success message
    await expect(
      page.locator('text=FAQ eliminata|FAQ rimossa|FAQ deleted', { exact: false })
    ).toBeVisible({ timeout: 5000 });
  });

  test('should upvote FAQ and update vote count', async ({ page }) => {
    await page.goto(`/games/${MOCK_GAME.id}`);

    // Wait for FAQs to load
    await expect(page.getByText('Come si muove il cavallo?')).toBeVisible();

    // Find the FAQ with 15 votes and click upvote
    const faqWithVotes = page
      .locator('[data-testid="faq-item"], .faq-item')
      .filter({ hasText: 'cavallo' });
    await expect(faqWithVotes.getByText('15')).toBeVisible();

    const upvoteButton = faqWithVotes
      .getByRole('button', { name: /Upvote|Vota|👍/i })
      .or(faqWithVotes.locator('[data-testid="upvote-button"]'));
    await upvoteButton.click();

    // Verify vote count increased
    await expect(faqWithVotes.getByText('16')).toBeVisible({ timeout: 5000 });
  });

  test('should show empty state when no FAQs exist', async ({ page }) => {
    // Override mock to return empty list
    await page.route(`${API_BASE}/api/v1/games/${MOCK_GAME.id}/faqs*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [],
          total: 0,
        }),
      });
    });

    await page.goto(`/games/${MOCK_GAME.id}`);

    // Verify empty state message
    await expect(
      page.locator('text=Nessuna FAQ|No FAQs|Non ci sono domande|Ancora nessuna domanda', {
        exact: false,
      })
    ).toBeVisible({ timeout: 10000 });
  });

  test('should handle FAQ creation error gracefully', async ({ page }) => {
    // Override mock to return error
    await page.route(`${API_BASE}/api/v1/games/${MOCK_GAME.id}/faqs`, async route => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'La domanda è già presente' }),
        });
      } else {
        await route.continue();
      }
    });

    await page.goto(`/games/${MOCK_GAME.id}`);

    // Try to add FAQ
    const addButton = page
      .getByRole('button', { name: /Aggiungi FAQ|Nuova Domanda|Add FAQ/i })
      .or(page.locator('[data-testid="add-faq-button"]'));
    await addButton.click();

    await page.fill(
      'input[name="question"], textarea[name="question"], [data-testid="faq-question-input"]',
      'Domanda duplicata'
    );
    await page.fill('textarea[name="answer"], [data-testid="faq-answer-input"]', 'Risposta test');
    await page.click('button[type="submit"], button:has-text("Salva"), button:has-text("Crea")');

    // Verify error message is displayed
    await expect(
      page.locator('text=errore|error|già presente|failed', { exact: false })
    ).toBeVisible({ timeout: 5000 });
  });

  test('should require authentication for FAQ management', async ({ page }) => {
    // Clear authentication
    await page.context().clearCookies();

    // Override mock to return 401
    await page.route(`${API_BASE}/api/v1/games/${MOCK_GAME.id}/faqs`, async route => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 401,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Unauthorized' }),
        });
      } else {
        await route.continue();
      }
    });

    await page.goto(`/games/${MOCK_GAME.id}`);

    // Verify add button is not visible for unauthenticated users
    // OR clicking it shows login prompt
    const addButton = page
      .getByRole('button', { name: /Aggiungi FAQ|Nuova Domanda|Add FAQ/i })
      .or(page.locator('[data-testid="add-faq-button"]'));

    const isVisible = await addButton.isVisible().catch(() => false);

    if (isVisible) {
      await addButton.click();
      // Should show login prompt or redirect
      await expect(
        page.locator('text=login|accedi|unauthorized|autenticazione', { exact: false })
      ).toBeVisible({ timeout: 5000 });
    }
  });
});
