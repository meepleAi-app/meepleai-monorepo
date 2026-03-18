import { type Page, expect } from '@playwright/test';

import { BasePage } from '../base/BasePage';

export interface AgentCreationResult {
  agentId: string;
  gameSessionId: string;
}

export class AgentCreationPage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  async goto(): Promise<void> {
    await this.page.goto('/agents');
    await this.waitForLoad();
  }

  async openCreationSheet(): Promise<void> {
    await this.click(
      this.page
        .locator('[data-testid="create-agent-button"]')
        .or(this.page.getByRole('button', { name: /crea agente|create agent|new agent|\+/i }))
    );
    // Wait for sheet/dialog to appear
    await expect(this.page.locator('[role="dialog"]').first()).toBeVisible({ timeout: 5_000 });
  }

  async selectGame(gameTitle: string): Promise<void> {
    // Click the game selector trigger
    const gameSelector = this.page
      .locator('[aria-label="Select game"]')
      .or(this.page.getByRole('combobox').first());
    await gameSelector.click();
    // Search for the game
    const searchInput = this.page.getByPlaceholder(/search games|cerca/i);
    if (await searchInput.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await this.fill(searchInput, gameTitle);
      await this.page.waitForTimeout(1000);
    }
    // Click the matching option
    await this.page.getByText(gameTitle, { exact: false }).first().click();
  }

  async selectStrategy(strategy: string = 'Tutor'): Promise<void> {
    const strategyOption = this.page
      .locator(`[data-testid="strategy-${strategy.toLowerCase()}"]`)
      .or(this.page.getByText(strategy, { exact: false }));
    if (await strategyOption.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await strategyOption.click();
    }
  }

  async selectFreeTier(): Promise<void> {
    const freeTier = this.page
      .locator('[data-testid="tier-free"]')
      .or(this.page.getByText(/free|gratuito/i).first());
    if (await freeTier.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await freeTier.click();
    }
  }

  async submitCreation(): Promise<AgentCreationResult> {
    // Register response interceptors BEFORE clicking
    const agentResponsePromise = this.page.waitForResponse(
      resp =>
        (resp.url().includes('/agents') || resp.url().includes('/game-sessions')) &&
        resp.request().method() === 'POST' &&
        resp.status() >= 200 &&
        resp.status() < 300
    );

    // Submit button: "Crea e Inizia Chat" or "Creazione..."
    await this.click(this.page.getByRole('button', { name: /crea e inizia chat|crea|create/i }));

    const response = await agentResponsePromise;
    const data = await response.json();

    return {
      agentId: data.id ?? data.agentId ?? '',
      gameSessionId: data.gameSessionId ?? '',
    };
  }

  async waitForAgentReady(timeout: number = 30_000): Promise<void> {
    // Wait for chat to become available or agent status
    await expect(
      this.page
        .locator('[data-testid="message-input"]')
        .or(this.page.locator('[data-testid="chat-thread-view"]'))
        .or(this.page.getByText(/pronto|ready|inizia conversazione/i))
    ).toBeVisible({ timeout });
  }
}
