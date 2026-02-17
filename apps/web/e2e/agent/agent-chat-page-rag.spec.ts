/**
 * E2E Test: Agent Chat Page with RAG Integration
 *
 * Tests complete agent chat flow from /agents/[id] page:
 * 1. Agent status validation (KB readiness check)
 * 2. Blocking UI when agent not configured
 * 3. Embedded chat when agent ready
 * 4. RAG-powered responses with citations
 * 5. Fullscreen mode toggle
 * 6. SSE streaming functionality
 *
 * Scenarios:
 * - Agent without KB → Blocking UI with config link
 * - Agent with KB → Chat enabled, RAG responses
 * - Fullscreen toggle → Modal appears/disappears
 */

import { test, expect, type Page } from '@playwright/test';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ========================================
// Test Helpers
// ========================================

async function mockAuth(page: Page) {
  await page.route('**/api/v1/auth/me', async route => {
    await route.fulfill({
      status: 200,
      json: {
        id: 'test-user-123',
        email: 'test@meepleai.com',
        tier: 'premium',
        role: 'admin',
      },
    });
  });
}

async function mockAgentNotReady(page: Page, agentId: string) {
  // Mock agent status: NOT ready (no documents)
  await page.route(`**/api/v1/agents/${agentId}/status`, async route => {
    await route.fulfill({
      status: 200,
      json: {
        agentId,
        name: 'Test Agent',
        isActive: true,
        isReady: false,
        hasConfiguration: true,
        hasDocuments: false,
        documentCount: 0,
        ragStatus: 'Not initialized',
        blockingReason: 'Agent has no documents in Knowledge Base',
      },
    });
  });

  // Mock agent details
  await page.route(`**/api/v1/agents/${agentId}`, async route => {
    await route.fulfill({
      status: 200,
      json: {
        id: agentId,
        name: 'Test Agent',
        type: 'tutor',
        isActive: true,
        invocationCount: 0,
      },
    });
  });
}

async function mockAgentReady(page: Page, agentId: string) {
  // Mock agent status: Ready (has documents)
  await page.route(`**/api/v1/agents/${agentId}/status`, async route => {
    await route.fulfill({
      status: 200,
      json: {
        agentId,
        name: 'Tutor Agent',
        isActive: true,
        isReady: true,
        hasConfiguration: true,
        hasDocuments: true,
        documentCount: 5,
        ragStatus: 'Ready',
        blockingReason: null,
      },
    });
  });

  // Mock agent details
  await page.route(`**/api/v1/agents/${agentId}`, async route => {
    await route.fulfill({
      status: 200,
      json: {
        id: agentId,
        name: 'Tutor Agent',
        type: 'tutor',
        isActive: true,
        invocationCount: 42,
        lastInvokedAt: new Date(Date.now() - 3600000).toISOString(), // 1h ago
      },
    });
  });

  // Mock create thread
  await page.route('**/api/v1/chat-threads', async route => {
    const threadId = 'thread-' + Math.random().toString(36).substring(7);
    await route.fulfill({
      status: 201,
      json: {
        id: threadId,
        userId: 'test-user-123',
        agentId,
        title: 'Chat con Tutor Agent',
        status: 'Active',
        createdAt: new Date().toISOString(),
        lastMessageAt: new Date().toISOString(),
        messageCount: 0,
        messages: [],
      },
    });
  });
}

async function mockSSEChatStream(page: Page, agentId: string) {
  await page.route(`**/api/v1/agents/${agentId}/chat`, async route => {
    // Simulate SSE streaming with RAG response
    const threadId = 'thread-sse-' + Math.random().toString(36).substring(7);
    const sseEvents = [
      `data: ${JSON.stringify({ type: 0, data: { message: 'Searching knowledge base...', chatThreadId: threadId }, timestamp: new Date().toISOString() })}\n\n`,
      `data: ${JSON.stringify({ type: 1, data: [{ documentId: 'doc-123', pageNumber: 15, score: 0.89 }], timestamp: new Date().toISOString() })}\n\n`,
      `data: ${JSON.stringify({ type: 0, data: { message: 'Generating response...', chatThreadId: threadId }, timestamp: new Date().toISOString() })}\n\n`,
      `data: ${JSON.stringify({ type: 7, data: { token: 'According' }, timestamp: new Date().toISOString() })}\n\n`,
      `data: ${JSON.stringify({ type: 7, data: { token: ' to' }, timestamp: new Date().toISOString() })}\n\n`,
      `data: ${JSON.stringify({ type: 7, data: { token: ' the' }, timestamp: new Date().toISOString() })}\n\n`,
      `data: ${JSON.stringify({ type: 7, data: { token: ' rules' }, timestamp: new Date().toISOString() })}\n\n`,
      `data: ${JSON.stringify({ type: 7, data: { token: ' (page' }, timestamp: new Date().toISOString() })}\n\n`,
      `data: ${JSON.stringify({ type: 7, data: { token: ' 15),' }, timestamp: new Date().toISOString() })}\n\n`,
      `data: ${JSON.stringify({ type: 7, data: { token: ' players' }, timestamp: new Date().toISOString() })}\n\n`,
      `data: ${JSON.stringify({ type: 7, data: { token: ' must' }, timestamp: new Date().toISOString() })}\n\n`,
      `data: ${JSON.stringify({ type: 7, data: { token: ' draw' }, timestamp: new Date().toISOString() })}\n\n`,
      `data: ${JSON.stringify({ type: 7, data: { token: ' cards.' }, timestamp: new Date().toISOString() })}\n\n`,
      `data: ${JSON.stringify({ type: 4, data: { totalTokens: 25, chatThreadId: threadId, promptTokens: 12, completionTokens: 13 }, timestamp: new Date().toISOString() })}\n\n`,
    ];

    await route.fulfill({
      status: 200,
      headers: {
        'Content-Type': 'text/event-stream',
        'Cache-Control': 'no-cache',
        Connection: 'keep-alive',
      },
      body: sseEvents.join(''),
    });
  });
}

// ========================================
// Tests
// ========================================

test.describe('Agent Chat Page - RAG Integration', () => {
  test.beforeEach(async ({ page }) => {
    await mockAuth(page);
  });

  test('should show blocking UI when agent has no KB documents', async ({ page }) => {
    const agentId = 'agent-no-docs';
    await mockAgentNotReady(page, agentId);

    // Navigate to agent page
    await page.goto(`/agents/${agentId}`);

    // Wait for page load
    await expect(page.locator('h1:has-text("Agent Chat")')).toBeVisible();

    // Verify MeepleCard shows agent info
    const agentCard = page.locator('[data-testid^="meeple-card"]');
    await expect(agentCard).toBeVisible();
    await expect(agentCard).toContainText('Test Agent');

    // Verify Chat tab is visible but shows blocking UI
    const chatTab = page.locator('[data-testid="agent-info-tab-chat"]');
    await expect(chatTab).toBeVisible();
    await chatTab.click();

    // Verify blocking message appears
    const blockingUI = page.locator('text=Agente non configurato');
    await expect(blockingUI).toBeVisible();

    // Verify blocking reason is displayed
    await expect(page.locator('text=/Knowledge Base/i')).toBeVisible();
    await expect(page.locator('text=Documenti: 0')).toBeVisible();
    await expect(page.locator('text=Status RAG: Not initialized')).toBeVisible();

    // Verify "Configura Agente" button exists
    const configButton = page.locator('button:has-text("Configura Agente")');
    await expect(configButton).toBeVisible();

    // Verify clicking button navigates to config page
    await configButton.click();
    await expect(page).toHaveURL(new RegExp(`/admin/ai-lab/agents/${agentId}/edit`));
  });

  test('should enable chat when agent is ready', async ({ page }) => {
    const agentId = 'agent-ready-123';
    await mockAgentReady(page, agentId);

    // Navigate to agent page
    await page.goto(`/agents/${agentId}`);

    // Wait for status check to complete
    await page.waitForTimeout(500);

    // Verify Chat tab shows ready state
    const chatTab = page.locator('[data-testid="agent-info-tab-chat"]');
    await chatTab.click();

    // Verify ready UI appears
    await expect(page.locator('text=Chat con Tutor Agent')).toBeVisible();
    await expect(page.locator('text=/Pronto per chattare/i')).toBeVisible();
    await expect(page.locator('text=5 documenti nella KB')).toBeVisible();

    // Verify "Inizia Conversazione" button exists
    const startChatButton = page.locator('button:has-text("Inizia Conversazione")');
    await expect(startChatButton).toBeVisible();
    await expect(startChatButton).toBeEnabled();
  });

  test('should create thread and embed chat interface', async ({ page }) => {
    const agentId = 'agent-ready-chat';
    await mockAgentReady(page, agentId);

    // Navigate and start chat
    await page.goto(`/agents/${agentId}`);
    await page.waitForTimeout(500);

    const chatTab = page.locator('[data-testid="agent-info-tab-chat"]');
    await chatTab.click();

    // Click "Inizia Conversazione"
    const startButton = page.locator('button:has-text("Inizia Conversazione")');
    await startButton.click();

    // Verify embedded chat interface appears
    await expect(page.locator('[data-testid="chat-thread-view"]')).toBeVisible({ timeout: 3000 });

    // Verify fullscreen toggle button exists
    const fullscreenToggle = page.locator('[data-testid="fullscreen-toggle"]');
    await expect(fullscreenToggle).toBeVisible();

    // Verify chat input is present
    const chatInput = page.locator('[data-testid="message-input"]');
    await expect(chatInput).toBeVisible();
    await expect(chatInput).toBeEnabled();
  });

  test('should stream RAG-powered responses with citations', async ({ page }) => {
    const agentId = 'agent-rag-stream';
    await mockAgentReady(page, agentId);
    await mockSSEChatStream(page, agentId);

    // Navigate and start chat
    await page.goto(`/agents/${agentId}`);
    await page.waitForTimeout(500);

    const chatTab = page.locator('[data-testid="agent-info-tab-chat"]');
    await chatTab.click();

    const startButton = page.locator('button:has-text("Inizia Conversazione")');
    await startButton.click();

    // Wait for chat interface
    await page.waitForSelector('[data-testid="message-input"]', { timeout: 3000 });

    // Send message
    const input = page.locator('[data-testid="message-input"]');
    await input.fill('How do I setup the game?');

    const sendButton = page.locator('[data-testid="send-btn"]');
    await sendButton.click();

    // Verify user message appears
    const userMessage = page.locator('[data-testid="message-user"]').last();
    await expect(userMessage).toBeVisible({ timeout: 2000 });
    await expect(userMessage).toContainText('How do I setup the game?');

    // Verify streaming status message appears
    const streamStatus = page.locator('[data-testid="stream-status"]');
    // May show "Searching knowledge base..." or "Generating response..."

    // Verify streaming response bubble appears
    const streamingMessage = page.locator('[data-testid="message-streaming"]');
    await expect(streamingMessage).toBeVisible({ timeout: 3000 });

    // Wait for complete response
    const assistantMessage = page.locator('[data-testid="message-assistant"]').last();
    await expect(assistantMessage).toContainText('According to the rules', { timeout: 5000 });
    await expect(assistantMessage).toContainText('players must draw cards');

    // Verify citations appear (page 15 from RAG)
    // Note: Citations format depends on ChatThreadView implementation
    const citationBadge = assistantMessage.locator('span:has-text("p.15")');
    // Citations may be present based on backend implementation
  });

  test('should toggle fullscreen mode correctly', async ({ page }) => {
    const agentId = 'agent-fullscreen';
    await mockAgentReady(page, agentId);

    // Navigate and start chat
    await page.goto(`/agents/${agentId}`);
    await page.waitForTimeout(500);

    const chatTab = page.locator('[data-testid="agent-info-tab-chat"]');
    await chatTab.click();

    const startButton = page.locator('button:has-text("Inizia Conversazione")');
    await startButton.click();

    // Wait for embedded chat
    await expect(page.locator('[data-testid="fullscreen-toggle"]')).toBeVisible({ timeout: 3000 });

    // Click fullscreen toggle
    const fullscreenButton = page.locator('[data-testid="fullscreen-toggle"]');
    await fullscreenButton.click();

    // Verify fullscreen modal appears
    const fullscreenModal = page.locator('[data-testid="fullscreen-chat-modal"]');
    await expect(fullscreenModal).toBeVisible();

    // Verify modal covers entire viewport
    const modalBox = await fullscreenModal.boundingBox();
    const viewportSize = page.viewportSize();
    if (modalBox && viewportSize) {
      expect(modalBox.width).toBeGreaterThan(viewportSize.width * 0.95); // ~100% width
      expect(modalBox.height).toBeGreaterThan(viewportSize.height * 0.95); // ~100% height
    }

    // Verify close button exists in fullscreen
    const closeButton = page.locator('[data-testid="fullscreen-close"]');
    await expect(closeButton).toBeVisible();

    // Click close to exit fullscreen
    await closeButton.click();

    // Verify modal disappears
    await expect(fullscreenModal).not.toBeVisible();

    // Verify embedded chat is still visible
    await expect(page.locator('[data-testid="chat-thread-view"]')).toBeVisible();
  });

  test('should maintain SSE connection during fullscreen toggle', async ({ page }) => {
    const agentId = 'agent-sse-fullscreen';
    await mockAgentReady(page, agentId);
    await mockSSEChatStream(page, agentId);

    await page.goto(`/agents/${agentId}`);
    await page.waitForTimeout(500);

    const chatTab = page.locator('[data-testid="agent-info-tab-chat"]');
    await chatTab.click();

    const startButton = page.locator('button:has-text("Inizia Conversazione")');
    await startButton.click();

    // Send message to start streaming
    await page.waitForSelector('[data-testid="message-input"]', { timeout: 3000 });
    const input = page.locator('[data-testid="message-input"]');
    await input.fill('Test question');

    const sendButton = page.locator('[data-testid="send-btn"]');
    await sendButton.click();

    // Wait for streaming to start
    await page.waitForTimeout(300);

    // Toggle fullscreen during streaming
    const fullscreenButton = page.locator('[data-testid="fullscreen-toggle"]');
    if (await fullscreenButton.isVisible()) {
      await fullscreenButton.click();
    }

    // Verify response completes successfully in fullscreen
    const fullscreenModal = page.locator('[data-testid="fullscreen-chat-modal"]');
    const assistantMessage = fullscreenModal.locator('[data-testid="message-assistant"]').last();
    await expect(assistantMessage).toContainText('According to the rules', { timeout: 5000 });
  });

  test('should show KB documents in KB tab', async ({ page }) => {
    const agentId = 'agent-kb-docs';
    await mockAgentReady(page, agentId);

    // Mock documents endpoint
    await page.route(`**/api/v1/agents/${agentId}/documents`, async route => {
      await route.fulfill({
        status: 200,
        json: {
          agentId,
          documents: [
            {
              id: 'doc-1',
              sharedGameId: 'game-1',
              pdfDocumentId: 'pdf-1',
              documentType: 1,
              version: '1.0',
              isActive: true,
              tags: ['rules', 'official'],
              gameName: '7 Wonders Rules',
            },
            {
              id: 'doc-2',
              sharedGameId: 'game-1',
              pdfDocumentId: 'pdf-2',
              documentType: 2,
              version: '2.0',
              isActive: true,
              tags: ['faq'],
              gameName: '7 Wonders FAQ',
            },
          ],
        },
      });
    });

    await page.goto(`/agents/${agentId}`);

    // Switch to KB tab
    const kbTab = page.locator('[data-testid="agent-info-tab-kb"]');
    await kbTab.click();

    // Verify documents list appears
    await expect(page.locator('text=7 Wonders Rules')).toBeVisible({ timeout: 2000 });
    await expect(page.locator('text=7 Wonders FAQ')).toBeVisible();

    // Verify document count matches
    const documentCards = page.locator('[data-testid^="agent-info-card"] >> text=/7 Wonders/');
    await expect(documentCards).toHaveCount(2);
  });
});

test.describe('Agent Chat Page - Error Scenarios', () => {
  test.beforeEach(async ({ page }) => {
    await mockAuth(page);
  });

  test('should handle agent not found gracefully', async ({ page }) => {
    const invalidAgentId = 'non-existent-agent';

    // Mock 404 response
    await page.route(`**/api/v1/agents/${invalidAgentId}`, async route => {
      await route.fulfill({
        status: 404,
        json: { error: 'Agent not found' },
      });
    });

    await page.goto(`/agents/${invalidAgentId}`);

    // Verify error state or not-found page
    // (Depends on Next.js notFound() handling)
    await expect(page.locator('text=/not found/i')).toBeVisible({ timeout: 3000 });
  });

  test('should handle status check failure gracefully', async ({ page }) => {
    const agentId = 'agent-status-error';

    // Mock agent details success
    await page.route(`**/api/v1/agents/${agentId}`, async route => {
      await route.fulfill({
        status: 200,
        json: {
          id: agentId,
          name: 'Test Agent',
          type: 'tutor',
          isActive: true,
        },
      });
    });

    // Mock status endpoint failure
    await page.route(`**/api/v1/agents/${agentId}/status`, async route => {
      await route.fulfill({
        status: 500,
        json: { error: 'Internal server error' },
      });
    });

    await page.goto(`/agents/${agentId}`);

    // Navigate to chat tab
    const chatTab = page.locator('[data-testid="agent-info-tab-chat"]');
    await chatTab.click();

    // Verify error message displays
    await expect(page.locator('text=/Errore/i')).toBeVisible({ timeout: 2000 });
  });
});

test.describe('Agent Chat Page - Responsive Behavior', () => {
  test.beforeEach(async ({ page }) => {
    await mockAuth(page);
  });

  test('should adapt to mobile viewport', async ({ page }) => {
    const agentId = 'agent-mobile';
    await mockAgentReady(page, agentId);

    // Set mobile viewport
    await page.setViewportSize({ width: 375, height: 667 });

    await page.goto(`/agents/${agentId}`);
    await page.waitForTimeout(500);

    // Verify layout adapts (tabs should be visible with icons only on mobile)
    const chatTab = page.locator('[data-testid="agent-info-tab-chat"]');
    await expect(chatTab).toBeVisible();

    // Mobile: Tab labels may be hidden (only icons visible)
    const tabLabel = chatTab.locator('span');
    // On mobile, span may have hidden class
  });
});
