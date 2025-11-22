/**
 * E2E Tests - Citation Display (Issue #859)
 *
 * Tests for PDF citation display in chat messages
 */

import { test, expect } from '@playwright/test';

test.describe('Chat Citations Display (#859)', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to chat page (assumes logged in via global setup)
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
  });

  test('displays citations for assistant messages with citations', async ({ page }) => {
    // Mock API response with citations
    await page.route('**/api/v1/agents/qa/stream', async (route) => {
      const response = [
        'event: token\ndata: {"token":"Test "}\n\n',
        'event: token\ndata: {"token":"answer "}\n\n',
        'event: token\ndata: {"token":"with citations"}\n\n',
        'event: citations\ndata: {"citations":[{"documentId":"doc-1","pageNumber":10,"snippet":"Citation from page 10","relevanceScore":0.95},{"documentId":"doc-2","pageNumber":25,"snippet":"Citation from page 25","relevanceScore":0.80}]}\n\n',
        'event: complete\ndata: {"totalTokens":100,"confidence":0.9}\n\n',
      ].join('');

      await route.fulfill({
        status: 200,
        headers: { 'Content-Type': 'text/event-stream' },
        body: response,
      });
    });

    // Send a message
    await page.getByRole('textbox', { name: /fai una domanda/i }).fill('Test question');
    await page.getByRole('button', { name: /invia/i }).click();

    // Wait for assistant response
    await expect(page.getByText('MeepleAI')).toBeVisible();

    // Check citations are displayed
    await expect(page.getByTestId('message-citations')).toBeVisible();
    await expect(page.getByTestId('citation-list')).toBeVisible();

    // Check citation count in header
    await expect(page.getByText('📚 Fonti (2)')).toBeVisible();

    // Check individual citations
    const citations = page.getByTestId('citation-card');
    await expect(citations).toHaveCount(2);

    // Check first citation
    await expect(citations.first()).toContainText('Pag. 10');
    await expect(citations.first()).toContainText('Citation from page 10');

    // Check second citation
    await expect(citations.nth(1)).toContainText('Pag. 25');
    await expect(citations.nth(1)).toContainText('Citation from page 25');
  });

  test('allows toggling citation visibility when collapsible', async ({ page }) => {
    // Similar setup as above
    await page.route('**/api/v1/agents/qa/stream', async (route) => {
      const response = [
        'event: token\ndata: {"token":"Answer"}\n\n',
        'event: citations\ndata: {"citations":[{"documentId":"doc-1","pageNumber":5,"snippet":"Test citation","relevanceScore":0.9}]}\n\n',
        'event: complete\ndata: {"totalTokens":50}\n\n',
      ].join('');

      await route.fulfill({
        status: 200,
        headers: { 'Content-Type': 'text/event-stream' },
        body: response,
      });
    });

    await page.getByRole('textbox', { name: /fai una domanda/i }).fill('Test');
    await page.getByRole('button', { name: /invia/i }).click();

    // Wait for citations
    await expect(page.getByTestId('citation-list')).toBeVisible();

    // Initially expanded
    await expect(page.getByTestId('citations-content')).toBeVisible();

    // Click header to collapse
    await page.getByTestId('citations-header').click();
    await expect(page.getByTestId('citations-content')).not.toBeVisible();

    // Click again to expand
    await page.getByTestId('citations-header').click();
    await expect(page.getByTestId('citations-content')).toBeVisible();
  });

  test('does not show citations for user messages', async ({ page }) => {
    // Send a message
    await page.getByRole('textbox', { name: /fai una domanda/i }).fill('User message');
    await page.getByRole('button', { name: /invia/i }).click();

    // Wait for user message to appear
    await expect(page.getByText('Tu')).toBeVisible();

    // User message should not have citations section
    const userMessage = page.locator('[aria-label="Your message"]').first();
    await expect(userMessage.getByTestId('message-citations')).not.toBeVisible();
  });

  test('does not show citations when citations array is empty', async ({ page }) => {
    await page.route('**/api/v1/agents/qa/stream', async (route) => {
      const response = [
        'event: token\ndata: {"token":"Answer without citations"}\n\n',
        'event: complete\ndata: {"totalTokens":20}\n\n',
      ].join('');

      await route.fulfill({
        status: 200,
        headers: { 'Content-Type': 'text/event-stream' },
        body: response,
      });
    });

    await page.getByRole('textbox', { name: /fai una domanda/i }).fill('Test');
    await page.getByRole('button', { name: /invia/i }).click();

    // Wait for response
    await expect(page.getByText('MeepleAI')).toBeVisible();

    // Should not show citations section
    await expect(page.getByTestId('message-citations')).not.toBeVisible();
  });
});
