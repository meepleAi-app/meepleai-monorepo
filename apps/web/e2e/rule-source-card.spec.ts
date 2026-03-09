/**
 * E2E Tests - RuleSourceCard (Issue #5527)
 *
 * Tests for the collapsible rule source citation card in chat messages.
 * Uses API mocks to inject assistant messages with citations.
 */

import { test, expect } from './fixtures';

test.describe('RuleSourceCard in Chat (#5527)', () => {
  const mockCitations = [
    {
      documentId: 'doc-catan-001',
      pageNumber: 12,
      snippet: 'Quando un giocatore costruisce un insediamento.',
      relevanceScore: 0.92,
    },
    {
      documentId: 'doc-catan-002',
      pageNumber: 23,
      snippet: 'Il ladro viene spostato quando un giocatore tira un 7.',
      relevanceScore: 0.67,
    },
    {
      documentId: 'doc-catan-003',
      pageNumber: 45,
      snippet: 'Per vincere servono 10 punti vittoria.',
      relevanceScore: 0.41,
    },
  ];

  test.beforeEach(async ({ page }) => {
    // Mock thread API to return messages with citations
    await page.route('**/api/v1/chat-threads/*', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'thread-001',
          title: 'Test Chat',
          gameId: 'game-001',
          agentId: null,
          messages: [
            {
              backendMessageId: 'msg-1',
              role: 'user',
              content: 'Come funzionano le risorse?',
              timestamp: new Date().toISOString(),
            },
            {
              backendMessageId: 'msg-2',
              role: 'assistant',
              content: 'Ecco come funzionano le risorse in Catan.',
              timestamp: new Date().toISOString(),
              citations: mockCitations,
            },
          ],
        }),
      });
    });

    // Mock games API
    await page.route('**/api/v1/games*', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          games: [{ id: 'game-001', title: 'Catan' }],
        }),
      });
    });
  });

  test('displays RuleSourceCard collapsed with citation count', async ({ page }) => {
    await page.goto('/chat/thread-001');
    await page.waitForLoadState('networkidle');

    const card = page.getByTestId('rule-source-card');
    await expect(card).toBeVisible();

    const header = page.getByTestId('rule-source-header');
    await expect(header).toContainText('3 fonti dal regolamento');
    await expect(header).toHaveAttribute('aria-expanded', 'false');
  });

  test('expands card on click and shows quote and action buttons', async ({ page }) => {
    await page.goto('/chat/thread-001');
    await page.waitForLoadState('networkidle');

    await page.getByTestId('rule-source-header').click();
    await expect(page.getByTestId('citation-quote')).toBeVisible();
    await expect(page.getByTestId('view-pdf-btn')).toBeVisible();
  });

  test('clicking different chips changes the displayed quote', async ({ page }) => {
    await page.goto('/chat/thread-001');
    await page.waitForLoadState('networkidle');

    await page.getByTestId('rule-source-header').click();
    await expect(page.getByTestId('citation-chips')).toBeVisible();

    // Click second chip
    const tabs = page.getByRole('tab');
    await tabs.nth(1).click();

    await expect(page.getByTestId('citation-quote')).toContainText('Il ladro viene spostato');
  });

  test('Vedi nel PDF button opens PDF modal', async ({ page }) => {
    await page.goto('/chat/thread-001');
    await page.waitForLoadState('networkidle');

    await page.getByTestId('rule-source-header').click();
    await page.getByTestId('view-pdf-btn').click();

    // PdfPageModal uses a Dialog — look for the dialog
    await expect(page.getByRole('dialog')).toBeVisible();
  });

  test('message without citations does NOT show RuleSourceCard', async ({ page }) => {
    // Override route with message that has no citations
    await page.route('**/api/v1/chat-threads/*', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'thread-002',
          title: 'No Citation Chat',
          gameId: null,
          agentId: null,
          messages: [
            {
              backendMessageId: 'msg-1',
              role: 'user',
              content: 'Ciao',
              timestamp: new Date().toISOString(),
            },
            {
              backendMessageId: 'msg-2',
              role: 'assistant',
              content: 'Ciao! Come posso aiutarti?',
              timestamp: new Date().toISOString(),
            },
          ],
        }),
      });
    });

    await page.goto('/chat/thread-002');
    await page.waitForLoadState('networkidle');

    await expect(page.getByTestId('rule-source-card')).not.toBeVisible();
  });
});
