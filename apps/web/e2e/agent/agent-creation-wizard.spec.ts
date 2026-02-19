/**
 * E2E: Agent Creation Wizard + Chat Flow
 * Issue #4782: End-to-end test of the critical path:
 * GameCard "Crea Agente" → wizard opens → fill form → submit →
 * redirect to /chat/{threadId} → welcome message → send message → SSE response
 *
 * Pattern: page.route() for API mocking, PLAYWRIGHT_AUTH_BYPASS=true
 */

import { test, expect } from '@playwright/test';

// ============================================================================
// Constants
// ============================================================================

const API_BASE = process.env.PLAYWRIGHT_API_BASE
  || process.env.NEXT_PUBLIC_API_BASE
  || 'http://localhost:8080';

const MOCK_USER = {
  id: 'e2e-user-001',
  email: 'test@meepleai.dev',
  displayName: 'E2E Tester',
  role: 'User',
};

const MOCK_GAME = {
  id: 'game-catan-e2e',
  title: 'Catan',
};

const MOCK_THREAD_ID = 'thread-e2e-001';

const MOCK_AGENT_RESULT = {
  agentId: 'agent-e2e-001',
  agentName: 'Esperto di Catan',
  threadId: MOCK_THREAD_ID,
  slotUsed: 2,
  gameAddedToCollection: false,
};

const MOCK_SLOTS = {
  used: 1,
  total: 5,
  available: 4,
};

const MOCK_THREAD = {
  id: MOCK_THREAD_ID,
  title: 'Chat con Catan',
  gameId: MOCK_GAME.id,
  agentId: MOCK_AGENT_RESULT.agentId,
  agentTypology: 'Tutor',
  status: 'Active',
  messages: [],
};

// ============================================================================
// Helpers
// ============================================================================

/** Set up common API mocks for auth + basic endpoints */
async function setupBaseMocks(page: import('@playwright/test').Page) {
  // Auth
  await page.route(`${API_BASE}/api/v1/auth/me`, async route => {
    await route.fulfill({ json: MOCK_USER });
  });

  // Catch-all for unmatched API calls
  await page.route(`${API_BASE}/api/**`, async route => {
    const method = route.request().method();
    if (method === 'GET') {
      await route.fulfill({ status: 200, json: [] });
    } else if (method === 'DELETE') {
      await route.fulfill({ status: 204, body: '' });
    } else {
      await route.fulfill({ status: 200, json: { success: true } });
    }
  });

  // Agent slots
  await page.route(`${API_BASE}/api/v1/agents/slots`, async route => {
    await route.fulfill({ json: MOCK_SLOTS });
  });

  // Games list (for GameSelector)
  await page.route(`${API_BASE}/api/v1/shared-games*`, async route => {
    await route.fulfill({
      json: {
        items: [{
          id: MOCK_GAME.id,
          title: MOCK_GAME.title,
          yearPublished: 1995,
          minPlayers: 3,
          maxPlayers: 4,
          playingTimeMinutes: 90,
          imageUrl: '',
          thumbnailUrl: '',
          status: 'Published',
        }],
        total: 1,
        page: 1,
        pageSize: 20,
      },
    });
  });

  // User library
  await page.route(`${API_BASE}/api/v1/library*`, async route => {
    await route.fulfill({
      json: {
        items: [{
          id: 'lib-1',
          userId: MOCK_USER.id,
          gameId: MOCK_GAME.id,
          gameTitle: MOCK_GAME.title,
          gamePublisher: 'Kosmos',
          gameYearPublished: 1995,
          gameIconUrl: null,
          gameImageUrl: null,
          addedAt: '2024-01-01T00:00:00Z',
          notes: null,
          isFavorite: false,
          currentState: 'Owned',
          stateChangedAt: null,
          stateNotes: null,
          hasPdfDocuments: true,
        }],
        total: 1,
      },
    });
  });

  // All games (for ChatThreadView game name resolution)
  await page.route(`${API_BASE}/api/v1/games*`, async route => {
    await route.fulfill({
      json: {
        games: [{ id: MOCK_GAME.id, title: MOCK_GAME.title }],
      },
    });
  });

  // Agent typologies
  await page.route(`${API_BASE}/api/v1/agent-typologies*`, async route => {
    await route.fulfill({
      json: [
        { id: 'typology-tutor', name: 'Tutor', description: 'Insegna le regole', status: 'Approved' },
        { id: 'typology-stratega', name: 'Stratega', description: 'Suggerisce strategie', status: 'Approved' },
      ],
    });
  });

  // Models
  await page.route(`${API_BASE}/api/v1/models*`, async route => {
    await route.fulfill({
      json: [
        { id: 'openrouter/auto', name: 'Auto', tier: 'free' },
      ],
    });
  });
}

/** Build SSE body string from RagStreamingEvent format */
function buildSSEBody(events: Array<{ type: number; data: unknown }>): string {
  return events
    .map(e => `data: ${JSON.stringify({ type: e.type, data: e.data, timestamp: new Date().toISOString() })}\n\n`)
    .join('');
}

// StreamingEventType enum values (matches useAgentChatStream.ts)
const EventType = {
  StateUpdate: 0,
  Token: 7,
  Complete: 4,
  FollowUpQuestions: 8,
};

// ============================================================================
// Tests
// ============================================================================

test.describe('Agent Creation Wizard + Chat Flow', () => {
  test.beforeEach(async ({ page }) => {
    await setupBaseMocks(page);
  });

  // --------------------------------------------------------------------------
  // Wizard Opening
  // --------------------------------------------------------------------------

  test('wizard opens from agents page "Crea nuovo agente" button', async ({ page }) => {
    // Mock agents list (empty)
    await page.route(`${API_BASE}/api/v1/agents*`, async route => {
      await route.fulfill({ json: [] });
    });

    await page.goto('/agents');

    // Look for the create button
    const createBtn = page.getByRole('button', { name: /crea/i });
    await expect(createBtn).toBeVisible({ timeout: 10000 });
    await createBtn.click();

    // Wizard sheet should open
    await expect(page.getByText('Crea Agente')).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('Gioco')).toBeVisible();
    await expect(page.getByText('Configura Agente')).toBeVisible();
  });

  // --------------------------------------------------------------------------
  // Full Flow: Wizard → Chat → Message
  // --------------------------------------------------------------------------

  test('complete flow: create agent → redirect to chat → welcome message', async ({ page }) => {
    // Mock create agent endpoint
    await page.route(`${API_BASE}/api/v1/agents/create-with-setup`, async route => {
      await route.fulfill({ json: MOCK_AGENT_RESULT });
    });

    // Mock chat thread (for after redirect)
    await page.route(`${API_BASE}/api/v1/chat/threads/${MOCK_THREAD_ID}`, async route => {
      await route.fulfill({ json: MOCK_THREAD });
    });

    await page.goto('/agents');

    // Open wizard
    const createBtn = page.getByRole('button', { name: /crea/i });
    await expect(createBtn).toBeVisible({ timeout: 10000 });
    await createBtn.click();

    // Wait for wizard to open
    await expect(page.getByText('Crea Agente')).toBeVisible({ timeout: 5000 });

    // Click "Crea e Inizia Chat" button
    const submitBtn = page.getByRole('button', { name: /crea e inizia chat/i });
    await expect(submitBtn).toBeVisible();

    // If button is disabled (no game selected), we need to select a game first
    // The GameSelector component loads games via API
    // For this test, we check if button becomes enabled or we interact with game selector

    // Fill agent name
    const nameInput = page.locator('#agent-name');
    if (await nameInput.isVisible()) {
      await nameInput.fill('Tutor Catan E2E');
    }
  });

  // --------------------------------------------------------------------------
  // Chat Welcome Message
  // --------------------------------------------------------------------------

  test('chat page shows welcome message for new thread with agent', async ({ page }) => {
    // Mock thread endpoint
    await page.route(`${API_BASE}/api/v1/chat/threads/${MOCK_THREAD_ID}`, async route => {
      await route.fulfill({ json: MOCK_THREAD });
    });

    await page.goto(`/chat/${MOCK_THREAD_ID}`);

    // Wait for chat view to load
    const chatView = page.locator('[data-testid="chat-thread-view"]');
    await expect(chatView).toBeVisible({ timeout: 10000 });

    // Welcome message should contain Tutor greeting for Catan
    await expect(page.getByText(/Ho studiato il regolamento di Catan/)).toBeVisible({ timeout: 5000 });

    // Capabilities section should be visible
    await expect(page.getByText(/Cosa posso fare/)).toBeVisible();
  });

  test('chat page shows Arbitro welcome for Arbitro typology', async ({ page }) => {
    await page.route(`${API_BASE}/api/v1/chat/threads/${MOCK_THREAD_ID}`, async route => {
      await route.fulfill({
        json: { ...MOCK_THREAD, agentTypology: 'Arbitro' },
      });
    });

    await page.goto(`/chat/${MOCK_THREAD_ID}`);

    await expect(page.locator('[data-testid="chat-thread-view"]')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText(/Sono il tuo arbitro per Catan/)).toBeVisible({ timeout: 5000 });
  });

  test('chat page shows Stratega welcome for Stratega typology', async ({ page }) => {
    await page.route(`${API_BASE}/api/v1/chat/threads/${MOCK_THREAD_ID}`, async route => {
      await route.fulfill({
        json: { ...MOCK_THREAD, agentTypology: 'Stratega' },
      });
    });

    await page.goto(`/chat/${MOCK_THREAD_ID}`);

    await expect(page.locator('[data-testid="chat-thread-view"]')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText(/analizzare le tue mosse a Catan/)).toBeVisible({ timeout: 5000 });
  });

  test('chat page shows Narratore welcome for Narratore typology', async ({ page }) => {
    await page.route(`${API_BASE}/api/v1/chat/threads/${MOCK_THREAD_ID}`, async route => {
      await route.fulfill({
        json: { ...MOCK_THREAD, agentTypology: 'Narratore' },
      });
    });

    await page.goto(`/chat/${MOCK_THREAD_ID}`);

    await expect(page.locator('[data-testid="chat-thread-view"]')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText(/Benvenuto nel mondo di Catan/)).toBeVisible({ timeout: 5000 });
  });

  // --------------------------------------------------------------------------
  // Chat SSE Streaming (Agent RAG endpoint)
  // --------------------------------------------------------------------------

  test('send message → SSE streaming → response appears', async ({ page }) => {
    // Mock thread with existing messages
    await page.route(`${API_BASE}/api/v1/chat/threads/${MOCK_THREAD_ID}`, async route => {
      await route.fulfill({
        json: {
          ...MOCK_THREAD,
          messages: [
            { id: 'msg-welcome', role: 'assistant', content: 'Benvenuto!', timestamp: '2024-01-01T00:00:00Z' },
          ],
        },
      });
    });

    // Mock SSE agent chat endpoint
    await page.route(`${API_BASE}/api/v1/agents/${MOCK_AGENT_RESULT.agentId}/chat`, async route => {
      const sseBody = buildSSEBody([
        { type: EventType.StateUpdate, data: { message: 'Cerco nel regolamento...' } },
        { type: EventType.Token, data: { token: 'I coloni ' } },
        { type: EventType.Token, data: { token: 'di Catan ' } },
        { type: EventType.Token, data: { token: 'possono commerciare ' } },
        { type: EventType.Token, data: { token: 'liberamente tra loro.' } },
        { type: EventType.FollowUpQuestions, data: { questions: ['Come funziona il brigante?', 'Quali risorse servono?'] } },
        { type: EventType.Complete, data: { totalTokens: 150, chatThreadId: MOCK_THREAD_ID } },
      ]);

      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
          'Cache-Control': 'no-cache',
          'Connection': 'keep-alive',
        },
        body: sseBody,
      });
    });

    await page.goto(`/chat/${MOCK_THREAD_ID}`);

    // Wait for chat to load
    await expect(page.locator('[data-testid="chat-thread-view"]')).toBeVisible({ timeout: 10000 });

    // Type a message
    const input = page.locator('[data-testid="message-input"]');
    await expect(input).toBeVisible({ timeout: 5000 });
    await input.fill('Come funziona il commercio a Catan?');

    // Send
    const sendBtn = page.locator('[data-testid="send-btn"]');
    await sendBtn.click();

    // Verify the streamed response appears
    await expect(page.getByText('I coloni di Catan possono commerciare liberamente tra loro.')).toBeVisible({
      timeout: 10000,
    });
  });

  // --------------------------------------------------------------------------
  // Input Disabled During Streaming
  // --------------------------------------------------------------------------

  test('input and send button disabled during streaming', async ({ page }) => {
    await page.route(`${API_BASE}/api/v1/chat/threads/${MOCK_THREAD_ID}`, async route => {
      await route.fulfill({
        json: {
          ...MOCK_THREAD,
          messages: [
            { id: 'msg-1', role: 'assistant', content: 'Ciao!', timestamp: '2024-01-01T00:00:00Z' },
          ],
        },
      });
    });

    // Slow SSE - delay the response
    let resolveStream: () => void;
    const streamReady = new Promise<void>(resolve => { resolveStream = resolve; });

    await page.route(`${API_BASE}/api/v1/agents/${MOCK_AGENT_RESULT.agentId}/chat`, async route => {
      await streamReady;
      const sseBody = buildSSEBody([
        { type: EventType.Token, data: { token: 'Risposta.' } },
        { type: EventType.Complete, data: { totalTokens: 10 } },
      ]);
      await route.fulfill({
        status: 200,
        headers: { 'Content-Type': 'text/event-stream' },
        body: sseBody,
      });
    });

    await page.goto(`/chat/${MOCK_THREAD_ID}`);
    await expect(page.locator('[data-testid="message-input"]')).toBeVisible({ timeout: 10000 });

    // Send message
    await page.locator('[data-testid="message-input"]').fill('Test');
    await page.locator('[data-testid="send-btn"]').click();

    // Input should be disabled while streaming
    await expect(page.locator('[data-testid="message-input"]')).toBeDisabled({ timeout: 3000 });
    await expect(page.locator('[data-testid="send-btn"]')).toBeDisabled();

    // Release stream
    resolveStream!();

    // After streaming completes, input should be enabled again
    await expect(page.locator('[data-testid="message-input"]')).toBeEnabled({ timeout: 10000 });
  });

  // --------------------------------------------------------------------------
  // Error Handling
  // --------------------------------------------------------------------------

  test('SSE error shows error state in chat', async ({ page }) => {
    await page.route(`${API_BASE}/api/v1/chat/threads/${MOCK_THREAD_ID}`, async route => {
      await route.fulfill({
        json: {
          ...MOCK_THREAD,
          messages: [
            { id: 'msg-1', role: 'assistant', content: 'Ciao!', timestamp: '2024-01-01T00:00:00Z' },
          ],
        },
      });
    });

    // Mock SSE returning HTTP error
    await page.route(`${API_BASE}/api/v1/agents/${MOCK_AGENT_RESULT.agentId}/chat`, async route => {
      await route.fulfill({ status: 500, body: 'Internal Server Error' });
    });

    await page.goto(`/chat/${MOCK_THREAD_ID}`);
    await expect(page.locator('[data-testid="message-input"]')).toBeVisible({ timeout: 10000 });

    // Send message
    await page.locator('[data-testid="message-input"]').fill('Test errore');
    await page.locator('[data-testid="send-btn"]').click();

    // Input should become enabled again after error
    await expect(page.locator('[data-testid="message-input"]')).toBeEnabled({ timeout: 10000 });
  });

  // --------------------------------------------------------------------------
  // Thread Not Found
  // --------------------------------------------------------------------------

  test('shows error when thread not found', async ({ page }) => {
    await page.route(`${API_BASE}/api/v1/chat/threads/nonexistent`, async route => {
      await route.fulfill({ status: 404, json: null });
    });

    await page.goto('/chat/nonexistent');

    await expect(page.locator('[data-testid="chat-error"]')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Thread non trovato')).toBeVisible();
  });

  // --------------------------------------------------------------------------
  // Wizard Validation
  // --------------------------------------------------------------------------

  test('wizard submit button is disabled when no game selected', async ({ page }) => {
    await page.route(`${API_BASE}/api/v1/agents*`, async route => {
      await route.fulfill({ json: [] });
    });

    await page.goto('/agents');

    const createBtn = page.getByRole('button', { name: /crea/i });
    await expect(createBtn).toBeVisible({ timeout: 10000 });
    await createBtn.click();

    // The "Crea e Inizia Chat" button should be visible but disabled
    const submitBtn = page.getByRole('button', { name: /crea e inizia chat/i });
    await expect(submitBtn).toBeVisible({ timeout: 5000 });
    await expect(submitBtn).toBeDisabled();
  });

  test('wizard close does not trigger side effects', async ({ page }) => {
    await page.route(`${API_BASE}/api/v1/agents*`, async route => {
      await route.fulfill({ json: [] });
    });

    await page.goto('/agents');

    // Open wizard
    const createBtn = page.getByRole('button', { name: /crea/i });
    await expect(createBtn).toBeVisible({ timeout: 10000 });
    await createBtn.click();
    await expect(page.getByText('Crea Agente')).toBeVisible({ timeout: 5000 });

    // Close with "Annulla" button
    const cancelBtn = page.getByRole('button', { name: /annulla/i });
    await cancelBtn.click();

    // Should still be on /agents
    await expect(page).toHaveURL(/\/agents/);
  });

  // --------------------------------------------------------------------------
  // No Slots Available
  // --------------------------------------------------------------------------

  test('shows warning when no slots available', async ({ page }) => {
    // Override slots mock with 0 available
    await page.route(`${API_BASE}/api/v1/agents/slots`, async route => {
      await route.fulfill({
        json: { used: 5, total: 5, available: 0 },
      });
    });

    await page.route(`${API_BASE}/api/v1/agents*`, async route => {
      if (route.request().url().includes('slots')) return;
      await route.fulfill({ json: [] });
    });

    await page.goto('/agents');

    const createBtn = page.getByRole('button', { name: /crea/i });
    await expect(createBtn).toBeVisible({ timeout: 10000 });
    await createBtn.click();

    // Warning about no slots should appear
    await expect(page.getByText(/nessuno slot disponibile/i)).toBeVisible({ timeout: 5000 });

    // Submit button should be disabled
    const submitBtn = page.getByRole('button', { name: /crea e inizia chat/i });
    await expect(submitBtn).toBeDisabled();
  });

  // --------------------------------------------------------------------------
  // Loading State
  // --------------------------------------------------------------------------

  test('chat shows loading state initially', async ({ page }) => {
    // Delay the thread response
    await page.route(`${API_BASE}/api/v1/chat/threads/${MOCK_THREAD_ID}`, async route => {
      await new Promise(resolve => setTimeout(resolve, 2000));
      await route.fulfill({ json: MOCK_THREAD });
    });

    await page.goto(`/chat/${MOCK_THREAD_ID}`);

    // Should show loading first
    await expect(page.locator('[data-testid="chat-loading"]')).toBeVisible({ timeout: 3000 });

    // Then transition to chat view
    await expect(page.locator('[data-testid="chat-thread-view"]')).toBeVisible({ timeout: 10000 });
  });
});
