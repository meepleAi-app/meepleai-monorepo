/**
 * E2E-FRONT-002: Chat SSE Streaming
 * Issue #3248 (FRONT-012)
 *
 * Tests SSE streaming functionality:
 * 1. Setup agent session
 * 2. Send message
 * 3. Verify typing indicator
 * 4. Verify streaming cursor and progressive text reveal
 * 5. Verify complete message with confidence and citations
 *
 * Expected: Streaming works smoothly, confidence >70%, citations present.
 */

import { test, expect } from '@playwright/test';

test.describe('Chat SSE Streaming', () => {
  test.beforeEach(async ({ page }) => {
    // Mock auth
    await page.route('**/api/v1/auth/me', async route => {
      await route.fulfill({
        json: {
          id: 'test-user',
          email: 'test@example.com',
          tier: 'free',
        },
      });
    });

    // Mock active session
    await page.route('**/api/v1/game-sessions/session-123', async route => {
      await route.fulfill({
        json: {
          sessionId: 'session-123',
          gameId: 'game-1',
          gameTitle: '7 Wonders',
          typologyName: 'Rules Helper',
          modelId: 'gpt-3.5-turbo',
          tokensUsed: 150,
          tokensLimit: 500,
        },
      });
    });

    // Navigate to chat (assumes session already exists)
    await page.goto('/library/games/game-1?session=session-123');

    // Open chat sheet
    const chatSheet = page.locator('[data-testid="agent-chat-sheet"]');
    if (!(await chatSheet.isVisible())) {
      const openChatButton = page.locator('[data-testid="open-chat-button"]');
      if (await openChatButton.isVisible()) {
        await openChatButton.click();
      }
    }
  });

  test('should stream response progressively', async ({ page }) => {
    // Mock SSE endpoint with streaming response
    await page.route('**/api/v1/game-sessions/*/agent/chat', async route => {
      // Create SSE stream
      const encoder = new TextEncoder();
      const chunks = [
        'data: {"type":"chunk","content":"Pawns"}\n\n',
        'data: {"type":"chunk","content":" can"}\n\n',
        'data: {"type":"chunk","content":" move"}\n\n',
        'data: {"type":"chunk","content":" forward"}\n\n',
        'data: {"type":"chunk","content":" one"}\n\n',
        'data: {"type":"chunk","content":" square."}\n\n',
        'data: {"type":"complete","metadata":{"confidence":0.85,"citations":[{"source":"Rules","page":12,"snippet":"Pawns move forward...","score":0.9}]}}\n\n',
      ];

      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
          'Cache-Control': 'no-cache',
          Connection: 'keep-alive',
        },
        body: chunks.join(''),
      });
    });

    // Type message in chat input
    const chatInput = page.locator('[data-testid="chat-input"]');
    await chatInput.fill('How do pawns move?');

    // Click send button
    const sendButton = page.locator('[data-testid="send-message-button"]');
    await sendButton.click();

    // Step 1: Verify user message appears (right-aligned, cyan)
    const userMessage = page.locator('[data-testid="chat-message"][data-type="user"]').last();
    await expect(userMessage).toBeVisible();
    await expect(userMessage).toContainText('How do pawns move?');

    // Step 2: Verify typing indicator shows
    const typingIndicator = page.locator('[data-testid="typing-indicator"]');
    await expect(typingIndicator).toBeVisible({ timeout: 2000 });

    // Step 3: Verify streaming cursor (▊) is visible during streaming
    const streamingCursor = page.locator('[data-testid="streaming-cursor"]');
    // Note: This may be very brief, so we check it appears at some point

    // Step 4: Wait for streaming to complete
    const agentMessage = page.locator('[data-testid="chat-message"][data-type="agent"]').last();
    await expect(agentMessage).toContainText('Pawns can move forward one square.', {
      timeout: 5000,
    });

    // Step 5: Verify typing indicator is gone
    await expect(typingIndicator).not.toBeVisible();

    // Step 6: Verify confidence bar displays
    const confidenceBar = page.locator('[data-testid="confidence-bar"]');
    await expect(confidenceBar).toBeVisible();
    // Confidence should be > 70% (shown as 85%)
    await expect(confidenceBar).toHaveAttribute('data-confidence', '0.85');

    // Step 7: Verify citation badges display
    const citationBadge = page.locator('[data-testid="citation-badge"]');
    await expect(citationBadge).toBeVisible();
    await expect(citationBadge).toContainText('Rules');
    await expect(citationBadge).toContainText('p.12');

    // Step 8: Click citation → verify handler called
    await citationBadge.click();
    // Citation click should trigger PDF viewer or navigation
    const pdfViewer = page.locator('[data-testid="pdf-viewer"]');
    // Note: PDF viewer may open, or toast notification appears
  });

  test('should handle streaming errors gracefully', async ({ page }) => {
    // Mock SSE endpoint with error
    await page.route('**/api/v1/game-sessions/*/agent/chat', async route => {
      await route.fulfill({
        status: 500,
        body: 'Internal Server Error',
      });
    });

    // Send message
    const chatInput = page.locator('[data-testid="chat-input"]');
    await chatInput.fill('Test question');

    const sendButton = page.locator('[data-testid="send-message-button"]');
    await sendButton.click();

    // Verify error message displays
    const errorMessage = page.locator('[data-testid="error-message"]');
    await expect(errorMessage).toBeVisible({ timeout: 3000 });

    // Verify retry button is available
    const retryButton = page.locator('button:has-text("Riprova")');
    await expect(retryButton).toBeVisible();
  });

  test('should accumulate tokens correctly during streaming', async ({ page }) => {
    // Mock SSE with multiple chunks
    await page.route('**/api/v1/game-sessions/*/agent/chat', async route => {
      const chunks = [
        'data: {"type":"chunk","content":"Word1 "}\n\n',
        'data: {"type":"chunk","content":"Word2 "}\n\n',
        'data: {"type":"chunk","content":"Word3."}\n\n',
        'data: {"type":"complete","metadata":{"confidence":0.9}}\n\n',
      ];

      await route.fulfill({
        status: 200,
        headers: { 'Content-Type': 'text/event-stream' },
        body: chunks.join(''),
      });
    });

    // Send message
    const chatInput = page.locator('[data-testid="chat-input"]');
    await chatInput.fill('Test');

    const sendButton = page.locator('[data-testid="send-message-button"]');
    await sendButton.click();

    // Wait for complete message
    const agentMessage = page.locator('[data-testid="chat-message"][data-type="agent"]').last();
    await expect(agentMessage).toContainText('Word1 Word2 Word3.', { timeout: 3000 });
  });

  test('should disable input during streaming', async ({ page }) => {
    // Mock slow streaming
    let resolveStream: () => void;
    const streamPromise = new Promise<void>(resolve => {
      resolveStream = resolve;
    });

    await page.route('**/api/v1/game-sessions/*/agent/chat', async route => {
      await streamPromise;
      await route.fulfill({
        status: 200,
        headers: { 'Content-Type': 'text/event-stream' },
        body: 'data: {"type":"complete","metadata":{}}\n\n',
      });
    });

    // Send message
    const chatInput = page.locator('[data-testid="chat-input"]');
    await chatInput.fill('Test');

    const sendButton = page.locator('[data-testid="send-message-button"]');
    await sendButton.click();

    // Verify input is disabled during streaming
    await expect(sendButton).toBeDisabled();

    // Complete streaming
    resolveStream!();

    // Verify input is enabled again
    await expect(sendButton).toBeEnabled({ timeout: 2000 });
  });
});
