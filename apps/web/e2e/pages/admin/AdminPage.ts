import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from '../base/BasePage';

/**
 * AdminPage - Base admin interface interactions
 *
 * Provides common admin functionality:
 * - Navigation to admin sections
 * - Bulk operations
 * - Data export/import
 * - Admin-specific UI patterns
 *
 * Usage:
 *   const adminPage = new AdminPage(page);
 *   await adminPage.gotoPrompts();
 *   await adminPage.gotoBulkExport();
 */
export class AdminPage extends BasePage {
  // ========================================================================
  // Navigation
  // ========================================================================

  /**
   * Navigate to admin dashboard
   */
  async goto(): Promise<void> {
    await this.page.goto('/admin');
    await this.waitForLoad();
  }

  /**
   * Navigate to prompt management section
   */
  async gotoPrompts(): Promise<void> {
    await this.page.goto('/admin/prompts');
    await this.waitForLoad();
  }

  /**
   * Navigate to bulk export section
   */
  async gotoBulkExport(): Promise<void> {
    await this.page.goto('/admin/bulk-export');
    await this.waitForLoad();
  }

  /**
   * Navigate to user management section
   */
  async gotoUsers(): Promise<void> {
    await this.page.goto('/admin/users');
    await this.waitForLoad();
  }

  /**
   * Navigate to analytics section
   */
  async gotoAnalytics(): Promise<void> {
    await this.page.goto('/admin/analytics');
    await this.waitForLoad();
  }

  /**
   * Navigate to configuration section
   */
  async gotoConfiguration(): Promise<void> {
    await this.page.goto('/admin/configuration');
    await this.waitForLoad();
  }

  // ========================================================================
  // Common Locators
  // ========================================================================

  /**
   * Admin dashboard heading
   */
  private get adminHeading(): Locator {
    return this.page.getByRole('heading', { name: /admin/i });
  }

  /**
   * Success message toast/banner
   */
  protected get successMessage(): Locator {
    return this.page.locator('[role="status"], [data-testid="success-message"]');
  }

  /**
   * Error message toast/banner
   */
  protected get errorMessage(): Locator {
    return this.page.locator('[role="alert"], [data-testid="error-message"]');
  }

  /**
   * Loading spinner
   */
  protected get loadingIndicator(): Locator {
    return this.page.locator('[data-testid="loading"], .spinner');
  }

  // ========================================================================
  // Common Actions
  // ========================================================================

  /**
   * Wait for loading to complete
   */
  async waitForLoading(): Promise<void> {
    await this.waitForElementToDisappear(this.loadingIndicator);
  }

  /**
   * Assert success message is displayed
   * @param expectedText - Expected success message (optional)
   */
  async assertSuccess(expectedText?: string): Promise<void> {
    await this.waitForElement(this.successMessage);
    if (expectedText) {
      await expect(this.successMessage).toContainText(expectedText);
    }
  }

  /**
   * Assert error message is displayed
   * @param expectedText - Expected error message (optional)
   */
  async assertError(expectedText?: string): Promise<void> {
    await this.waitForElement(this.errorMessage);
    if (expectedText) {
      await expect(this.errorMessage).toContainText(expectedText);
    }
  }

  /**
   * Assert admin page is visible
   */
  async assertAdminPageVisible(): Promise<void> {
    await this.waitForElement(this.adminHeading);
  }
}

/**
 * PromptManagementPage - Prompt template management
 */
export class PromptManagementPage extends AdminPage {
  // ========================================================================
  // Locators
  // ========================================================================

  /**
   * Prompt templates list
   */
  private get promptList(): Locator {
    return this.page.locator('[data-testid="prompt-list"]');
  }

  /**
   * Search input for filtering prompts
   */
  private get searchInput(): Locator {
    return this.page.getByPlaceholder(/search.*prompt|cerca/i);
  }

  /**
   * Category filter dropdown
   */
  private get categoryFilter(): Locator {
    return this.page.getByLabel(/category|categoria/i);
  }

  /**
   * Create new prompt button
   */
  private get createButton(): Locator {
    return this.page.getByRole('button', { name: /create.*prompt|new.*prompt|crea/i });
  }

  /**
   * Monaco editor (for prompt editing)
   */
  private get monacoEditor(): Locator {
    return this.page.locator('.monaco-editor');
  }

  /**
   * Save button
   */
  private get saveButton(): Locator {
    return this.page.getByRole('button', { name: /save|salva/i });
  }

  /**
   * Activate version button
   */
  private get activateButton(): Locator {
    return this.page.getByRole('button', { name: /activate|attiva/i });
  }

  // ========================================================================
  // Navigation
  // ========================================================================

  /**
   * Navigate to prompt management page
   */
  async goto(): Promise<void> {
    await this.gotoPrompts();
  }

  /**
   * Navigate to specific prompt detail page
   * @param promptId - Prompt template ID
   */
  async gotoPromptDetail(promptId: string): Promise<void> {
    await this.page.goto(`/admin/prompts/${promptId}`);
    await this.waitForLoad();
  }

  /**
   * Navigate to create new version page
   * @param promptId - Prompt template ID
   */
  async gotoNewVersion(promptId: string): Promise<void> {
    await this.page.goto(`/admin/prompts/${promptId}/versions/new`);
    await this.waitForLoad();
  }

  /**
   * Navigate to version detail page
   * @param promptId - Prompt template ID
   * @param versionId - Version ID
   */
  async gotoVersionDetail(promptId: string, versionId: string): Promise<void> {
    await this.page.goto(`/admin/prompts/${promptId}/versions/${versionId}`);
    await this.waitForLoad();
  }

  /**
   * Navigate to version comparison page
   * @param promptId - Prompt template ID
   */
  async gotoVersionComparison(promptId: string): Promise<void> {
    await this.page.goto(`/admin/prompts/${promptId}/compare`);
    await this.waitForLoad();
  }

  /**
   * Navigate to audit log page
   * @param promptId - Prompt template ID
   */
  async gotoAuditLog(promptId: string): Promise<void> {
    await this.page.goto(`/admin/prompts/${promptId}/audit`);
    await this.waitForLoad();
  }

  // ========================================================================
  // Actions
  // ========================================================================

  /**
   * Search for prompts by name
   * @param query - Search query
   */
  async searchPrompts(query: string): Promise<void> {
    await this.fill(this.searchInput, query);
    await this.page.waitForTimeout(500); // Wait for debounce
  }

  /**
   * Filter prompts by category
   * @param category - Category name
   */
  async filterByCategory(category: string): Promise<void> {
    await this.selectOption(this.categoryFilter, category);
  }

  /**
   * Click on a prompt template in the list
   * @param templateName - Template name
   */
  async clickPromptTemplate(templateName: string): Promise<void> {
    const template = this.promptList.getByText(templateName);
    await this.click(template);
  }

  /**
   * Create a new prompt version with Monaco editor
   * @param content - Prompt content
   */
  async createVersion(content: string): Promise<void> {
    // Wait for Monaco editor to load
    await this.waitForElement(this.monacoEditor);

    // Monaco requires special handling - use keyboard input
    await this.monacoEditor.click();
    await this.page.keyboard.press('Control+A');
    await this.page.keyboard.type(content);

    // Save
    await this.click(this.saveButton);
  }

  /**
   * Activate a specific version
   */
  async activateVersion(): Promise<void> {
    await this.click(this.activateButton);
  }

  /**
   * Get version history entries
   * @returns Array of version objects
   */
  async getVersionHistory(): Promise<Array<{ version: number; active: boolean }>> {
    const rows = await this.page.locator('[data-testid="version-row"]').all();
    const versions = [];

    for (const row of rows) {
      const versionText = await row.locator('[data-testid="version-number"]').textContent();
      const isActive = await row.locator('[data-testid="active-badge"]').isVisible().catch(() => false);

      versions.push({
        version: parseInt(versionText || '0'),
        active: isActive
      });
    }

    return versions;
  }

  /**
   * Get audit log entries
   * @returns Number of audit log entries
   */
  async getAuditLogCount(): Promise<number> {
    return await this.page.locator('[data-testid="audit-entry"]').count();
  }

  // ========================================================================
  // Assertions
  // ========================================================================

  /**
   * Assert prompt list is visible
   */
  async assertPromptListVisible(): Promise<void> {
    await this.waitForElement(this.promptList);
  }

  /**
   * Assert specific prompt template exists
   * @param templateName - Template name
   */
  async assertPromptExists(templateName: string): Promise<void> {
    await expect(this.promptList.getByText(templateName)).toBeVisible();
  }

  /**
   * Assert Monaco editor is loaded
   */
  async assertMonacoLoaded(): Promise<void> {
    await this.waitForElement(this.monacoEditor);
  }

  /**
   * Assert version is active
   * @param version - Version number
   */
  async assertVersionActive(version: number): Promise<void> {
    const versionRow = this.page.locator(`[data-testid="version-row"][data-version="${version}"]`);
    await expect(versionRow.locator('[data-testid="active-badge"]')).toBeVisible();
  }
}

/**
 * BulkExportPage - Bulk RuleSpec export functionality
 */
export class BulkExportPage extends AdminPage {
  // ========================================================================
  // Locators
  // ========================================================================

  /**
   * Game list with checkboxes
   */
  private get gameList(): Locator {
    return this.page.locator('[data-testid="game-list"]');
  }

  /**
   * Select all checkbox
   */
  private get selectAllCheckbox(): Locator {
    return this.page.getByRole('checkbox', { name: /select.*all|tutti/i });
  }

  /**
   * Export button
   */
  private get exportButton(): Locator {
    return this.page.getByRole('button', { name: /export|esporta/i });
  }

  /**
   * Progress indicator during export
   */
  private get progressIndicator(): Locator {
    return this.page.locator('[data-testid="export-progress"]');
  }

  // ========================================================================
  // Navigation
  // ========================================================================

  /**
   * Navigate to bulk export page
   */
  async goto(): Promise<void> {
    await this.gotoBulkExport();
  }

  // ========================================================================
  // Actions
  // ========================================================================

  /**
   * Select a specific game by name
   * @param gameName - Game name
   */
  async selectGame(gameName: string): Promise<void> {
    const gameCheckbox = this.gameList.locator(`[data-game-name="${gameName}"]`).getByRole('checkbox');
    await this.click(gameCheckbox);
  }

  /**
   * Toggle select all games
   */
  async selectAll(): Promise<void> {
    await this.click(this.selectAllCheckbox);
  }

  /**
   * Trigger export and wait for download
   * @returns Download promise
   */
  async exportGames() {
    const downloadPromise = this.page.waitForEvent('download');
    await this.click(this.exportButton);
    return downloadPromise;
  }

  /**
   * Get count of selected games
   * @returns Number of selected games
   */
  async getSelectedCount(): Promise<number> {
    const checkboxes = await this.gameList.getByRole('checkbox', { checked: true }).all();
    return checkboxes.length - 1; // Exclude "select all" checkbox
  }

  /**
   * Get total game count
   * @returns Total number of games
   */
  async getTotalGameCount(): Promise<number> {
    return await this.gameList.locator('[data-testid="game-row"]').count();
  }

  // ========================================================================
  // Assertions
  // ========================================================================

  /**
   * Assert game list is visible
   */
  async assertGameListVisible(): Promise<void> {
    await this.waitForElement(this.gameList);
  }

  /**
   * Assert export button is disabled
   */
  async assertExportDisabled(): Promise<void> {
    await expect(this.exportButton).toBeDisabled();
  }

  /**
   * Assert export button is enabled
   */
  async assertExportEnabled(): Promise<void> {
    await expect(this.exportButton).toBeEnabled();
  }

  /**
   * Assert progress indicator is visible
   */
  async assertProgressVisible(): Promise<void> {
    await this.waitForElement(this.progressIndicator);
  }

  /**
   * Assert specific game is selected
   * @param gameName - Game name
   */
  async assertGameSelected(gameName: string): Promise<void> {
    const checkbox = this.gameList.locator(`[data-game-name="${gameName}"]`).getByRole('checkbox');
    await expect(checkbox).toBeChecked();
  }
}
