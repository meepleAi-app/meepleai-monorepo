import { Page, expect } from '@playwright/test';
import {
  setupAuthRoutes,
  mockGamesAPI,
  mockAgentsAPI,
  mockChatsAPI,
  mockChatCreation,
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
  const games = options?.games || [
    { id: 'harmonies-1', name: 'HARMONIES', createdAt: '2025-01-01T00:00:00Z' },
    { id: 'chess-1', name: 'Chess', createdAt: '2025-01-01T00:00:00Z' },
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
  await mockAgentsAPI(
    page,
    gameId,
    agents.filter(a => a.gameId === gameId)
  );
  await mockChatsAPI(page, gameId);

  // Default chat creation
  const defaultChat: QATestChat = {
    id: `chat-citation-${Date.now()}`,
    gameId,
    gameName: games.find(g => g.id === gameId)?.name || 'Test Game',
    agentId: agents.find(a => a.gameId === gameId)?.id || 'agent-qa-1',
    agentName: agents.find(a => a.gameId === gameId)?.name || 'Q&A Agent',
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
 * Mock Q&A streaming API with citations via SSE
 *
 * Follows pattern from chat-citations.spec.ts
 */
export async function mockCitationStreamingAPI(
  page: Page,
  tokens: string[],
  citations: CitationSnippet[]
) {
  const events: string[] = [];

  // Token events
  tokens.forEach(token => {
    events.push(`event: token\ndata: {"token":"${token}"}\n\n`);
  });

  // Citations event
  if (citations.length > 0) {
    const citationsData = citations.map(c => ({
      documentId: c.documentId || `doc-${Date.now()}`,
      pageNumber: c.page,
      snippet: c.text,
      source: c.source,
      relevanceScore: 0.9,
    }));
    events.push(`event: citations\ndata: ${JSON.stringify({ citations: citationsData })}\n\n`);
  }

  // Complete event
  events.push('event: complete\ndata: {"totalTokens":100,"confidence":0.9}\n\n');

  await page.route(`${apiBase}/api/v1/agents/qa/stream`, async route => {
    await route.fulfill({
      status: 200,
      headers: {
        'Content-Type': 'text/event-stream',
        'Cache-Control': 'no-cache',
        Connection: 'keep-alive',
      },
      body: events.join(''),
    });
  });
}

/**
 * Verify citation display in UI
 *
 * Reusable assertion helper for both mocked and real tests
 */
export async function verifyCitationDisplay(
  page: Page,
  expectedCitations: { source: string; page: number | null; text: string }[]
) {
  // Wait for citations section to appear
  await expect(page.getByText('Fonti:')).toBeVisible({ timeout: 10000 });

  // Verify citation count in header
  const citationCount = expectedCitations.length;
  if (citationCount > 0) {
    await expect(page.getByText(`📚 Fonti (${citationCount})`)).toBeVisible();
  }

  // Verify each citation
  for (const citation of expectedCitations) {
    // Verify source and page number
    if (citation.page !== null) {
      await expect(page.getByText(`${citation.source} (Pagina ${citation.page})`)).toBeVisible();
    } else {
      await expect(page.getByText(citation.source)).toBeVisible();
    }

    // Verify citation text snippet (partial match)
    const snippetPreview = citation.text.substring(0, 50); // First 50 chars
    await expect(
      page.getByText(new RegExp(snippetPreview.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')))
    ).toBeVisible();
  }
}

/**
 * Verify no citations are shown
 *
 * For "Not specified" scenarios
 * Uses data-testid for consistency
 */
export async function verifyNoCitations(page: Page) {
  // Citations section should not be visible (using data-testid)
  await expect(page.getByTestId('message-citations')).not.toBeVisible({
    timeout: TIMEOUTS.NETWORK_IDLE,
  });
}

/**
 * Send question and wait for response
 *
 * Follows pattern from qa-test-utils
 */
export async function sendQuestionAndWaitForResponse(
  page: Page,
  question: string,
  expectedAnswerSnippet: string
) {
  // Wait for input to be enabled (after auto-selection or manual selection)
  const input = page.getByPlaceholder(/fai una domanda|ask a question/i);
  await expect(input).toBeEnabled({ timeout: 15000 });

  // Type question
  await input.fill(question);

  // Click send
  const sendButton = page.getByRole('button', { name: /invia|send/i });
  await sendButton.click();

  // Verify question appears
  await expect(page.getByText(question)).toBeVisible();

  // Wait for assistant response (with expected answer snippet)
  await expect(
    page.getByText(new RegExp(expectedAnswerSnippet.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')))
  ).toBeVisible({
    timeout: 10000,
  });
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
