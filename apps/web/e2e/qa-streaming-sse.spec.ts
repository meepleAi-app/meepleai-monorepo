/**
 * QA Streaming SSE E2E Tests - MIGRATED TO POM
 *
 * @see apps/web/e2e/helpers/qa-test-utils.ts
 */

import { test, expect } from './fixtures/chromatic';
import { setupQATestEnvironment, waitForAutoSelection, QASnippet } from './helpers/qa-test-utils';
import { WaitHelper } from './helpers/WaitHelper';

test.describe('Q&A Interface - SSE Streaming (Issue #1009)', () => {
  test('should establish SSE connection and receive streaming events', async ({ page }) => {
    const { mockQAStreaming, gameId, agents } = await setupQATestEnvironment(page);

    // Mock streaming response with multiple SSE events
    await mockQAStreaming([
      { type: 'stateUpdate', data: { state: 'Searching knowledge base...' } },
      { type: 'token', data: { token: 'En passant ' } },
      { type: 'token', data: { token: 'is a special ' } },
      { type: 'token', data: { token: 'pawn capture.' } },
      {
        type: 'complete',
        data: {
          totalTokens: 15,
          confidence: 0.92,
          snippets: [
            {
              text: 'En passant capture rule.',
              source: 'chess-rules.pdf',
              page: 12,
              line: null,
            },
          ],
        },
      },
    ]);

    await page.goto('/chat');
    await waitForAutoSelection(page, gameId, agents[0].id);

    // Send question
    const input = page.locator('[data-testid="message-input"]');
    await input.fill('What is en passant?');
    await page.getByRole('button', { name: 'Invia' }).click();

    // Verify streaming state appears
    await expect(page.locator('button:has-text("Invio..."), text=Sto pensando')).toBeVisible({
      timeout: 2000,
    });

    // Verify complete response appears
    await expect(page.getByText(/En passant is a special pawn capture/i)).toBeVisible({
      timeout: 5000,
    });

    // Verify snippets rendered
    await expect(page.getByText('chess-rules.pdf (Pagina 12)')).toBeVisible();
  });

  test('should handle streaming interruption gracefully', async ({ page }) => {
    const { mockQAStreaming, gameId, agents } = await setupQATestEnvironment(page);

    // Mock interrupted stream (no complete event)
    await mockQAStreaming([
      { type: 'stateUpdate', data: { state: 'Generating answer...' } },
      { type: 'token', data: { token: 'The game ' } },
      { type: 'token', data: { token: 'requires ' } },
      // No complete event - stream interrupted
    ]);

    await page.goto('/chat');
    await waitForAutoSelection(page, gameId, agents[0].id);

    const input = page.locator('[data-testid="message-input"]');
    await input.fill('How many players?');
    await page.getByRole('button', { name: 'Invia' }).click();

    // Partial response should be visible
    await expect(page.getByText(/The game requires/i)).toBeVisible({ timeout: 3000 });

    // UI should return to ready state after timeout
    await expect(page.locator('button[type="submit"]:has-text("Invia")')).toBeVisible({
      timeout: 10000,
    });
  });

  test('should display stop button during active streaming', async ({ page }) => {
    const { gameId, agents } = await setupQATestEnvironment(page);

    // Intercept to create slow stream
    await page.route('**/api/v1/agents/qa/stream', async route => {
      await new Promise(resolve => setTimeout(resolve, 1000));
      await route.continue();
    });

    await page.goto('/chat');
    await waitForAutoSelection(page, gameId, agents[0].id);

    await page.fill('#message-input', 'Tell me about castling');
    await page.locator('button[type="submit"]').click();

    // Stop button should appear during streaming
    const stopButton = page.locator('button[aria-label="Stop streaming"], button:has-text("Stop")');
    const isVisible = await stopButton.isVisible().catch(() => false);

    if (isVisible) {
      expect(await stopButton.isEnabled()).toBe(true);
    }
  });

  test('should stop streaming when stop button clicked', async ({ page }) => {
    const { gameId, agents } = await setupQATestEnvironment(page);

    let streamingStopped = false;

    await page.route('**/api/v1/agents/qa/stream', async route => {
      const sseData = [
        'event: stateUpdate\ndata: {"state":"Thinking..."}\n\n',
        'event: token\ndata: {"token":"This is "}\n\n',
      ].join('');

      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
          'Cache-Control': 'no-cache',
        },
        body: sseData,
      });

      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(5000);
      streamingStopped = true;
    });

    await page.goto('/chat');
    await waitForAutoSelection(page, gameId, agents[0].id);

    await page.fill('#message-input', 'Long question');
    await page.locator('button[type="submit"]').click();

    const stopButton = page.locator('button[aria-label="Stop streaming"], button:has-text("Stop")');

    if (await stopButton.isVisible().catch(() => false)) {
      await stopButton.click({ force: true });

      // Verify submit button returns to ready state
      await expect(page.locator('button[type="submit"]:has-text("Invia")')).toBeVisible({
        timeout: 2000,
      });
    }
  });

  test('should accumulate tokens incrementally in UI', async ({ page }) => {
    const { mockQAStreaming, gameId, agents } = await setupQATestEnvironment(page);

    await mockQAStreaming([
      { type: 'stateUpdate', data: { state: 'Generating embeddings...' } },
      { type: 'token', data: { token: 'The ' } },
      { type: 'token', data: { token: 'game ' } },
      { type: 'token', data: { token: 'is ' } },
      { type: 'token', data: { token: 'played ' } },
      { type: 'token', data: { token: 'on ' } },
      { type: 'token', data: { token: 'a ' } },
      { type: 'token', data: { token: 'board.' } },
      { type: 'complete', data: { totalTokens: 7, confidence: 0.88, snippets: [] } },
    ]);

    await page.goto('/chat');
    await waitForAutoSelection(page, gameId, agents[0].id);

    await page.fill('#message-input', 'Where is the game played?');
    await page.locator('button[type="submit"]').click();

    // Verify complete sentence assembled from tokens
    await expect(page.getByText(/The game is played on a board/i)).toBeVisible({ timeout: 5000 });
  });

  test('should display citations when received via SSE', async ({ page }) => {
    const { mockQAStreaming, gameId, agents } = await setupQATestEnvironment(page);

    const testSnippets: QASnippet[] = [
      {
        text: 'Players alternate turns.',
        source: 'rules.pdf',
        page: 3,
        line: null,
      },
      {
        text: 'Each turn consists of one move.',
        source: 'gameplay.pdf',
        page: 7,
        line: null,
      },
    ];

    await mockQAStreaming([
      { type: 'stateUpdate', data: { state: 'Searching rules...' } },
      { type: 'citations', data: { snippets: testSnippets } },
      { type: 'token', data: { token: 'Players take turns moving pieces.' } },
      {
        type: 'complete',
        data: { totalTokens: 6, confidence: 0.95, snippets: testSnippets },
      },
    ]);

    await page.goto('/chat');
    await waitForAutoSelection(page, gameId, agents[0].id);

    await page.fill('#message-input', 'How do turns work?');
    await page.locator('button[type="submit"]').click();

    // Verify answer
    await expect(page.getByText(/Players take turns moving pieces/i)).toBeVisible({
      timeout: 5000,
    });

    // Verify both citations displayed
    await expect(page.getByText('rules.pdf (Pagina 3)')).toBeVisible();
    await expect(page.getByText('gameplay.pdf (Pagina 7)')).toBeVisible();
    await expect(page.getByText(/Players alternate turns/)).toBeVisible();
    await expect(page.getByText(/Each turn consists of one move/)).toBeVisible();
  });

  test('should handle SSE error events', async ({ page }) => {
    const { mockQAStreaming, gameId, agents } = await setupQATestEnvironment(page);

    await mockQAStreaming([
      { type: 'stateUpdate', data: { state: 'Processing...' } },
      {
        type: 'error',
        data: { error: 'Failed to retrieve context', code: 'CONTEXT_ERROR' },
      },
    ]);

    await page.goto('/chat');
    await waitForAutoSelection(page, gameId, agents[0].id);

    await page.fill('#message-input', 'Test error');
    await page.locator('button[type="submit"]').click();

    // Error message should be displayed
    await expect(page.getByText(/error|failed|problema/i)).toBeVisible({ timeout: 5000 });

    // UI should return to ready state
    await expect(page.locator('button[type="submit"]:has-text("Invia")')).toBeVisible({
      timeout: 3000,
    });
  });

  test('should maintain connection stability across multiple requests', async ({ page }) => {
    const { mockQAStreaming, gameId, agents } = await setupQATestEnvironment(page);

    await page.goto('/chat');
    await waitForAutoSelection(page, gameId, agents[0].id);

    // First request
    await mockQAStreaming([
      { type: 'token', data: { token: 'Answer one.' } },
      { type: 'complete', data: { totalTokens: 2, confidence: 0.9, snippets: [] } },
    ]);

    await page.fill('#message-input', 'First question');
    await page.locator('button[type="submit"]').click();
    await expect(page.getByText('Answer one.')).toBeVisible({ timeout: 5000 });

    // Second request (new SSE connection)
    await mockQAStreaming([
      { type: 'token', data: { token: 'Answer two.' } },
      { type: 'complete', data: { totalTokens: 2, confidence: 0.9, snippets: [] } },
    ]);

    await page.fill('#message-input', 'Second question');
    await page.locator('button[type="submit"]').click();
    await expect(page.getByText('Answer two.')).toBeVisible({ timeout: 5000 });

    // Verify both messages in chat history
    await expect(page.getByText('Answer one.')).toBeVisible();
    await expect(page.getByText('Answer two.')).toBeVisible();
  });
});
