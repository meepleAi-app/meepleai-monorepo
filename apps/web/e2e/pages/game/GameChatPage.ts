import { type Page, expect } from '@playwright/test';

import { BasePage } from '../base/BasePage';

/**
 * Chat scoped to a GAME — issue #4138.
 *
 * Was `pages/agent/AgentChatPage`, which navigated to `/agents` and
 * `/agents/{agentId}`: both deleted with the user-facing agents section (#4141).
 * There is one system agent now, configured by the admin, so a chat is never
 * addressed to an agent — it is addressed to a game, and the retrieval scope is
 * a parameter of the question.
 *
 * The surviving surface is the library game detail's AI chat tab:
 *   `/library/{gameId}?tab=aiChat` → `GameAiChatTab` → `GameChatTab` →
 *   `components/features/game-chat/ChatInputBar`
 * which is the component carrying the `message-input` / `send-btn` test ids these
 * helpers use. The message helpers are unchanged — they were never agent-specific.
 */
export class GameChatPage extends BasePage {
  private readonly gameId: string;

  constructor(page: Page, gameId: string) {
    super(page);
    this.gameId = gameId;
  }

  async goto(): Promise<void> {
    await this.page.goto(`/library/${this.gameId}?tab=aiChat`);
    await this.waitForLoad();

    // If we see "Inizia Conversazione" button, click it
    const startChat = this.page.getByRole('button', { name: /inizia conversazione|start chat/i });
    if (await startChat.isVisible({ timeout: 5_000 })) {
      await startChat.click();
      await this.waitForLoad();
    }
  }

  async sendMessage(message: string): Promise<void> {
    const chatInput = this.page
      .locator('[data-testid="message-input"]')
      .or(this.page.getByPlaceholder(/scrivi un messaggio|write a message/i));

    await this.fill(chatInput, message);

    await this.click(
      this.page
        .locator('[data-testid="send-btn"]')
        .or(this.page.locator('[aria-label="Invia messaggio"]'))
    );
  }

  async waitForAgentResponse(timeout: number = 60_000): Promise<string> {
    // Wait for assistant message to appear
    const responseLocator = this.page.locator('[data-testid="message-assistant"]').last();

    await expect(responseLocator).toBeVisible({ timeout });

    // Wait for streaming to finish
    const streamingMsg = this.page.locator('[data-testid="message-streaming"]');
    try {
      // Wait for streaming indicator to disappear
      await streamingMsg.waitFor({ state: 'detached', timeout });
    } catch {
      // May already be gone
    }

    // Wait for response text to be non-empty
    await expect(responseLocator).not.toBeEmpty({ timeout: 5_000 });

    const responseText = (await responseLocator.textContent()) ?? '';
    return responseText.trim();
  }

  async verifyResponseIsValid(responseText: string): Promise<void> {
    expect(responseText.length).toBeGreaterThan(10);
    expect(responseText).not.toMatch(/error|errore|failed|something went wrong/i);
  }
}
