/**
 * AI Chat Flow - Visual Documentation
 *
 * Captures visual documentation for AI chat flows:
 * - Start chat session
 * - Ask questions
 * - View citations/sources
 * - Chat history
 * - Export chat
 *
 * @see docs/08-user-flows/user-role/04-ai-chat.md
 */

import { test } from '../../fixtures';
import { AuthHelper, USER_FIXTURES } from '../../pages';
import {
  ScreenshotHelper,
  USER_FLOWS,
  disableAnimations,
  waitForStableState,
  ANNOTATION_COLORS,
} from '../fixtures/screenshot-helpers';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Mock chat data
const MOCK_THREADS = [
  {
    id: 'thread-1',
    title: 'Ticket to Ride Rules',
    gameId: 'game-1',
    createdAt: '2026-01-19T10:00:00Z',
    updatedAt: '2026-01-19T12:00:00Z',
    messageCount: 5,
  },
  {
    id: 'thread-2',
    title: 'Catan Strategy',
    gameId: 'game-2',
    createdAt: '2026-01-18T14:00:00Z',
    updatedAt: '2026-01-18T15:30:00Z',
    messageCount: 8,
  },
];

const MOCK_MESSAGES = [
  {
    id: 'msg-1',
    role: 'user',
    content: 'How do I score points for completing a route?',
    createdAt: '2026-01-19T10:05:00Z',
  },
  {
    id: 'msg-2',
    role: 'assistant',
    content: 'In Ticket to Ride, you score points by completing routes between cities. The longer the route, the more points you earn. A 6-train route scores 15 points.',
    createdAt: '2026-01-19T10:05:30Z',
    citations: [
      { page: 5, text: 'Route scoring table', source: 'rulebook.pdf' },
    ],
  },
];

test.describe('AI Chat Flow - Visual Documentation', () => {
  let helper: ScreenshotHelper;
  let authHelper: AuthHelper;

  test.beforeEach(async ({ page }) => {
    helper = new ScreenshotHelper({
      outputDir: USER_FLOWS.aiChat.outputDir,
      flow: USER_FLOWS.aiChat.name,
      role: USER_FLOWS.aiChat.role,
    });
    authHelper = new AuthHelper(page);
    await disableAnimations(page);

    // Setup authenticated session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // Mock chat threads
    await page.route(`${API_BASE}/api/v1/chat-threads*`, async route => {
      const url = route.request().url();
      if (url.includes('/chat-threads/thread-1/messages')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_MESSAGES),
        });
      } else if (url.includes('/chat-threads/thread-1')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_THREADS[0]),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_THREADS),
        });
      }
    });

    // Mock games for chat context
    await page.route(`${API_BASE}/api/v1/games*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'game-1', title: 'Ticket to Ride', publisher: 'Days of Wonder' },
          { id: 'game-2', title: 'Catan', publisher: 'Kosmos' },
        ]),
      });
    });
  });

  test('chat interface - main view', async ({ page }) => {
    // Step 1: Navigate to chat
    await page.goto('/chat');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Chat Interface',
      description: 'AI chat interface for asking game-related questions',
      annotations: [
        { selector: '[data-testid="chat-input"], textarea, input[type="text"]', label: 'Message Input', color: ANNOTATION_COLORS.primary },
      ],
      nextAction: 'Type a question',
    });

    // Step 2: Message input area
    const chatInput = page.locator('[data-testid="chat-input"], textarea, input[placeholder*="message"], input[placeholder*="Ask"]').first();
    if (await chatInput.isVisible({ timeout: 3000 }).catch(() => false)) {
      await chatInput.fill('How do I score points in Ticket to Ride?');
      await waitForStableState(page);

      await helper.capture(page, {
        step: 2,
        title: 'Question Entered',
        description: 'User types a question about game rules',
        annotations: [
          { selector: '[data-testid="chat-input"], textarea', label: 'Question', color: ANNOTATION_COLORS.success },
          { selector: 'button[type="submit"], button:has-text("Send"), [data-testid="send-message"]', label: 'Send', color: ANNOTATION_COLORS.primary },
        ],
        previousAction: 'Type question',
        nextAction: 'Send message',
      });
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Chat interface captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test.skip('chat with response and citations', async ({ page }) => {
    // SKIP: Route /chat/[threadId] not implemented - chat uses client-side routing
    // TODO: Implement thread URL routing or test via client-side navigation

    // Mock SSE streaming response
    await page.route(`${API_BASE}/api/v1/chat-threads/*/messages`, async route => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'new-msg',
            role: 'assistant',
            content: 'In Ticket to Ride, routes score based on their length...',
            citations: [
              { page: 5, text: 'Route scoring', source: 'rulebook.pdf' },
            ],
          }),
        });
      } else {
        await route.continue();
      }
    });

    // Step 1: Navigate to existing chat thread
    await page.goto('/chat/thread-1');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Chat Thread',
      description: 'Existing conversation with AI assistant',
      nextAction: 'View conversation',
    });

    // Step 2: Message bubbles
    const messageArea = page.locator('[data-testid="messages"], .messages, .chat-messages').first();
    if (await messageArea.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Chat Messages',
        description: 'Conversation history with user questions and AI responses',
        annotations: [
          { selector: '[data-testid="user-message"], .user-message', label: 'Your Question', color: ANNOTATION_COLORS.info },
          { selector: '[data-testid="assistant-message"], .assistant-message', label: 'AI Response', color: ANNOTATION_COLORS.success },
        ],
        previousAction: 'Load thread',
        nextAction: 'View citations',
      });
    }

    // Step 3: Citations/sources
    const citationCard = page.locator('[data-testid="citation"], .citation, .source-card').first();
    if (await citationCard.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 3,
        title: 'Source Citations',
        description: 'AI responses include citations from rulebook PDFs',
        annotations: [
          { selector: '[data-testid="citation"], .citation', label: 'Citation', color: ANNOTATION_COLORS.primary },
        ],
        previousAction: 'View response',
        nextAction: 'Click citation',
      });
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ Chat conversation captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('chat history - thread list', async ({ page }) => {
    // Step 1: Navigate to chat history
    await page.goto('/chat');
    await waitForStableState(page);

    // Step 2: Thread list sidebar
    const threadList = page.locator('[data-testid="thread-list"], .thread-list, aside').first();
    if (await threadList.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 1,
        title: 'Chat History',
        description: 'List of previous chat conversations',
        annotations: [
          { selector: '[data-testid="thread-list"], .thread-list', label: 'Thread List', color: ANNOTATION_COLORS.info },
        ],
        nextAction: 'Select a thread',
      });

      // Step 3: Thread item
      const threadItem = page.locator('[data-testid="thread-item"], .thread-item').first();
      if (await threadItem.isVisible({ timeout: 2000 }).catch(() => false)) {
        await helper.capture(page, {
          step: 2,
          title: 'Thread Item',
          description: 'Individual conversation thread with title and date',
          annotations: [
            { selector: '[data-testid="thread-item"], .thread-item', label: 'Chat Thread', color: ANNOTATION_COLORS.primary },
          ],
          previousAction: 'View history',
          nextAction: 'Open thread',
        });
      }
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Chat history captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test.skip('export chat - download conversation', async ({ page }) => {
    // SKIP: Route /chat/[threadId] not implemented - chat uses client-side routing
    // TODO: Implement thread URL routing or test via client-side navigation

    // Mock export endpoint
    await page.route(`${API_BASE}/api/v1/chat-threads/thread-1/export*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'text/markdown',
        body: '# Ticket to Ride Rules\n\n## Question\nHow do I score points?\n\n## Answer\nRoutes score based on length...',
      });
    });

    // Step 1: Navigate to chat thread
    await page.goto('/chat/thread-1');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Thread for Export',
      description: 'Select conversation to export',
      nextAction: 'Find export option',
    });

    // Step 2: Export button/menu
    const exportButton = page.locator('button:has-text("Export"), [data-testid="export-chat"], button[aria-label*="export"]').first();
    if (await exportButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Export Option',
        description: 'Export conversation as Markdown or PDF',
        annotations: [
          { selector: 'button:has-text("Export"), [data-testid="export-chat"]', label: 'Export', color: ANNOTATION_COLORS.primary },
        ],
        previousAction: 'View thread',
        nextAction: 'Click export',
      });
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Chat export captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('start new chat - game selection', async ({ page }) => {
    // Step 1: Navigate to chat
    await page.goto('/chat');
    await waitForStableState(page);

    // Step 2: New chat button
    const newChatButton = page.locator('button:has-text("New Chat"), button:has-text("New"), [data-testid="new-chat"]').first();
    if (await newChatButton.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 1,
        title: 'Start New Chat',
        description: 'Begin a new conversation with AI assistant',
        annotations: [
          { selector: 'button:has-text("New Chat"), [data-testid="new-chat"]', label: 'New Chat', color: ANNOTATION_COLORS.success },
        ],
        nextAction: 'Click new chat',
      });

      await newChatButton.click();
      await waitForStableState(page);

      // Step 3: Game selection (if modal appears)
      const gameSelector = page.locator('[data-testid="game-selector"], .game-selector, [role="dialog"]').first();
      if (await gameSelector.isVisible({ timeout: 2000 }).catch(() => false)) {
        await helper.capture(page, {
          step: 2,
          title: 'Select Game',
          description: 'Choose a game to chat about',
          annotations: [
            { selector: '[data-testid="game-selector"], .game-selector', label: 'Game Selection', color: ANNOTATION_COLORS.primary },
          ],
          previousAction: 'Click new chat',
          nextAction: 'Select game',
        });
      }
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ New chat captured: ${helper.getCapturedSteps().length} screenshots`);
  });
});
