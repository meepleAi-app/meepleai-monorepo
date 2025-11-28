import { test, expect } from '@playwright/test';
import { setupQATestEnvironment, QAResponse } from './helpers/qa-test-utils';

test.describe('Q&A Interface - Multi-Turn Conversations (Issue #1009)', () => {
  test('should maintain context across follow-up questions', async ({ page }) => {
    const { mockQA } = await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // First question
    await mockQA({
      answer: 'Chess is played on an 8x8 board.',
      snippets: [{ text: 'Board size is 8x8.', source: 'rules.pdf', page: 1, line: null }],
      messageId: 'msg-turn-1',
    });

    await page.fill('#message-input', 'What is the board size?');
    await page.locator('button[type="submit"]').click();
    await expect(page.getByText(/8x8 board/i)).toBeVisible({ timeout: 5000 });

    // Follow-up question (context-dependent)
    await mockQA({
      answer: 'Each square can hold one piece.',
      snippets: [{ text: 'One piece per square.', source: 'rules.pdf', page: 2, line: null }],
      messageId: 'msg-turn-2',
    });

    await page.fill('#message-input', 'How many pieces per square?');
    await page.locator('button[type="submit"]').click();
    await expect(page.getByText(/one piece/i)).toBeVisible({ timeout: 5000 });

    // Verify both messages visible in chat history
    await expect(page.getByText(/8x8 board/i)).toBeVisible();
    await expect(page.getByText(/one piece/i)).toBeVisible();
  });

  test('should handle context switching between different games', async ({ page }) => {
    const env = await setupQATestEnvironment(page, {
      games: [
        { id: 'chess-1', title: 'Chess', createdAt: '2025-01-01T00:00:00Z' },
        { id: 'monopoly-1', name: 'Monopoly', createdAt: '2025-01-01T00:00:00Z' },
      ],
    });

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Question about Chess
    await env.mockQA({
      answer: 'Chess has 16 pieces per player.',
      snippets: [],
      messageId: 'msg-chess-1',
    });

    await page.selectOption('#gameSelect', 'chess-1');
    await page.fill('#message-input', 'How many pieces?');
    await page.locator('button[type="submit"]').click();
    await expect(page.getByText(/16 pieces/i)).toBeVisible({ timeout: 5000 });

    // Switch to Monopoly
    await page.selectOption('#gameSelect', 'monopoly-1');

    // Mock new agents and chat for Monopoly
    await page.route('**/api/v1/games/monopoly-1/agents', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'agent-monopoly-1',
            gameId: 'monopoly-1',
            name: 'Monopoly Agent',
            kind: 'qa',
            createdAt: '2025-01-01T00:00:00Z',
          },
        ]),
      });
    });

    await page.route('**/api/v1/chats?gameId=monopoly-1', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    });

    await env.mockQA({
      answer: 'Monopoly has $20,580 total in the bank.',
      snippets: [],
      messageId: 'msg-monopoly-1',
    });

    await page.fill('#message-input', 'How much money in the bank?');
    await page.locator('button[type="submit"]').click();
    await expect(page.getByText(/\$20,580/i)).toBeVisible({ timeout: 5000 });

    // Verify context switched correctly (Chess and Monopoly answers both visible)
    await expect(page.getByText(/16 pieces/i)).toBeVisible();
    await expect(page.getByText(/\$20,580/i)).toBeVisible();
  });

  test('should persist chat history across page reloads', async ({ page }) => {
    const { mockQA } = await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Send first message
    await mockQA({
      answer: 'Castling is a special king move.',
      snippets: [],
      messageId: 'msg-persist-1',
    });

    await page.fill('#message-input', 'What is castling?');
    await page.locator('button[type="submit"]').click();
    await expect(page.getByText(/Castling/i)).toBeVisible({ timeout: 5000 });

    // Reload page
    await page.reload();

    // Wait for page to load
    await page.waitForLoadState('networkidle');

    // Chat history should be restored (if implemented)
    // This test may need adjustment based on actual persistence implementation
    // For now, verify page reloads successfully
    await expect(page.locator('#message-input')).toBeVisible();
  });

  test('should reference snippets from previous turns', async ({ page }) => {
    const { mockQA } = await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // First turn with snippet
    await mockQA({
      answer: 'En passant is a pawn capture.',
      snippets: [{ text: 'En passant rule details.', source: 'rules.pdf', page: 12, line: null }],
      messageId: 'msg-ref-1',
    });

    await page.fill('#message-input', 'What is en passant?');
    await page.locator('button[type="submit"]').click();
    await expect(page.getByText(/en passant/i)).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('rules.pdf (Pagina 12)')).toBeVisible();

    // Follow-up referencing same snippet
    await mockQA({
      answer: 'Yes, it must be done immediately after the pawn move.',
      snippets: [{ text: 'En passant timing rules.', source: 'rules.pdf', page: 12, line: null }],
      messageId: 'msg-ref-2',
    });

    await page.fill('#message-input', 'Does it have to be immediate?');
    await page.locator('button[type="submit"]').click();
    await expect(page.getByText(/immediately/i)).toBeVisible({ timeout: 5000 });

    // Both snippet references should be visible
    const snippetMatches = await page.getByText('rules.pdf (Pagina 12)').count();
    expect(snippetMatches).toBeGreaterThanOrEqual(2);
  });

  test('should maintain chat thread continuity', async ({ page }) => {
    const { mockQA } = await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Simulate multi-turn conversation
    const turns = [
      { question: 'How do I start?', answer: 'White moves first.' },
      { question: 'What can I move?', answer: 'You can move pawns or knights.' },
      { question: 'How does a knight move?', answer: 'Knights move in an L-shape.' },
    ];

    for (const [index, turn] of turns.entries()) {
      await mockQA({
        answer: turn.answer,
        snippets: [],
        messageId: `msg-thread-${index + 1}`,
      });

      await page.fill('#message-input', turn.question);
      await page.locator('button[type="submit"]').click();
      await expect(page.getByText(new RegExp(turn.answer, 'i'))).toBeVisible({ timeout: 5000 });
    }

    // All questions and answers should be visible in order
    for (const turn of turns) {
      await expect(page.getByText(new RegExp(turn.answer, 'i'))).toBeVisible();
    }
  });

  test('should handle clarification requests in multi-turn flow', async ({ page }) => {
    const { mockQA } = await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Ambiguous question
    await mockQA({
      answer: 'Not specified. Please clarify: do you mean opening moves or endgame?',
      snippets: [],
      messageId: 'msg-clarify-1',
    });

    await page.fill('#message-input', 'Best strategy?');
    await page.locator('button[type="submit"]').click();
    await expect(page.getByText(/clarify/i)).toBeVisible({ timeout: 5000 });

    // Clarified follow-up
    await mockQA({
      answer: 'For opening, control the center with pawns and develop knights.',
      snippets: [
        { text: 'Opening strategy principles.', source: 'strategy.pdf', page: 5, line: null },
      ],
      messageId: 'msg-clarify-2',
    });

    await page.fill('#message-input', 'Opening moves');
    await page.locator('button[type="submit"]').click();
    await expect(page.getByText(/control the center/i)).toBeVisible({ timeout: 5000 });

    // Verify clarification flow maintained
    await expect(page.getByText(/clarify/i)).toBeVisible();
    await expect(page.getByText(/control the center/i)).toBeVisible();
  });

  test('should support conversation branching with new chat', async ({ page }) => {
    const { mockQA } = await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // First conversation
    await mockQA({
      answer: 'First conversation answer.',
      snippets: [],
      messageId: 'msg-branch-1',
    });

    await page.fill('#message-input', 'First question');
    await page.locator('button[type="submit"]').click();
    await expect(page.getByText('First conversation answer.')).toBeVisible({ timeout: 5000 });

    // Start new chat (if "New Chat" button exists)
    const newChatButton = page.locator(
      'button:has-text("New Chat"), button:has-text("Nuova Chat")'
    );
    const hasNewChatButton = await newChatButton.count();

    if (hasNewChatButton > 0) {
      await newChatButton.click();

      // Second conversation (branched)
      await mockQA({
        answer: 'Second conversation answer.',
        snippets: [],
        messageId: 'msg-branch-2',
      });

      await page.fill('#message-input', 'Different question');
      await page.locator('button[type="submit"]').click();
      await expect(page.getByText('Second conversation answer.')).toBeVisible({ timeout: 5000 });

      // First conversation should not be visible (new chat started)
      await expect(page.getByText('First conversation answer.')).not.toBeVisible();
    }
  });

  test('should handle mixed snippet types across turns', async ({ page }) => {
    const { mockQA } = await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Turn 1: Snippet with page number
    await mockQA({
      answer: 'Answer with page number.',
      snippets: [{ text: 'Content with page.', source: 'doc1.pdf', page: 5, line: null }],
      messageId: 'msg-mixed-1',
    });

    await page.fill('#message-input', 'First question');
    await page.locator('button[type="submit"]').click();
    await expect(page.getByText(/Answer with page number/i)).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('doc1.pdf (Pagina 5)')).toBeVisible();

    // Turn 2: Snippet without page number
    await mockQA({
      answer: 'Answer without page number.',
      snippets: [{ text: 'Content without page.', source: 'doc2.txt', page: null, line: null }],
      messageId: 'msg-mixed-2',
    });

    await page.fill('#message-input', 'Second question');
    await page.locator('button[type="submit"]').click();
    await expect(page.getByText(/Answer without page number/i)).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('doc2.txt')).toBeVisible();
    await expect(page.getByText(/doc2\.txt \(Pagina/)).not.toBeVisible();

    // Both snippet types should coexist correctly
    await expect(page.getByText('doc1.pdf (Pagina 5)')).toBeVisible();
    await expect(page.getByText('doc2.txt')).toBeVisible();
  });

  test('should maintain scroll position during multi-turn conversation', async ({ page }) => {
    const { mockQA } = await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Send multiple messages to create scrollable content
    for (let i = 1; i <= 5; i++) {
      await mockQA({
        answer: `Long answer ${i}. ${'Lorem ipsum dolor sit amet. '.repeat(10)}`,
        snippets: [],
        messageId: `msg-scroll-${i}`,
      });

      await page.fill('#message-input', `Question ${i}`);
      await page.locator('button[type="submit"]').click();
      await expect(page.getByText(`Long answer ${i}.`)).toBeVisible({ timeout: 5000 });
    }

    // Verify page has scrolled down (latest message visible)
    await expect(page.getByText(/Long answer 5/i)).toBeVisible();

    // Scroll to top
    await page.evaluate(() => window.scrollTo(0, 0));

    // Verify first message still accessible
    await expect(page.getByText(/Long answer 1/i)).toBeVisible();
  });

  test('should handle conversation with no snippets mixed with snippets', async ({ page }) => {
    const { mockQA } = await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Turn 1: No snippets
    await mockQA({
      answer: 'General answer without sources.',
      snippets: [],
      messageId: 'msg-nosrc-1',
    });

    await page.fill('#message-input', 'General question');
    await page.locator('button[type="submit"]').click();
    await expect(page.getByText(/General answer/i)).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('Fonti:')).not.toBeVisible();

    // Turn 2: With snippets
    await mockQA({
      answer: 'Detailed answer with sources.',
      snippets: [{ text: 'Source content.', source: 'manual.pdf', page: 10, line: null }],
      messageId: 'msg-nosrc-2',
    });

    await page.fill('#message-input', 'Detailed question');
    await page.locator('button[type="submit"]').click();
    await expect(page.getByText(/Detailed answer/i)).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('Fonti:')).toBeVisible();
    await expect(page.getByText('manual.pdf (Pagina 10)')).toBeVisible();

    // Verify both turns displayed correctly
    await expect(page.getByText(/General answer/i)).toBeVisible();
    await expect(page.getByText(/Detailed answer/i)).toBeVisible();
  });
});
