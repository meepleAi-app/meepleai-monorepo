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
        .or(this.page.getByRole('button', { name: /create agent|new agent/i }))
    );
    await expect(
      this.page.locator('[data-testid="agent-creation-sheet"]').or(this.page.getByRole('dialog'))
    ).toBeVisible();
  }

  async selectGame(gameTitle: string): Promise<void> {
    const gameSelector = this.page
      .locator('[data-testid="game-selector"]')
      .or(this.page.getByLabel(/game/i));
    await gameSelector.click();
    await this.page.getByText(gameTitle, { exact: false }).first().click();
  }

  async selectStrategy(strategy: string = 'Tutor'): Promise<void> {
    const strategyOption = this.page
      .locator(`[data-testid="strategy-${strategy.toLowerCase()}"]`)
      .or(this.page.getByText(strategy, { exact: false }));
    await strategyOption.click();
  }

  async selectFreeTier(): Promise<void> {
    const freeTier = this.page
      .locator('[data-testid="tier-free"]')
      .or(this.page.getByText(/free/i).first());
    if (await freeTier.isVisible()) {
      await freeTier.click();
    }
  }

  async submitCreation(): Promise<AgentCreationResult> {
    // Register BOTH response interceptors BEFORE clicking
    const agentResponsePromise = this.page.waitForResponse(
      resp =>
        resp.url().includes('/agents') &&
        resp.request().method() === 'POST' &&
        resp.status() >= 200 &&
        resp.status() < 300
    );

    const sessionResponsePromise = this.page
      .waitForResponse(
        resp => resp.url().includes('/game-sessions') && resp.status() >= 200 && resp.status() < 300
      )
      .catch(() => null); // Optional - may not fire

    await this.click(
      this.page
        .locator('[data-testid="create-agent-submit"]')
        .or(this.page.getByRole('button', { name: /create|submit/i }))
    );

    const agentResponse = await agentResponsePromise;
    const agentData = await agentResponse.json();

    let gameSessionId = agentData.gameSessionId ?? '';
    if (!gameSessionId) {
      const sessionResponse = await Promise.race([
        sessionResponsePromise,
        new Promise<null>(resolve => setTimeout(() => resolve(null), 10_000)),
      ]);
      if (sessionResponse) {
        const sessionData = await sessionResponse.json();
        gameSessionId = sessionData.id ?? sessionData.gameSessionId ?? '';
      }
    }

    return {
      agentId: agentData.id ?? agentData.agentId ?? '',
      gameSessionId,
    };
  }

  async waitForAgentReady(timeout: number = 30_000): Promise<void> {
    await expect(
      this.page
        .locator('[data-testid="agent-status-ready"]')
        .or(this.page.getByText(/ready|online|active/i))
    ).toBeVisible({ timeout });
  }
}
