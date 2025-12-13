/**
 * E2E Tests - Citation Display (Issue #859) - MIGRATED TO POM
 *
 * Tests for PDF citation display in chat messages
 *
 * @see apps/web/e2e/pages/ - Page Object Model architecture
 */

import { test, expect } from './fixtures/chromatic';
import { ChatHelper } from './pages';

test.describe('Chat Citations Display (#859)', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to chat page
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
  });

  test('displays citations for assistant messages with citations', async ({ page }) => {
    const chatHelper = new ChatHelper(page);

    // Mock QA stream with citations
    await chatHelper.mockQAStreamWithCitations(
      ['Test ', 'answer ', 'with citations'],
      [
        {
          documentId: 'doc-1',
          pageNumber: 10,
          snippet: 'Citation from page 10',
          relevanceScore: 0.95,
        },
        {
          documentId: 'doc-2',
          pageNumber: 25,
          snippet: 'Citation from page 25',
          relevanceScore: 0.8,
        },
      ]
    );

    // Send a message using data-testid (locale-independent selectors)
    await page.locator('[data-testid="message-input"]').fill('Test question');
    await page.locator('[data-testid="send-message-button"]').click();

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
    const chatHelper = new ChatHelper(page);

    await chatHelper.mockQAStreamWithCitations(
      ['Answer'],
      [
        {
          documentId: 'doc-1',
          pageNumber: 5,
          snippet: 'Test citation',
          relevanceScore: 0.9,
        },
      ]
    );

    await page.locator('[data-testid="message-input"]').fill('Test');
    await page.locator('[data-testid="send-message-button"]').click();

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
    // Send a message using data-testid (locale-independent selectors)
    await page.locator('[data-testid="message-input"]').fill('User message');
    await page.locator('[data-testid="send-message-button"]').click();

    // Wait for user message to appear
    await expect(page.getByText('Tu')).toBeVisible();

    // User message should not have citations section
    const userMessage = page.locator('[aria-label="Your message"]').first();
    await expect(userMessage.getByTestId('message-citations')).not.toBeVisible();
  });

  test('does not show citations when citations array is empty', async ({ page }) => {
    const chatHelper = new ChatHelper(page);

    // Mock stream without citations
    await chatHelper.mockQAStreamWithCitations(['Answer without citations'], []);

    await page.locator('[data-testid="message-input"]').fill('Test');
    await page.locator('[data-testid="send-message-button"]').click();

    // Wait for response
    await expect(page.getByText('MeepleAI')).toBeVisible();

    // Should not show citations section
    await expect(page.getByTestId('message-citations')).not.toBeVisible();
  });
});
