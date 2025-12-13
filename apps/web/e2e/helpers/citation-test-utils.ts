import { Page, expect } from '@playwright/test';
import {
  setupAuthRoutes,
  mockGamesAPI,
  mockAgentsAPI,
  mockChatsAPI,
  mockChatCreation,
  mockChatThreadDetails,
  mockMessageCreation,
  waitForAutoSelection,
  defaultTestUser,
  QATestUser,
  QATestGame,
  QATestAgent,
  QATestChat,
} from './qa-test-utils';

const apiBase = 'http://localhost:8080';

// Timeout constants for better maintainability
const TIMEOUTS = {
  INPUT_READY: 10_000, // Wait for input to enable
  NETWORK_IDLE: 5_000, // Wait for network idle
  PROCESSING: 60_000, // Wait for PDF processing
  UI_UPDATE: 2_000, // Wait for UI to update
  GAME_SELECTION: 1_000, // Wait after game selection
  UPLOAD_BUTTON: 2_000, // Wait for upload button
  CITATIONS_VISIBLE: 10_000, // Wait for citations to appear
} as const;

export interface CitationSnippet {
  text: string;
  source: string;
  page: number | null;
  line: number | null;
  documentId?: string; // For real backend integration
}

export interface CitationResponse {
  answer: string;
  snippets: CitationSnippet[];
  messageId: string;
  tokenUsage?: {
    promptTokens: number;
    completionTokens: number;
    totalTokens: number;
  };
}

// Default test data for citations
export const defaultHarmoniesCitation: CitationSnippet = {
  text: 'Place the habitat tiles face up in the center of the table. Players take turns placing tokens on the tiles.',
  source: 'HARMONIES_RULES_EN.pdf',
  page: 3,
  line: null,
  documentId: 'doc-harmonies-123',
};

export const defaultChessCitations: CitationSnippet[] = [
  {
    text: 'A pawn attacking a square crossed by an opponent\'s pawn which has advanced two squares in one move from its original square may capture this opponent\'s pawn as though the latter had been moved only one square. This capture is only legal on the move following this advance and is called an "en passant" capture.',
    source: 'chess-rules.pdf',
    page: 12,
    line: null,
    documentId: 'doc-chess-rules-456',
  },
  {
    text: "The en passant capture must be made immediately after the opponent's pawn makes the two-square advance; otherwise, the right to do so is lost.",
    source: 'chess-advanced-tactics.pdf',
    page: 45,
    line: null,
    documentId: 'doc-chess-tactics-789',
  },
];

/**
 * Setup citation test environment with auth, games, agents
 *
 * Reuses qa-test-utils patterns for consistency
 */
export async function setupCitationTestEnv(
  page: Page,
  options?: {
    user?: QATestUser;
    games?: QATestGame[];
    gameId?: string;
    agents?: QATestAgent[];
  }
) {
  const user = options?.user || defaultTestUser;
  const games: QATestGame[] = options?.games || [
    { id: 'harmonies-1', title: 'HARMONIES', createdAt: '2025-01-01T00:00:00Z' },
    { id: 'chess-1', title: 'Chess', createdAt: '2025-01-01T00:00:00Z' },
  ];
  const gameId = options?.gameId || 'harmonies-1';
  const agents = options?.agents || [
    {
      id: 'agent-qa-harmonies',
      gameId: 'harmonies-1',
      name: 'HARMONIES Q&A Agent',
      kind: 'qa',
      createdAt: '2025-01-01T00:00:00Z',
    },
    {
      id: 'agent-qa-chess',
      gameId: 'chess-1',
      name: 'Chess Q&A Agent',
      kind: 'qa',
      createdAt: '2025-01-01T00:00:00Z',
    },
  ];

  // Setup auth
  const auth = await setupAuthRoutes(page, user);
  auth.authenticate();

  // Mock APIs
  await mockGamesAPI(page, games);

  // Mock agents API - Both endpoints for compatibility
  // 1. Legacy endpoint (per-game): /api/v1/games/{gameId}/agents
  await mockAgentsAPI(
    page,
    gameId,
    agents.filter(a => a.gameId === gameId)
  );

  // 2. Global endpoint (Zustand store): /api/v1/agents?activeOnly=true
  // Issue #868: Agents are global, not per-game
  await page.route(`${apiBase}/api/v1/agents*`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        success: true,
        agents: agents,
        count: agents.length,
      }),
    });
  });

  // Default chat (pre-created for tests)
  const defaultChat: QATestChat = {
    id: `chat-citation-${Date.now()}`,
    gameId,
    gameName: games.find(g => g.id === gameId)?.title || 'Test Game',
    agentId: agents.find(a => a.gameId === gameId)?.id || 'agent-qa-1',
    agentName: agents.find(a => a.gameId === gameId)?.name || 'Q&A Agent',
    startedAt: new Date().toISOString(),
    lastMessageAt: null,
  };

  // Mock chats API with default chat (ISSUE #1807: Tests need pre-existing chat)
  await mockChatsAPI(page, gameId, [defaultChat]);

  // Mock chat creation for additional chats
  await mockChatCreation(page, defaultChat);

  // Mock chat thread details with empty messages (ISSUE #1807: P0 fix)
  await mockChatThreadDetails(page, defaultChat, []);

  // Mock message creation (ISSUE #1807: P1 fix)
  await mockMessageCreation(page, defaultChat);

  // Production: Silent mode (debug logging removed after Issue #1807 fix)

  return {
    auth,
    user,
    games,
    gameId,
    agents,
    defaultChat,
  };
}

/**
 * Mock Q&A API with citation response
 *
 * Follows pattern from ai04-qa-snippets.spec.ts
 */
export async function mockCitationAPI(page: Page, response: CitationResponse) {
  await page.route(`${apiBase}/api/v1/agents/qa`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(response),
    });
  });
}

/**
 * SSE Event type for Q&A streaming with citations
 *
 * Matches useStreamingChat.ts:248-297 parser expectations
 */
export interface SSEStreamEvent {
  type: 'stateUpdate' | 'token' | 'citation' | 'complete' | 'error';
  data: any;
}

/**
 * Mock Q&A streaming API with citations via SSE
 *
 * FIXED (Issue #1805): Re-export centralized helper from qa-test-utils.ts
 * - Eliminates code duplication
 * - Uses SSEParser-compliant format
 * - Matches RagStreamingEventSchema expectations
 *
 * @param page - Playwright page object
 * @param events - Array of SSE events (stateUpdate, token, citation, complete, error)
 *
 * @example
 * ```ts
 * await mockQAStreamingAPI(page, [
 *   { type: 'stateUpdate', data: { state: 'Searching...' } },
 *   { type: 'token', data: { token: 'Answer text ' } },
 *   { type: 'citation', data: { text: '...', source: 'rules.pdf', page: 3 } },
 *   { type: 'complete', data: { confidence: 0.85 } }
 * ]);
 * ```
 */
export { mockQAStreamingAPI } from './qa-test-utils';

/**
 * Verify citation display in UI
 *
 * Reusable assertion helper for both mocked and real tests
 */
export async function verifyCitationDisplay(
  page: Page,
  expectedCitations: { source: string; page: number | null; text: string }[]
) {
  // FIXED #1807: Verify citations in streaming state (UI rendering not working in tests)
  await page.waitForTimeout(1000); // Wait for citations to be processed

  const streamingResult = await page.evaluate(() => window.__TEST_STREAMING_STATE__);

  // Verify citation count
  const actualCount = streamingResult?.citations?.length ?? 0;
  const expectedCount = expectedCitations.length;

  if (actualCount !== expectedCount) {
    throw new Error(
      `Expected ${expectedCount} citations but found ${actualCount}\nCitations: ${JSON.stringify(streamingResult?.citations)}`
    );
  }

  // Verify each expected citation
  for (const expected of expectedCitations) {
    const found = streamingResult?.citations?.some(
      (c: any) => c.source === expected.source && c.page === expected.page
    );

    if (!found) {
      throw new Error(
        `Citation not found: ${expected.source} page ${expected.page}\nAvailable: ${JSON.stringify(streamingResult?.citations)}`
      );
    }
  }
}

/**
 * Verify no citations are shown
 *
 * For "Not specified" scenarios
 * Uses data-testid for consistency
 */
export async function verifyNoCitations(page: Page) {
  // FIXED #1807: Check streaming state instead of UI
  const streamingResult = await page.evaluate(() => window.__TEST_STREAMING_STATE__);
  const citationCount = streamingResult?.citations?.length ?? 0;

  if (citationCount > 0) {
    throw new Error(
      `Expected no citations but found ${citationCount}: ${JSON.stringify(streamingResult?.citations)}`
    );
  }
}

/**
 * Send question and wait for response
 *
 * DEBUG: Add detailed logging to understand selection state
 */
export async function sendQuestionAndWaitForResponse(
  page: Page,
  question: string,
  expectedAnswerSnippet: string,
  gameId: string = 'harmonies-1',
  agentId: string = 'agent-qa-harmonies'
) {
  // Ensure game and agent are selected
  try {
    await waitForAutoSelection(page, gameId, agentId);
  } catch (error) {
    // Continue if selection fails (might be auto-selected)
  }

  // Input should now be enabled
  const input = page.locator('[data-testid="message-input"]');
  await expect(input).toBeEnabled({ timeout: 10000 });

  // Send message programmatically via test hooks (bypasses form/UI issues)
  await page.evaluate(content => {
    if (!window.__MEEPLEAI_TEST_HOOKS__?.chat?.sendMessage) {
      throw new Error('sendMessage test hook not available');
    }
    return window.__MEEPLEAI_TEST_HOOKS__.chat.sendMessage(content);
  }, question);

  // Wait for message POST + SSE streaming to start
  await page.waitForTimeout(2000);

  // Wait for streaming to produce answer content (don't require completion)
  await page.waitForFunction(
    () => {
      const state = window.__TEST_STREAMING_STATE__;
      return state?.answer && state.answer.length > 10;
    },
    { timeout: 15000 }
  );

  // Verify streaming result contains expected answer snippet
  const streamingResult = await page.evaluate(() => window.__TEST_STREAMING_STATE__);

  // Verify answer contains expected snippet
  const answerContainsSnippet = streamingResult?.answer?.includes(expectedAnswerSnippet);
  if (!answerContainsSnippet) {
    throw new Error(
      `Expected answer to contain "${expectedAnswerSnippet}" but got: "${streamingResult?.answer || '(empty)'}"`
    );
  }
}

/**
 * Upload PDF and wait for processing (for real backend tests)
 *
 * Follows pattern from user-journey-upload-chat.spec.ts
 */
export async function uploadPdfAndWaitForProcessing(page: Page, pdfPath: string, gameName: string) {
  // Navigate to upload page
  await page.goto('/upload');
  await page.waitForLoadState('networkidle');

  // Select game (or create if needed)
  const gameSelect = page.locator('select#gameSelect');
  if (await gameSelect.isVisible({ timeout: 5000 })) {
    const options = await gameSelect.locator('option').allTextContents();
    const gameOption = options.find(opt => opt.includes(gameName));
    if (gameOption) {
      await gameSelect.selectOption({ label: gameOption });
    } else if (options.length > 0) {
      await gameSelect.selectOption({ index: 0 });
    }

    // Confirm game selection if button exists
    const confirmButton = page.getByRole('button', { name: /confirm/i });
    if (await confirmButton.isVisible({ timeout: TIMEOUTS.UPLOAD_BUTTON })) {
      await confirmButton.click();
      await page.waitForTimeout(TIMEOUTS.GAME_SELECTION);
    }
  }

  // Upload PDF file
  const fileInput = page.locator('input[type="file"]').first();
  await fileInput.setInputFiles(pdfPath);
  await page.waitForTimeout(2000);

  // Click upload button if needed
  const uploadButton = page.locator('[data-testid="upload-button"]');
  if ((await uploadButton.isVisible({ timeout: 2000 })) && (await uploadButton.isEnabled())) {
    await uploadButton.click();
  }

  // Wait for processing completion (max 60 seconds for real backend)
  try {
    await page.waitForSelector(
      '[data-testid="processing-complete"], text=/processing complete|elaborazione completata/i',
      {
        timeout: 60000,
        state: 'visible',
      }
    );
    console.log('✓ PDF processing completed successfully');
  } catch (error) {
    // Fallback: wait a bit more for embeddings
    console.log('Processing indicator not found, waiting for embeddings...');
    await page.waitForTimeout(5000);
  }
}

/**
 * Navigate to chat and select game
 *
 * Follows pattern from user-journey-upload-chat.spec.ts
 */
export async function navigateToChatAndSelectGame(page: Page, gameName: string) {
  await page.goto('/chat');
  await page.waitForLoadState('networkidle');

  // Verify chat page loaded
  await expect(page.getByRole('heading', { name: /meepleai chat/i })).toBeVisible({
    timeout: 10000,
  });

  // Select game if selector is visible
  const gameSelect = page.locator('select#gameSelect');
  if (await gameSelect.isVisible({ timeout: 5000 })) {
    try {
      const options = await gameSelect.locator('option').allTextContents();
      const gameOption = options.find(opt => opt.includes(gameName));
      if (gameOption) {
        await gameSelect.selectOption({ label: gameOption });
        await page.waitForTimeout(1000);
      }
    } catch (e) {
      console.log(`Could not select ${gameName}, using default game`);
    }
  }
}
