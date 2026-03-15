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

    const chatButton = this.page
      .locator('[data-testid="start-chat-button"]')
      .or(this.page.getByRole('button', { name: /chat|ask|start/i }));
    if (await chatButton.isVisible()) {
      await chatButton.click();
      await this.waitForLoad();
    }
  }

  async sendMessage(message: string): Promise<void> {
    const chatInput = this.page
      .locator('[data-testid="chat-input"]')
      .or(this.page.getByPlaceholder(/ask|message|type/i));

    await this.fill(chatInput, message);

    await this.click(
      this.page
        .locator('[data-testid="chat-send-button"]')
        .or(this.page.getByRole('button', { name: /send/i }))
    );
  }

  async waitForAgentResponse(timeout: number = 60_000): Promise<string> {
    const responseLocator = this.page
      .locator('[data-testid="agent-message"], [data-testid="assistant-message"]')
      .last();

    await expect(responseLocator).toBeVisible({ timeout });

    const streamingIndicator = this.page.locator(
      '[data-testid="streaming-indicator"], .animate-pulse'
    );

    try {
      await streamingIndicator.waitFor({ state: 'detached', timeout });
    } catch {
      // Indicator may never appear if response is fast
    }

    // Wait for response text to be non-empty (deterministic)
    await expect(responseLocator).not.toBeEmpty({ timeout: 5_000 });

    const responseText = (await responseLocator.textContent()) ?? '';
    return responseText.trim();
  }

  async verifyResponseIsValid(responseText: string): Promise<void> {
    expect(responseText.length).toBeGreaterThan(10);
    expect(responseText).not.toMatch(/error|failed|something went wrong/i);
  }
}
