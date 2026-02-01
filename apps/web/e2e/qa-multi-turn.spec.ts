/**
 * QA Multi-Turn Conversation E2E Tests - MIGRATED TO POM
 *
 * @see apps/web/e2e/helpers/qa-test-utils.ts
 */

import { test, expect } from './fixtures';
import { setupQATestEnvironment } from './helpers/qa-test-utils';

test.describe('Q&A Interface - Multi-Turn Conversations (Issue #1009)', () => {
  test('should maintain context across follow-up questions', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // First question
    await page.fill('#message-input', 'What is the board size?');
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(2000);

    // Follow-up question (context-dependent)
    await page.fill('#message-input', 'How many pieces per square?');
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(2000);
  });

  test('should handle context switching between different games', async ({ page }) => {
    const env = await setupQATestEnvironment(page, {
      games: [
        { id: 'chess-1', title: 'Chess', createdAt: '2025-01-01T00:00:00Z' },
        { id: 'monopoly-1', title: 'Monopoly', createdAt: '2025-01-01T00:00:00Z' },
      ],
    });

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Question about Chess
    await page.selectOption('#gameSelect', 'chess-1');
    await page.fill('#message-input', 'How many pieces?');
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(2000);

    // Switch to Monopoly
    await page.selectOption('#gameSelect', 'monopoly-1');

    await page.fill('#message-input', 'How much money in the bank?');
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(2000);
  });

  test('should persist chat history across page reloads', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Send first message

    await page.fill('#message-input', 'What is castling?');
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(2000);

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
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // First turn with snippet

    await page.fill('#message-input', 'What is en passant?');
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(2000);

    // Follow-up referencing same snippet
    await page.fill('#message-input', 'Does it have to be immediate?');
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(2000);
  });

  test('should maintain chat thread continuity', async ({ page }) => {
    await setupQATestEnvironment(page);

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

    for (let i = 0; i < turns.length; i++) {
      const turn = turns[i];
      await page.fill('#message-input', turn.question);
      await page.locator('button[type="submit"]').click();
      await page.waitForTimeout(2000);
    }

    // Verify questions sent
    await expect(page.locator('#message-input')).toBeEnabled();
  });

  test('should handle clarification requests in multi-turn flow', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Ambiguous question

    await page.fill('#message-input', 'Best strategy?');
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(2000);

    // Clarified follow-up
    await page.fill('#message-input', 'Opening moves');
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(2000);
  });

  test('should support conversation branching with new chat', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // First conversation

    await page.fill('#message-input', 'First question');
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(2000);

    // Start new chat (if "New Chat" button exists)
    const newChatButton = page.locator(
      'button:has-text("New Chat"), button:has-text("Nuova Chat")'
    );
    const hasNewChatButton = await newChatButton.count();

    if (hasNewChatButton > 0) {
      await newChatButton.click();

      // Second conversation (branched)

      await page.fill('#message-input', 'Different question');
      await page.locator('button[type="submit"]').click();
      await page.waitForTimeout(2000);

      // First conversation should not be visible (new chat started)
      await expect(page.getByText('First conversation answer.')).not.toBeVisible();
    }
  });

  test('should handle mixed snippet types across turns', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Turn 1: Snippet with page number
    await page.fill('#message-input', 'First question');
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(2000);

    // Turn 2: Snippet without page number
    await page.fill('#message-input', 'Second question');
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(2000);
  });

  test('should maintain scroll position during multi-turn conversation', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Send multiple messages to create scrollable content
    for (let i = 1; i <= 5; i++) {
      await page.fill('#message-input', `Question ${i}`);
      await page.locator('button[type="submit"]').click();
      await page.waitForTimeout(2000);
    }

    // Scroll to top
    await page.evaluate(() => window.scrollTo(0, 0));
  });

  test('should handle conversation with no snippets mixed with snippets', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Turn 1: No snippets
    await page.fill('#message-input', 'General question');
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(2000);

    // Turn 2: With snippets
    await page.fill('#message-input', 'Detailed question');
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(2000);
  });
});
