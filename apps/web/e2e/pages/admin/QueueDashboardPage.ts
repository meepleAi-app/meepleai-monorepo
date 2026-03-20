import { Locator, expect } from '@playwright/test';

import { BasePage } from '../base/BasePage';

/**
 * QueueDashboardPage - Queue dashboard and embedding flow interactions
 *
 * Provides interactions for the job processing queue dashboard:
 * - Navigation with optional embedding flow context params
 * - Job list and detail panel interactions
 * - Chunk preview and agent test actions
 * - Assertions for embedding flow banner, job state, and chunk visibility
 *
 * Usage:
 *   const queuePage = new QueueDashboardPage(page);
 *   await queuePage.goto({ flow: 'embedding', gameId: '...', gameName: 'Catan' });
 *   await queuePage.expectFlowBannerVisible('Catan');
 *   await queuePage.waitForJobCompletion();
 */
export class QueueDashboardPage extends BasePage {
  // ========================================================================
  // Navigation
  // ========================================================================

  /**
   * Navigate to queue dashboard, optionally with embedding flow context
   * @param params - Optional query parameters
   * @param params.flow - Flow type (e.g. 'embedding')
   * @param params.gameId - Game ID to highlight/filter
   * @param params.gameName - Game name shown in flow banner
   * @param params.jobId - Job ID to auto-select in the list
   */
  async goto(params?: {
    flow?: string;
    gameId?: string;
    gameName?: string;
    jobId?: string;
  }): Promise<void> {
    const searchParams = new URLSearchParams();
    if (params?.flow) searchParams.set('flow', params.flow);
    if (params?.gameId) searchParams.set('gameId', params.gameId);
    if (params?.gameName) searchParams.set('gameName', params.gameName);
    if (params?.jobId) searchParams.set('jobId', params.jobId);

    const query = searchParams.toString();
    const url = query ? `/admin/queue?${query}` : '/admin/queue';

    await this.page.goto(url);
    await this.waitForLoad();
  }

  // ========================================================================
  // Locators
  // ========================================================================

  /**
   * Embedding flow context banner
   * Shown when the page is opened from the upload flow with ?flow=embedding
   */
  get flowBanner(): Locator {
    return this.page.locator('[data-testid="embedding-flow-banner"]');
  }

  /**
   * Job list container
   */
  get jobList(): Locator {
    return this.page.locator('[data-testid="job-list"]');
  }

  /**
   * Job detail side panel
   */
  get jobDetailPanel(): Locator {
    return this.page.locator('[data-testid="job-detail-panel"]');
  }

  /**
   * Chunk preview tab inside the job detail panel
   */
  get chunkPreviewTab(): Locator {
    return this.page.locator('[data-testid="chunk-preview-tab"]');
  }

  /**
   * "Testa Agent" button — navigates to the agent test interface
   */
  get testAgentButton(): Locator {
    return this.page.getByRole('link', { name: /testa agent/i });
  }

  /**
   * Job status badge for the currently selected/visible job
   * Use inside a specific job row for scoped assertions
   */
  get jobStatusBadge(): Locator {
    return this.page.locator('[data-testid="job-status-badge"]');
  }

  // ========================================================================
  // Private helpers
  // ========================================================================

  /**
   * Get a specific job row by job ID
   * @param jobId - Job identifier
   */
  private getJobRow(jobId: string): Locator {
    return this.page.locator(`[data-testid="job-row"][data-job-id="${jobId}"]`);
  }

  /**
   * Get the chunks section within the detail panel
   */
  private get chunksSection(): Locator {
    return this.page.locator('[data-testid="chunks-section"]');
  }

  /**
   * Loading indicator for the job list
   */
  private get jobListLoading(): Locator {
    return this.page.locator('[data-testid="job-list-loading"]');
  }

  // ========================================================================
  // Actions
  // ========================================================================

  /**
   * Wait for the currently selected job to reach a terminal status
   * (Completed or Failed)
   * @param timeout - Max wait time in ms (default: 60 000 ms)
   */
  async waitForJobCompletion(timeout = 60_000): Promise<void> {
    await expect(this.jobStatusBadge).toHaveAttribute('data-status', /completed|failed/i, {
      timeout,
    });
  }

  /**
   * Click a job row to select it and open the detail panel
   * @param jobId - Job ID to select
   */
  async selectJob(jobId: string): Promise<void> {
    const row = this.getJobRow(jobId);
    await this.click(row);
    await this.waitForElement(this.jobDetailPanel);
  }

  /**
   * Click the chunk preview tab to open the chunks section
   */
  async openChunksSection(): Promise<void> {
    await this.click(this.chunkPreviewTab);
    await this.waitForElement(this.chunksSection);
  }

  /**
   * Click the "Testa Agent" link to navigate to the agent test interface
   */
  async clickTestAgent(): Promise<void> {
    await this.click(this.testAgentButton);
  }

  /**
   * Wait for the job list to finish loading
   */
  async waitForJobListLoaded(): Promise<void> {
    await this.waitForElementToDisappear(this.jobListLoading, { timeout: 15_000 });
    await this.waitForElement(this.jobList);
  }

  // ========================================================================
  // Assertions
  // ========================================================================

  /**
   * Assert the embedding flow banner is visible and contains the game name
   * @param gameName - Expected game name in the banner
   */
  async expectFlowBannerVisible(gameName: string): Promise<void> {
    await this.waitForElement(this.flowBanner);
    await expect(this.flowBanner).toContainText(gameName);
  }

  /**
   * Assert the flow banner is not present (normal queue view)
   */
  async expectFlowBannerHidden(): Promise<void> {
    await expect(this.flowBanner).not.toBeVisible();
  }

  /**
   * Assert a specific job row is highlighted (selected state)
   * @param jobId - Job ID to check
   */
  async expectJobHighlighted(jobId?: string): Promise<void> {
    if (jobId) {
      const row = this.getJobRow(jobId);
      await expect(row).toHaveAttribute('aria-selected', 'true');
    } else {
      // Assert that at least one row has the highlighted/selected state
      await expect(
        this.jobList.locator('[aria-selected="true"], [data-selected="true"]').first()
      ).toBeVisible();
    }
  }

  /**
   * Assert the chunks section is visible after opening the chunk preview tab
   */
  async expectChunksVisible(): Promise<void> {
    await this.waitForElement(this.chunksSection);
    await expect(this.chunksSection).toBeVisible();
  }

  /**
   * Assert the job detail panel is visible
   */
  async expectDetailPanelVisible(): Promise<void> {
    await this.waitForElement(this.jobDetailPanel);
  }

  /**
   * Assert the "Testa Agent" button is visible
   */
  async expectTestAgentButtonVisible(): Promise<void> {
    await this.waitForElement(this.testAgentButton);
  }

  /**
   * Assert the job list has at least one entry
   */
  async expectJobListNotEmpty(): Promise<void> {
    await this.waitForJobListLoaded();
    await expect(this.jobList.locator('[data-testid="job-row"]').first()).toBeVisible();
  }
}
