import { Page } from '@playwright/test';

const apiBase = 'http://localhost:8080';

export interface QATestUser {
  id: string;
  email: string;
  displayName: string;
  role: string;
}

export interface QATestGame {
  id: string;
  name: string;
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
  { id: 'chess-1', name: 'Chess', createdAt: '2025-01-01T00:00:00Z' },
  { id: 'tictactoe-1', name: 'Tic-Tac-Toe', createdAt: '2025-01-01T00:00:00Z' },
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
export async function mockChatsAPI(page: Page, gameId: string, chats: QATestChat[] = []) {
  await page.route(`${apiBase}/api/v1/chats?gameId=${gameId}`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(chats),
    });
  });
}

/**
 * Mock chat creation
 */
export async function mockChatCreation(page: Page, chat: QATestChat) {
  await page.route(`${apiBase}/api/v1/chats`, async route => {
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
 */
export async function mockQAStreamingAPI(
  page: Page,
  events: { type: 'stateUpdate' | 'token' | 'citations' | 'complete' | 'error'; data: any }[]
) {
  await page.route(`${apiBase}/api/v1/agents/qa/stream`, async route => {
    const sseData = events
      .map(event => `event: ${event.type}\ndata: ${JSON.stringify(event.data)}\n\n`)
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
    gameName: games.find(g => g.id === gameId)?.name || 'Test Game',
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
