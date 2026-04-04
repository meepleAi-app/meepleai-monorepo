import { type Page, expect } from '@playwright/test';

import { BasePage } from '../base/BasePage';

export class AgentChatPage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  async goto(): Promise<void> {
    await this.page.goto('/agents');
    await this.waitForLoad();
  }

  async navigateToChat(agentId: string): Promise<void> {
    await this.page.goto(`/agents/${agentId}`);
    await this.waitForLoad();

    // If we see "Inizia Conversazione" button, click it
    const startChat = this.page.getByRole('button', { name: /inizia conversazione|start chat/i });
    if (await startChat.isVisible({ timeout: 5_000 }).catch(() => false)) {
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
