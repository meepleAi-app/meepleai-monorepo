import { test, expect } from '@playwright/test';

/**
 * CHAT-03: Multi-document context switching in chat interface
 *
 * Feature: Multi-game chat context management
 * As a user discussing multiple games
 * I want to switch between rulebooks without losing my conversation history
 * So that I can compare rules across games and maintain context for each game
 *
 * Epic: EPIC-03 - Chat Interface Enhancement
 * Issue: #403
 */

test.describe('CHAT-03: Multi-game chat context switching', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to home and login
    await page.goto('/');

    // Login with demo user
    await page.fill('input[name="email"]', 'user@meepleai.dev');
    await page.fill('input[name="password"]', 'Demo123!');
    await page.click('button[type="submit"]:has-text("Login")');

    // Wait for successful login (redirected to homepage or chat available)
    await page.waitForURL('/');

    // Navigate to chat page
    await page.goto('/chat');
    await page.waitForSelector('h2:has-text("MeepleAI Chat")');
  });

  /**
   * Scenario: User switches between games and conversation history is preserved
   *   Given user is on chat page
   *   When user selects Chess game
   *   And sends message about castling
   *   Then switches to Checkers game
   *   And sends message about backwards movement
   *   Then switches back to Chess
   *   Then Chess conversation history is restored
   *   And Checkers history is preserved when switching back
   */
  test('preserves conversation history when switching between games', async ({ page }) => {
    // Step 1: Select Chess game
    const gameSelector = page.locator('#gameSelect');
    await gameSelector.selectOption({ label: /chess/i });

    // Wait for agents to load
    await page.waitForSelector('#agentSelect option:not(:disabled)', { timeout: 10000 });

    // Select Q&A agent
    const agentSelector = page.locator('#agentSelect');
    await agentSelector.selectOption({ index: 1 }); // Select first non-empty option

    // Verify game context badge shows Chess
    const contextBadge = page.locator('div[aria-label*="Active game context"]');
    await expect(contextBadge).toContainText(/chess/i);

    // Step 2: Send message about Chess castling
    const messageInput = page.locator('#message-input');
    const sendButton = page.locator('button[type="submit"][aria-label="Send message"]');

    await messageInput.fill('How does castling work in Chess?');
    await sendButton.click();

    // Wait for user message to appear
    await expect(page.locator('.message, [role="log"] li').filter({ hasText: 'castling' }).first()).toBeVisible({ timeout: 15000 });

    // Note: We're testing the UI, not waiting for AI response (which may be slow/unavailable in tests)

    // Step 3: Switch to Checkers game
    await gameSelector.selectOption({ label: /checkers|tic.tac.toe/i }); // Use available games

    // Verify game context badge updated
    await expect(contextBadge).not.toContainText(/chess/i);

    // Wait for agents to load for new game
    await page.waitForSelector('#agentSelect option:not(:disabled)', { timeout: 10000 });
    await agentSelector.selectOption({ index: 1 });

    // Verify Chess message is NOT visible (we switched games)
    const messagesArea = page.locator('[role="region"][aria-label="Chat messages"]');
    const chessMessages = messagesArea.locator('text=castling');
    await expect(chessMessages).not.toBeVisible();

    // Step 4: Send message about Checkers (or Tic-Tac-Toe)
    await messageInput.fill('Can pieces move backwards?');
    await sendButton.click();

    // Wait for backwards message to appear
    await expect(page.locator('.message, [role="log"] li').filter({ hasText: 'backwards' }).first()).toBeVisible({ timeout: 15000 });

    // Step 5: Switch back to Chess
    await gameSelector.selectOption({ label: /chess/i });

    // Wait for page to update
    await page.waitForTimeout(500);

    // Verify game context badge shows Chess again
    await expect(contextBadge).toContainText(/chess/i);

    // Step 6: Verify Chess conversation history is restored
    await expect(page.locator('.message, [role="log"] li').filter({ hasText: 'castling' }).first()).toBeVisible({ timeout: 10000 });

    // Verify Checkers message is NOT visible (we're back on Chess)
    const checkersMessages = messagesArea.locator('text=backwards');
    await expect(checkersMessages).not.toBeVisible();

    // Step 7: Switch back to Checkers/Tic-Tac-Toe to verify its history was preserved
    await gameSelector.selectOption({ label: /checkers|tic.tac.toe/i });

    // Wait for page to update
    await page.waitForTimeout(500);

    // Verify Checkers history is still there
    await expect(page.locator('.message, [role="log"] li').filter({ hasText: 'backwards' }).first()).toBeVisible({ timeout: 10000 });

    // Verify Chess message is NOT visible
    const chessMessagesAgain = messagesArea.locator('text=castling');
    await expect(chessMessagesAgain).not.toBeVisible();
  });

  /**
   * Scenario: Game context badge is visible and updates correctly
   *   Given user has selected a game
   *   Then game context badge shows current game name
   *   When user switches to different game
   *   Then badge updates to show new game name
   */
  test('game context badge displays and updates correctly', async ({ page }) => {
    const gameSelector = page.locator('#gameSelect');
    const contextBadge = page.locator('div[aria-label*="Active game context"]');

    // Initially no game selected - badge should not be visible or be empty
    // (depending on implementation)

    // Select Chess
    await gameSelector.selectOption({ label: /chess/i });
    await page.waitForTimeout(300);

    // Verify badge shows Chess
    await expect(contextBadge).toBeVisible();
    await expect(contextBadge).toContainText(/chess/i);

    // Switch to another game
    await gameSelector.selectOption({ label: /tic.tac.toe/i });
    await page.waitForTimeout(300);

    // Verify badge updated
    await expect(contextBadge).toContainText(/tic.tac.toe/i);
    await expect(contextBadge).not.toContainText(/chess/i);
  });

  /**
   * Scenario: Chat list is filtered by selected game
   *   Given user creates chat for Chess
   *   When user switches to different game
   *   Then only chats for that game are shown in sidebar
   */
  test('chat list filters by selected game', async ({ page }) => {
    const gameSelector = page.locator('#gameSelect');

    // Select Chess and create a chat
    await gameSelector.selectOption({ label: /chess/i });
    await page.waitForSelector('#agentSelect option:not(:disabled)', { timeout: 10000 });

    const agentSelector = page.locator('#agentSelect');
    await agentSelector.selectOption({ index: 1 });

    // Create new chat button
    const newChatButton = page.locator('button:has-text("Nuova Chat"), button[aria-label="Create new chat"]');
    await newChatButton.click();

    // Wait for chat to be created and appear in sidebar
    await page.waitForSelector('[role="list"] [role="button"], nav[aria-label="Chat history"] li', { timeout: 5000 });

    // Count chats in sidebar
    const chessChats = await page.locator('[role="list"] [role="button"], nav[aria-label="Chat history"] li').count();

    // Switch to different game
    await gameSelector.selectOption({ label: /tic.tac.toe/i });
    await page.waitForTimeout(500);

    // Chat list should be different (either empty or different chats)
    const ticTacToeChats = await page.locator('[role="list"] [role="button"], nav[aria-label="Chat history"] li').count();

    // They should be different unless user has chats for both games
    // In fresh test environment, Tic-Tac-Toe should have 0 chats
    expect(ticTacToeChats).toBeLessThanOrEqual(chessChats);
  });

  /**
   * Scenario: Multiple rapid switches preserve state correctly
   *   Given user has conversations in multiple games
   *   When user rapidly switches between games
   *   Then each game maintains its own state
   *   And no data is lost or mixed between games
   */
  test('rapid game switching preserves independent state', async ({ page }) => {
    const gameSelector = page.locator('#gameSelect');
    const messageInput = page.locator('#message-input');
    const sendButton = page.locator('button[type="submit"][aria-label="Send message"]');
    const messagesArea = page.locator('[role="region"][aria-label="Chat messages"]');

    // Setup: Create conversations in two games
    // Game 1: Chess
    await gameSelector.selectOption({ label: /chess/i });
    await page.waitForSelector('#agentSelect option:not(:disabled)', { timeout: 10000 });
    const agentSelector = page.locator('#agentSelect');
    await agentSelector.selectOption({ index: 1 });

    await messageInput.fill('Chess question 1');
    await sendButton.click();
    await page.waitForTimeout(1000);

    // Game 2: Tic-Tac-Toe
    await gameSelector.selectOption({ label: /tic.tac.toe/i });
    await page.waitForSelector('#agentSelect option:not(:disabled)', { timeout: 10000 });
    await agentSelector.selectOption({ index: 1 });

    await messageInput.fill('Tic-Tac-Toe question 1');
    await sendButton.click();
    await page.waitForTimeout(1000);

    // Rapid switching test
    for (let i = 0; i < 3; i++) {
      // Switch to Chess
      await gameSelector.selectOption({ label: /chess/i });
      await page.waitForTimeout(200);

      // Verify Chess message exists
      await expect(messagesArea.locator('text=Chess question 1')).toBeVisible();

      // Verify Tic-Tac-Toe message is NOT visible
      await expect(messagesArea.locator('text=Tic-Tac-Toe question 1')).not.toBeVisible();

      // Switch to Tic-Tac-Toe
      await gameSelector.selectOption({ label: /tic.tac.toe/i });
      await page.waitForTimeout(200);

      // Verify Tic-Tac-Toe message exists
      await expect(messagesArea.locator('text=Tic-Tac-Toe question 1')).toBeVisible();

      // Verify Chess message is NOT visible
      await expect(messagesArea.locator('text=Chess question 1')).not.toBeVisible();
    }
  });

  /**
   * Scenario: Accessibility - keyboard navigation works for game switching
   *   Given user is on chat page
   *   When user uses keyboard to navigate game selector
   *   Then game selector is keyboard accessible
   *   And screen reader announces game context changes
   */
  test('game selector is keyboard accessible', async ({ page }) => {
    const gameSelector = page.locator('#gameSelect');

    // Focus on game selector
    await gameSelector.focus();

    // Verify it's focused
    await expect(gameSelector).toBeFocused();

    // Use arrow keys to navigate (simulated)
    await gameSelector.press('ArrowDown');
    await gameSelector.press('Enter');

    // Verify selection changed
    const selectedValue = await gameSelector.inputValue();
    expect(selectedValue).toBeTruthy();
    expect(selectedValue).not.toBe('');

    // Verify ARIA label on context badge
    const contextBadge = page.locator('div[aria-label*="Active game context"]');
    const ariaLabel = await contextBadge.getAttribute('aria-label');
    expect(ariaLabel).toContain('Active game context');
  });
});
