import { Page, Locator } from '@playwright/test';

import { IUploadPage } from '../../types/pom-interfaces';
import { BasePage } from '../base/BasePage';

/**
 * UploadPage - Page Object Model for PDF upload functionality
 *
 * Handles the complete upload workflow:
 * - Game selection (new or existing)
 * - PDF file upload with validation
 * - Processing status monitoring
 * - Success/error handling
 * - Automatic navigation to chat after successful upload
 *
 * Usage:
 *   const uploadPage = new UploadPage(page);
 *   await uploadPage.goto();
 *   await uploadPage.selectOrCreateGame('Chess');
 *   await uploadPage.uploadPdf('/path/to/rules.pdf');
 *   await uploadPage.waitForUploadSuccess();
 */
export class UploadPage extends BasePage implements IUploadPage {
  // Locators for game selection
  private readonly gameSelect: Locator;
  private readonly newGameNameInput: Locator;
  private readonly createGameButton: Locator;
  private readonly confirmGameButton: Locator;

  // Locators for file upload
  private readonly fileInput: Locator;
  private readonly languageSelect: Locator;
  private readonly uploadButton: Locator;

  // Locators for status and messages
  private readonly uploadingIndicator: Locator;
  private readonly processingIndicator: Locator;
  private readonly successMessage: Locator;
  private readonly errorMessage: Locator;
  private readonly validationError: Locator;

  // Locators for upload queue (multi-file upload)
  private readonly uploadQueueItem: Locator;
  private readonly uploadQueueSuccess: Locator;
  private readonly uploadQueueError: Locator;

  constructor(page: Page) {
    super(page);

    // Game selection elements
    this.gameSelect = this.page.getByRole('combobox', { name: /select game|game/i });
    // GamePicker has: id="new-game", label "Create New Game", placeholder "e.g., Gloomhaven"
    this.newGameNameInput = this.page
      .locator('#new-game')
      .or(this.page.getByLabel(/create new game/i));
    // GamePicker button text is just "Create" (not "Create Game")
    this.createGameButton = this.page.getByRole('button', { name: /^create$/i });
    this.confirmGameButton = this.page.getByRole('button', { name: /confirm|proceed/i });

    // File upload elements
    this.fileInput = this.page.locator('input[type="file"]');
    this.languageSelect = this.page.getByRole('combobox', { name: /language/i });
    this.uploadButton = this.page.getByRole('button', { name: /upload|carica/i });

    // Status indicators
    this.uploadingIndicator = this.page.getByText(/uploading|caricamento/i);
    this.processingIndicator = this.page.getByText(/processing|elaborazione/i);
    this.successMessage = this.page.getByText(
      /success|uploaded successfully|caricato con successo/i
    );
    this.errorMessage = this.page.locator('[role="alert"]');
    this.validationError = this.page.locator('.validation-error, [data-testid="validation-error"]');

    // Upload queue elements (for multi-file upload)
    this.uploadQueueItem = this.page.locator('[data-testid="upload-queue-item"]');
    this.uploadQueueSuccess = this.page.locator('[data-testid="upload-success"]');
    this.uploadQueueError = this.page.locator('[data-testid="upload-error"]');
  }

  /**
   * Navigate to the upload page
   */
  async goto(): Promise<void> {
    await this.page.goto('/upload');
    await this.waitForLoad();
  }

  /**
   * Select an existing game from the dropdown
   * @param gameName - Name of the game to select
   */
  async selectGame(gameName: string): Promise<void> {
    await this.waitForElement(this.gameSelect);
    await this.selectOption(this.gameSelect, gameName);

    // Wait for confirmation if needed
    if (await this.isVisible(this.confirmGameButton)) {
      await this.click(this.confirmGameButton);
    }
  }

  /**
   * Create a new game
   * @param gameName - Name of the new game
   */
  async createNewGame(gameName: string): Promise<void> {
    await this.waitForElement(this.newGameNameInput);
    await this.fill(this.newGameNameInput, gameName);
    await this.click(this.createGameButton);

    // Wait for game creation to complete
    await this.page.waitForResponse(
      resp => resp.url().includes('/api/v1/games') && resp.status() === 201,
      { timeout: 10000 }
    );
  }

  /**
   * Select or create a game (convenience method)
   * @param gameName - Name of the game (will create if doesn't exist)
   * @param createNew - Force creation of new game even if name exists
   */
  async selectOrCreateGame(gameName: string, createNew = false): Promise<void> {
    if (createNew) {
      await this.createNewGame(gameName);
    } else {
      // Try to select existing game, fall back to creating if not found
      try {
        await this.selectGame(gameName);
      } catch {
        await this.createNewGame(gameName);
      }
    }
  }

  /**
   * Select language for PDF processing
   * @param languageCode - ISO 639-1 language code (e.g., 'en', 'it', 'fr')
   */
  async selectLanguage(languageCode: string): Promise<void> {
    if (await this.isVisible(this.languageSelect)) {
      await this.selectOption(this.languageSelect, languageCode);
    }
  }

  /**
   * Upload a PDF file
   * @param filePath - Absolute path to the PDF file
   * @param options - Upload options (language, autoWait)
   */
  async uploadPdf(
    filePath: string,
    options: { language?: string; autoWait?: boolean } = {}
  ): Promise<void> {
    const { language = 'en', autoWait = true } = options;

    // Select language if specified
    if (language !== 'en') {
      await this.selectLanguage(language);
    }

    // Upload file
    await this.uploadFile(this.fileInput, filePath);

    // Wait for upload to start (file input should trigger automatic upload)
    if (autoWait) {
      await this.waitForUploadToStart();
    }
  }

  /**
   * Wait for upload process to start
   */
  private async waitForUploadToStart(): Promise<void> {
    // Wait for either uploading indicator or upload request
    await Promise.race([
      this.waitForElement(this.uploadingIndicator, { timeout: 5000 }),
      this.page.waitForResponse(
        resp => resp.url().includes('/api/v1/pdfs') && resp.request().method() === 'POST',
        { timeout: 5000 }
      ),
    ]);
  }

  /**
   * Wait for upload and processing to complete successfully
   * This method waits for:
   * 1. Upload request to complete
   * 2. Processing to finish
   * 3. Success message or automatic redirect to chat
   */
  async waitForUploadSuccess(): Promise<void> {
    // Wait for upload API call to complete
    const uploadResponse = await this.page.waitForResponse(
      resp => resp.url().includes('/api/v1/pdfs') && resp.request().method() === 'POST',
      { timeout: 30000 }
    );

    if (!uploadResponse.ok()) {
      throw new Error(`Upload failed with status ${uploadResponse.status()}`);
    }

    // Wait for processing to complete (polls /api/v1/pdfs/{id}/processing)
    // The frontend polls every 2 seconds, so wait up to 60 seconds for processing
    await this.page.waitForResponse(
      resp => {
        if (!resp.url().includes('/processing')) return false;
        if (resp.status() !== 200) return false;

        // Check if response indicates completion
        resp
          .json()
          .then(data => {
            return data.processingStatus === 'completed';
          })
          .catch(() => false);

        return true;
      },
      { timeout: 60000 }
    );

    // Wait for success indicator (message or redirect to chat)
    await Promise.race([
      this.waitForElement(this.successMessage, { timeout: 5000 }),
      this.waitForElement(this.uploadQueueSuccess, { timeout: 5000 }),
      this.waitForUrl(/\/chat/), // Auto-redirect to chat
    ]);
  }

  /**
   * Get the uploaded game name from the UI
   * @returns Game name or null if not found
   */
  async getUploadedGameName(): Promise<string | null> {
    try {
      // Try to get from selected game
      const selectedOption = await this.gameSelect.locator('option:checked').textContent();
      if (selectedOption) {
        return selectedOption.trim();
      }

      // Try to get from new game input
      const newGameName = await this.newGameNameInput.inputValue();
      if (newGameName) {
        return newGameName.trim();
      }

      return null;
    } catch {
      return null;
    }
  }

  /**
   * Get error message if upload failed
   * @returns Error message or null if no error
   */
  async getErrorMessage(): Promise<string | null> {
    try {
      // Check for validation errors first
      if (await this.isVisible(this.validationError)) {
        return await this.validationError.textContent();
      }

      // Check for general error messages
      if (await this.isVisible(this.errorMessage)) {
        return await this.errorMessage.textContent();
      }

      // Check upload queue for errors
      if (await this.isVisible(this.uploadQueueError)) {
        return await this.uploadQueueError.textContent();
      }

      return null;
    } catch {
      return null;
    }
  }

  /**
   * Check if upload is currently in progress
   * @returns True if uploading/processing, false otherwise
   */
  async isUploading(): Promise<boolean> {
    return (
      (await this.isVisible(this.uploadingIndicator)) ||
      (await this.isVisible(this.processingIndicator))
    );
  }

  /**
   * Wait for processing to complete (for existing uploads)
   * @param documentId - PDF document ID to monitor
   */
  async waitForProcessingComplete(documentId: string): Promise<void> {
    await this.page.waitForResponse(
      resp => {
        if (!resp.url().includes(`/pdfs/${documentId}/processing`)) return false;
        if (resp.status() !== 200) return false;

        resp
          .json()
          .then(data => {
            return data.processingStatus === 'completed';
          })
          .catch(() => false);

        return true;
      },
      { timeout: 60000 }
    );
  }

  /**
   * Get current upload/processing status
   * @returns Status text or null
   */
  async getStatusText(): Promise<string | null> {
    if (await this.isVisible(this.uploadingIndicator)) {
      return await this.uploadingIndicator.textContent();
    }

    if (await this.isVisible(this.processingIndicator)) {
      return await this.processingIndicator.textContent();
    }

    if (await this.isVisible(this.successMessage)) {
      return await this.successMessage.textContent();
    }

    return null;
  }
}
