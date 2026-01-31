import { Page, expect } from '@playwright/test';

const apiBase = 'http://localhost:8080';

export interface QATestUser {
  id: string;
  email: string;
  displayName: string;
  role: string;
}

export interface QATestGame {
  id: string;
  title: string;
  createdAt: string;
}

export interface QATestAgent {
  id: string;
  gameId: string;
  name: string;
  kind: string;
  createdAt: string;
}

export interface QATestChat {
  id: string;
  gameId: string;
  gameName: string;
  agentId: string;
  agentName: string;
  startedAt: string;
  lastMessageAt: string | null;
}

export interface QASnippet {
  text: string;
  source: string;
  page: number | null;
  line: number | null;
}

export interface QAResponse {
  answer: string;
  snippets: QASnippet[];
  messageId: string;
  tokenUsage?: {
    promptTokens: number;
    completionTokens: number;
    totalTokens: number;
  };
}

// Default test data
export const defaultTestUser: QATestUser = {
  id: 'user-test-1',
  email: 'testuser@meepleai.dev',
  displayName: 'Test User',
  role: 'User',
};

export const defaultGames: QATestGame[] = [
  { id: 'chess-1', title: 'Chess', createdAt: '2025-01-01T00:00:00Z' },
  { id: 'tictactoe-1', title: 'Tic-Tac-Toe', createdAt: '2025-01-01T00:00:00Z' },
];

export const defaultAgents: QATestAgent[] = [
  {
    id: 'agent-qa-1',
    gameId: 'chess-1',
    name: 'Chess Q&A Agent',
    kind: 'qa',
    createdAt: '2025-01-01T00:00:00Z',
  },
  {
    id: 'agent-qa-2',
    gameId: 'tictactoe-1',
    name: 'Tic-Tac-Toe Q&A Agent',
    kind: 'qa',
    createdAt: '2025-01-01T00:00:00Z',
  },
];

/**
 * Setup authentication routes and return control object
 */
export async function setupAuthRoutes(page: Page, user: QATestUser = defaultTestUser) {
  let authenticated = false;

  const userResponse = {
    user,
    expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  };

  await page.route(`${apiBase}/api/v1/auth/me`, async route => {
    if (authenticated) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(userResponse),
      });
    } else {
      await route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Unauthorized' }),
      });
    }
  });

  await page.route(`${apiBase}/api/v1/auth/login`, async route => {
    authenticated = true;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(userResponse),
    });
  });

  // Mock /auth/session/status endpoint (FIXED #1807: Missing session check)
  await page.route(`${apiBase}/api/v1/auth/session/status`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        authenticated: true,
        user: userResponse.user,
      }),
    });
  });

  return {
    authenticate() {
      authenticated = true;
    },
    deauthenticate() {
      authenticated = false;
    },
  };
}

/**
 * Mock games API
 */
export async function mockGamesAPI(page: Page, games: QATestGame[] = defaultGames) {
  await page.route(`${apiBase}/api/v1/games`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(games),
    });
  });
}

/**
 * Mock agents API for a specific game
 */
export async function mockAgentsAPI(page: Page, gameId: string, agents: QATestAgent[]) {
  await page.route(`${apiBase}/api/v1/games/${gameId}/agents`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(agents),
    });
  });
}

/**
 * Mock chats API (list chats for a game)
 */
/**
 * Mock chat threads API (FIXED #1807: KnowledgeBase endpoint pattern)
 * Endpoint: GET /api/v1/knowledge-base/chat-threads?gameId={id}
 */
export async function mockChatsAPI(page: Page, gameId: string, chats: QATestChat[] = []) {
  await page.route(`${apiBase}/api/v1/knowledge-base/chat-threads?gameId=${gameId}`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(chats),
    });
  });
}

/**
 * Mock chat creation (FIXED #1807: KnowledgeBase endpoint pattern)
 * Endpoint: POST /api/v1/knowledge-base/chat-threads
 */
export async function mockChatCreation(page: Page, chat: QATestChat) {
  await page.route(`${apiBase}/api/v1/knowledge-base/chat-threads`, async route => {
    if (route.request().method() === 'POST') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(chat),
      });
    }
  });
}

/**
 * Mock chat thread details with messages (FIXED #1807: KnowledgeBase endpoint pattern)
 * Endpoint: GET /api/v1/knowledge-base/chat-threads/{threadId}
 */
export async function mockChatThreadDetails(page: Page, chat: QATestChat, messages: any[] = []) {
  await page.route(`${apiBase}/api/v1/knowledge-base/chat-threads/${chat.id}`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        ...chat,
        messages,
        status: 'Active',
        messageCount: messages.length,
      }),
    });
  });
}

/**
 * Mock message creation (FIXED #1807: KnowledgeBase endpoint pattern)
 * Endpoint: POST /api/v1/knowledge-base/chat-threads/{threadId}/messages
 */
export async function mockMessageCreation(page: Page, chat: QATestChat) {
  // Match both the specific chat ID and any thread ID (for flexibility)
  const patterns = [
    `${apiBase}/api/v1/knowledge-base/chat-threads/${chat.id}/messages`,
    new RegExp(`${apiBase.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}/api/v1/knowledge-base/chat-threads/[^/]+/messages`),
  ];

  for (const pattern of patterns) {
    await page.route(pattern, async route => {
      console.log(`🎯 Message route matched: ${route.request().method()} ${route.request().url()}`);
      if (route.request().method() === 'POST') {
        const body = route.request().postDataJSON();
        console.log(`📨 Message POST body:`, body);
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            ...chat,
            messages: [
              {
                role: body.role || 'user',
                content: body.content,
                timestamp: new Date().toISOString(),
                backendMessageId: `msg-${Date.now()}`,
              },
            ],
            messageCount: 1,
          }),
        });
      }
    });
  }
}

/**
 * Mock Q&A API with custom response
 */
export async function mockQAAPI(page: Page, response: QAResponse) {
  await page.route(`${apiBase}/api/v1/agents/qa`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(response),
    });
  });
}

/**
 * Mock Q&A streaming API with SSE events
 *
 * FIXED (Issue #1805): Generate SSEParser-compliant format
 * - Format: data: {"type":"...","data":{...},"timestamp":"..."}\n\n
 * - Matches SSEParser.ts expectations (sseParser.ts:88-119)
 * - Matches RagStreamingEventSchema (streaming.schemas.ts:143-147)
 */
export async function mockQAStreamingAPI(
  page: Page,
  events: {
    type: 'stateUpdate' | 'token' | 'citations' | 'citation' | 'complete' | 'error';
    data: any;
  }[]
) {
  await page.route(`${apiBase}/api/v1/agents/qa/stream`, async route => {
    const sseData = events
      .map(event => {
        const payload = {
          type: event.type,
          data: event.data,
          timestamp: new Date().toISOString(),
        };
        return `data: ${JSON.stringify(payload)}\n\n`;
      })
      .join('');

    await route.fulfill({
      status: 200,
      headers: {
        'Content-Type': 'text/event-stream',
        'Cache-Control': 'no-cache',
        Connection: 'keep-alive',
      },
      body: sseData,
    });
  });
}

/**
 * Setup complete Q&A test environment with auth, games, agents, chats
 *
 * CRITICAL FIX (Issue #1009): Uses localStorage pre-seed to enable input on page load
 */
export async function setupQATestEnvironment(
  page: Page,
  options?: {
    user?: QATestUser;
    games?: QATestGame[];
    gameId?: string;
    agents?: QATestAgent[];
  }
) {
  const user = options?.user || defaultTestUser;
  const games = options?.games || defaultGames;
  const gameId = options?.gameId || 'chess-1';
  const agents = options?.agents || defaultAgents.filter(a => a.gameId === gameId);

  // Setup auth
  const auth = await setupAuthRoutes(page, user);
  auth.authenticate();

  // Mock APIs
  await mockGamesAPI(page, games);
  await mockAgentsAPI(page, gameId, agents);
  await mockChatsAPI(page, gameId);

  // Default chat creation
  const defaultChat: QATestChat = {
    id: `chat-${Date.now()}`,
    gameId,
    gameName: games.find(g => g.id === gameId)?.title || 'Test Game',
    agentId: agents[0]?.id || 'agent-qa-1',
    agentName: agents[0]?.name || 'Q&A Agent',
    startedAt: new Date().toISOString(),
    lastMessageAt: null,
  };
  await mockChatCreation(page, defaultChat);

  return {
    auth,
    user,
    games,
    gameId,
    agents,
    defaultChat,
    mockQA: (response: QAResponse) => mockQAAPI(page, response),
    mockQAStreaming: (events: Parameters<typeof mockQAStreamingAPI>[1]) =>
      mockQAStreamingAPI(page, events),
  };
}

/**
 * Wait for page load and ensure game/agent are selected
 *
 * BEST PRACTICE: Uses data-testid + programmatic approach (UI Element Identification Guide)
 * Follows strategy from ui-element-identification-guide.md
 *
 * CRITICAL FIX (Issue #1800): Programmatic selection via data-testid to avoid:
 * - Magic strings/localizable text
 * - Viewport/responsive issues (sidebar hidden on mobile)
 * - Unreliable auto-selection in test environment
 */
export async function waitForAutoSelection(
  page: Page,
  expectedGameId: string,
  expectedAgentId: string
) {
  // Wait for page to load completely
  await page.waitForLoadState('networkidle');
  await page.waitForLoadState('networkidle'); // Double wait pattern from existing tests

  // Wait for game selector with data-testid (Strategy 3 from UI Guide)
  const gameSelector = page.getByTestId('game-selector');
  await gameSelector.waitFor({ state: 'attached', timeout: 10000 });

  // Check if auto-selection happened (input enabled = game + agent selected)
  const messageInput = page.locator('#message-input');
  const isEnabled = await messageInput.isEnabled().catch(() => false);

  if (isEnabled) {
    // Auto-selection worked! We're done
    return;
  }

  // Auto-selection didn't happen - use programmatic selection via test hooks
  // ISSUE #1807 FIX: Radix UI Select doesn't respond reliably to Playwright clicks/keyboard
  // Solution: Bypass UI interaction entirely using test hooks (Option A)

  // Wait for test hooks to be available (ChatProvider useEffect must run first)
  await page.waitForFunction(
    () => window.__MEEPLEAI_TEST_HOOKS__?.chat !== undefined,
    { timeout: 10000 }
  );

  // Use test hooks to programmatically select game and agent
  await page.evaluate(
    ({ gameId, agentId }) => {
      if (!window.__MEEPLEAI_TEST_HOOKS__?.chat) {
        throw new Error('Test hooks not available - ChatProvider not loaded');
      }
      return window.__MEEPLEAI_TEST_HOOKS__.chat.selectGameAndAgent(gameId, agentId);
    },
    { gameId: expectedGameId, agentId: expectedAgentId }
  );

  // Wait for React state updates to propagate and input to enable
  await expect(messageInput).toBeEnabled({ timeout: 10000 });
}
