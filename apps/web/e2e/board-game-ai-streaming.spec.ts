/**
 * E2E-FRONT-014: Board Game AI Ask Page - SSE Streaming
 * Issue #3373
 *
 * Tests streaming SSE integration in /board-game-ai/ask page:
 * 1. Load Ask page
 * 2. Select game and ask question
 * 3. Verify typing indicator appears
 * 4. Verify progressive token-by-token response
 * 5. Verify complete message with citations
 * 6. Verify state messages (Searching, Generating, Complete)
 *
 * Expected: SSE streaming works, progressive text appears, citations visible
 */

import { test, expect } from '@playwright/test';

test.describe('Board Game AI Ask - SSE Streaming (Issue #3373)', () => {
  test.beforeEach(async ({ page }) => {
    // Mock games API
    await page.route('**/api/v1/games**', async route => {
      await route.fulfill({
        json: [
          {
            id: 'chess-123',
            title: 'Chess',
            description: 'Classic strategy board game',
            publisher: null,
            yearPublished: null,
            minPlayers: 2,
            maxPlayers: 2,
            minPlayTimeMinutes: null,
            maxPlayTimeMinutes: null,
            bggId: null,
            createdAt: '2024-01-01T00:00:00Z',
          },
        ],
      });
    });

    // Navigate to Ask page
    await page.goto('/board-game-ai/ask');

    // Wait for games to load
    await page.waitForSelector('text=Select a game', { timeout: 5000 });
  });

  test('should display streaming response progressively', async ({ page }) => {
    // Mock SSE streaming endpoint
    let streamController: ReadableStreamDefaultController;

    await page.route('**/api/v1/agents/qa/stream', async route => {
      const stream = new ReadableStream({
        start(controller) {
          streamController = controller;

          // Send state update
          controller.enqueue(
            new TextEncoder().encode(
              'event: state_update\ndata: {"state":"Searching vector database..."}\n\n'
            )
          );

          // Send citations
          setTimeout(() => {
            controller.enqueue(
              new TextEncoder().encode(
                'event: citations\ndata: {"citations":[{"source":"Chess Rules Official","documentId":"chess-doc-1","pageNumber":5,"snippet":"Knights move in an L-shape","relevanceScore":0.95}]}\n\n'
              )
            );
          }, 100);

          // Stream tokens progressively
          const tokens = ['In ', 'chess, ', 'knights ', 'move ', 'in ', 'an ', 'L-shape...'];
          tokens.forEach((token, index) => {
            setTimeout(() => {
              controller.enqueue(
                new TextEncoder().encode(`event: token\ndata: {"token":"${token}"}\n\n`)
              );
            }, 200 + index * 50);
          });

          // Complete
          setTimeout(() => {
            controller.enqueue(
              new TextEncoder().encode(
                'event: complete\ndata: {"totalTokens":45,"confidence":0.92,"estimatedReadingTimeMinutes":1}\n\n'
              )
            );
            controller.close();
          }, 200 + tokens.length * 50 + 100);
        },
      });

      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
          'Cache-Control': 'no-cache',
          Connection: 'keep-alive',
        },
        body: stream,
      });
    });

    // Select game
    await page.click('[id="game-select"]');
    await page.click('text=Chess');

    // Enter question
    await page.fill('[id="question-input"]', 'How does the knight move in chess?');

    // Click Ask button
    await page.click('text=🚀 Ask Question');

    // Verify state message appears
    await expect(page.locator('text=Searching vector database...')).toBeVisible({
      timeout: 2000,
    });

    // Verify typing indicator appears
    await expect(page.locator('text=typing...')).toBeVisible({ timeout: 3000 });

    // Verify streaming response card appears
    const streamingCard = page.locator('text=Streaming Response').locator('..');
    await expect(streamingCard).toBeVisible({ timeout: 1000 });

    // Verify progressive text accumulation (check for partial content)
    await expect(page.locator('text=/In chess/')).toBeVisible({ timeout: 1500 });

    // Wait for complete message
    await expect(page.locator('text=/L-shape/')).toBeVisible({ timeout: 5000 });

    // Verify typing indicator disappears when complete
    await expect(page.locator('text=typing...')).not.toBeVisible({ timeout: 6000 });

    // Verify message moved to conversation history
    await expect(page.locator('text=Conversation')).toBeVisible({ timeout: 1000 });

    // Verify citations appeared
    await expect(page.locator('text=📖 Sources:')).toBeVisible();
    await expect(page.locator('text=Chess Rules Official')).toBeVisible();
    await expect(page.locator('text=Knights move in an L-shape')).toBeVisible();
  });

  test('should show state updates during streaming', async ({ page }) => {
    await page.route('**/api/v1/agents/qa/stream', async route => {
      const stream = new ReadableStream({
        start(controller) {
          // State: Searching
          controller.enqueue(
            new TextEncoder().encode(
              'event: state_update\ndata: {"state":"Searching vector database..."}\n\n'
            )
          );

          setTimeout(() => {
            // State: Generating
            controller.enqueue(
              new TextEncoder().encode(
                'event: state_update\ndata: {"state":"Generating response..."}\n\n'
              )
            );
          }, 200);

          setTimeout(() => {
            // Token
            controller.enqueue(
              new TextEncoder().encode('event: token\ndata: {"token":"Test response"}\n\n')
            );
          }, 400);

          setTimeout(() => {
            // Complete
            controller.enqueue(
              new TextEncoder().encode(
                'event: complete\ndata: {"totalTokens":10,"confidence":0.85}\n\n'
              )
            );
            controller.close();
          }, 600);
        },
      });

      await route.fulfill({
        status: 200,
        headers: { 'Content-Type': 'text/event-stream' },
        body: stream,
      });
    });

    await page.click('[id="game-select"]');
    await page.click('text=Chess');
    await page.fill('[id="question-input"]', 'Test question');
    await page.click('text=🚀 Ask Question');

    // Verify state progression
    await expect(page.locator('text=Searching vector database...')).toBeVisible({
      timeout: 1000,
    });
    await expect(page.locator('text=Generating response...')).toBeVisible({ timeout: 1500 });
  });

  test('should handle streaming errors gracefully', async ({ page }) => {
    // Mock error response
    await page.route('**/api/v1/agents/qa/stream', async route => {
      const stream = new ReadableStream({
        start(controller) {
          controller.enqueue(
            new TextEncoder().encode(
              'event: error\ndata: {"code":"STREAM_ERROR","message":"Failed to process request"}\n\n'
            )
          );
          controller.close();
        },
      });

      await route.fulfill({
        status: 200,
        headers: { 'Content-Type': 'text/event-stream' },
        body: stream,
      });
    });

    await page.click('[id="game-select"]');
    await page.click('text=Chess');
    await page.fill('[id="question-input"]', 'Test question');
    await page.click('text=🚀 Ask Question');

    // Verify error message appears
    await expect(page.locator('text=Failed to process request')).toBeVisible({ timeout: 2000 });

    // Verify streaming stopped (no typing indicator)
    await expect(page.locator('text=typing...')).not.toBeVisible({ timeout: 500 });
  });

  test('should clear input after successful stream', async ({ page }) => {
    await page.route('**/api/v1/agents/qa/stream', async route => {
      const stream = new ReadableStream({
        start(controller) {
          controller.enqueue(new TextEncoder().encode('event: token\ndata: {"token":"Answer"}\n\n'));
          setTimeout(() => {
            controller.enqueue(
              new TextEncoder().encode('event: complete\ndata: {"totalTokens":5}\n\n')
            );
            controller.close();
          }, 100);
        },
      });

      await route.fulfill({
        status: 200,
        headers: { 'Content-Type': 'text/event-stream' },
        body: stream,
      });
    });

    await page.click('[id="game-select"]');
    await page.click('text=Chess');

    const input = page.locator('[id="question-input"]');
    await input.fill('How do knights move?');

    // Verify input has value
    await expect(input).toHaveValue('How do knights move?');

    await page.click('text=🚀 Ask Question');

    // Wait for completion
    await page.waitForSelector('text=Conversation', { timeout: 2000 });

    // Verify input is cleared
    await expect(input).toHaveValue('');
  });

  test('should disable controls during streaming', async ({ page }) => {
    await page.route('**/api/v1/agents/qa/stream', async route => {
      const stream = new ReadableStream({
        start(controller) {
          // Long stream to test disabled state
          setTimeout(() => {
            controller.enqueue(new TextEncoder().encode('event: token\ndata: {"token":"Test"}\n\n'));
          }, 500);
          setTimeout(() => {
            controller.enqueue(new TextEncoder().encode('event: complete\ndata: {}\n\n'));
            controller.close();
          }, 1500);
        },
      });

      await route.fulfill({
        status: 200,
        headers: { 'Content-Type': 'text/event-stream' },
        body: stream,
      });
    });

    await page.click('[id="game-select"]');
    await page.click('text=Chess');
    await page.fill('[id="question-input"]', 'Test');
    await page.click('text=🚀 Ask Question');

    // Verify controls are disabled during streaming
    await expect(page.locator('[id="game-select"]')).toBeDisabled();
    await expect(page.locator('[id="question-input"]')).toBeDisabled();
    await expect(page.locator('text=Thinking...')).toBeVisible();

    // Wait for completion
    await page.waitForSelector('text=🚀 Ask Question', { timeout: 3000 });

    // Verify controls are re-enabled
    await expect(page.locator('[id="game-select"]')).not.toBeDisabled();
    await expect(page.locator('[id="question-input"]')).not.toBeDisabled();
  });
});
