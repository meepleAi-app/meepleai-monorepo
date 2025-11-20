import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from '../base/BasePage';
import { IChatPage, ChatSource } from '../../types/pom-interfaces';
import { getTextMatcher } from '../../fixtures/i18n';

/**
 * ChatPage - Chat interface interactions
 *
 * Handles chat functionality:
 * - Ask questions and receive streaming answers
 * - View and interact with citations
 * - Like/dislike answers
 * - Edit and delete messages
 * - Export conversation
 * - Switch game context
 *
 * Usage:
 *   const chatPage = new ChatPage(page);
 *   await chatPage.goto();
 *   await chatPage.askQuestionAndWait('How do I castle in chess?');
 *   const answer = await chatPage.getLastAnswer();
 */
export class ChatPage extends BasePage implements IChatPage {
  // ========================================================================
  // Locators
  // ========================================================================

  /**
   * Chat page heading
   */
  private get chatHeading(): Locator {
    return this.page.getByRole('heading', { name: getTextMatcher('chat.title') });
  }

  /**
   * Question input field
   */
  private get questionInput(): Locator {
    return this.page.getByPlaceholder(getTextMatcher('chat.placeholder'));
  }

  /**
   * Send button
   */
  private get sendButton(): Locator {
    return this.page.getByRole('button', { name: getTextMatcher('chat.send') });
  }

  /**
   * Messages container (all chat messages)
   */
  private get messagesContainer(): Locator {
    return this.page.locator('[data-testid="chat-messages"]');
  }

  /**
   * Streaming indicator (spinner during answer generation)
   */
  private get streamingIndicator(): Locator {
    return this.page.locator('[data-testid="streaming-indicator"]');
  }

  /**
   * Login modal heading shown after middleware redirect
   */
  private get loginModalHeading(): Locator {
    return this.page.getByRole('heading', { name: getTextMatcher('auth.login.signInTo') });
  }

  /**
   * Login and register tabs inside the auth modal
   */
  private get loginTab(): Locator {
    return this.page.getByRole('tab', { name: getTextMatcher('navigation.login') });
  }

  private get registerTab(): Locator {
    return this.page.getByRole('tab', { name: getTextMatcher('navigation.register') });
  }

  /**
   * Inline unauthenticated state
   */
  private get unauthenticatedHeading(): Locator {
    return this.page.getByRole('heading', { name: getTextMatcher('chat.loginRequired') });
  }

  private get unauthenticatedMessage(): Locator {
    return this.page.getByText(getTextMatcher('chat.loginRequiredMessage'));
  }

  private get goToLoginLink(): Locator {
    return this.page.getByRole('link', { name: getTextMatcher('chat.goToLogin') });
  }

  /**
   * Game selection dropdown
   */
  private get gameSelect(): Locator {
    return this.page.locator('select#gameSelect');
  }

  /**
   * Export button
   */
  private get exportButton(): Locator {
    return this.page.getByRole('button', { name: /export|esporta/i });
  }

  // ========================================================================
  // Navigation
  // ========================================================================

  /**
   * Navigate to chat page
   */
  async goto(): Promise<void> {
    await this.page.goto('/chat');
    await this.waitForLoad();
  }

  // ========================================================================
  // Question/Answer Actions
  // ========================================================================

  /**
   * Type question and click send button
   * @param question - Question text
   */
  async askQuestion(question: string): Promise<void> {
    await this.fill(this.questionInput, question);
    await this.click(this.sendButton);
  }

  /**
   * Wait for streaming answer to complete
   * Waits for streaming indicator to disappear
   */
  async waitForAnswer(): Promise<void> {
    await this.waitForElementToDisappear(this.streamingIndicator);
  }

  /**
   * Ask question and wait for answer (composite method)
   * @param question - Question text
   */
  async askQuestionAndWait(question: string): Promise<void> {
    await this.askQuestion(question);
    await this.waitForAnswer();
  }

  /**
   * Get text of last answer in chat
   * @returns Answer text
   */
  async getLastAnswer(): Promise<string> {
    const lastMessage = this.messagesContainer.locator('.answer').last();
    return (await lastMessage.textContent()) || '';
  }

  // ========================================================================
  // Citation Actions
  // ========================================================================

  /**
   * Get all citations from the last answer
   * @returns Array of citation objects with title and page number
   */
  async getCitations(): Promise<ChatSource[]> {
    const citations = await this.messagesContainer
      .locator('[data-testid="citation"]')
      .all();

    return Promise.all(
      citations.map(async (citation) => {
        const text = await citation.textContent();
        // Parse "Title (Pagina 5)" format
        const match = text?.match(/(.+?)\s*\(Pagina\s+(\d+)\)/);
        return {
          title: match?.[1] || text || '',
          page: match?.[2] ? parseInt(match[2]) : undefined,
        };
      })
    );
  }

  /**
   * Click a citation (opens PDF viewer or navigates to page)
   * @param index - Citation index (0-based)
   */
  async clickCitation(index: number): Promise<void> {
    const citation = this.messagesContainer
      .locator('[data-testid="citation"]')
      .nth(index);
    await this.click(citation);
  }

  // ========================================================================
  // Feedback Actions
  // ========================================================================

  /**
   * Click "Like" button on last answer
   */
  async likeAnswer(): Promise<void> {
    const likeButton = this.messagesContainer
      .last()
      .getByRole('button', { name: /👍|utile/i });
    await this.click(likeButton);
  }

  /**
   * Click "Dislike" button on last answer
   */
  async dislikeAnswer(): Promise<void> {
    const dislikeButton = this.messagesContainer
      .last()
      .getByRole('button', { name: /👎|non utile/i });
    await this.click(dislikeButton);
  }

  /**
   * Check if like button is active (background color changed)
   * @returns True if like button is active
   */
  async isLikeActive(): Promise<boolean> {
    const likeButton = this.messagesContainer
      .last()
      .getByRole('button', { name: /👍|utile/i });
    const bgColor = await likeButton.evaluate((el) =>
      window.getComputedStyle(el).backgroundColor
    );
    // Green background: rgb(52, 168, 83)
    return bgColor === 'rgb(52, 168, 83)';
  }

  // ========================================================================
  // Message Management Actions
  // ========================================================================

  /**
   * Edit a message (question) in the chat
   * @param messageIndex - Message index (0-based)
   * @param newText - New message text
   */
  async editMessage(messageIndex: number, newText: string): Promise<void> {
    const message = this.messagesContainer.locator('.message').nth(messageIndex);
    await this.click(message.getByRole('button', { name: /edit|modifica/i }));
    await this.fill(message.getByRole('textbox'), newText);
    await this.click(message.getByRole('button', { name: /save|salva/i }));
  }

  /**
   * Delete a message (with confirmation)
   * @param messageIndex - Message index (0-based)
   */
  async deleteMessage(messageIndex: number): Promise<void> {
    const message = this.messagesContainer.locator('.message').nth(messageIndex);
    await this.click(message.getByRole('button', { name: /delete|elimina/i }));

    // Confirm deletion in modal
    await this.click(this.page.getByRole('button', { name: /confirm|conferma/i }));
  }

  /**
   * Get message count in current conversation
   * @returns Number of messages (questions + answers)
   */
  async getMessageCount(): Promise<number> {
    return await this.messagesContainer.locator('.message').count();
  }

  // ========================================================================
  // Export Actions
  // ========================================================================

  /**
   * Export conversation in specified format
   * @param format - Export format ('json' or 'txt')
   */
  async exportConversation(format: 'json' | 'txt'): Promise<void> {
    await this.click(this.exportButton);
    await this.click(
      this.page.getByRole('button', { name: new RegExp(format, 'i') })
    );
  }

  // ========================================================================
  // Context Switching Actions
  // ========================================================================

  /**
   * Select a different game for chat context
   * @param gameName - Game name (must match option value)
   */
  async selectGame(gameName: string): Promise<void> {
    await this.selectOption(this.gameSelect, gameName);
  }

  /**
   * Get currently selected game
   * @returns Selected game name
   */
  async getSelectedGame(): Promise<string> {
    return await this.gameSelect.inputValue();
  }

  /**
   * Wait for game context to be loaded
   * Waits for game select to be visible and populated
   */
  async waitForGameContext(): Promise<void> {
    await this.waitForElement(this.gameSelect, { timeout: 10000 });
    // Wait for at least one option to be available
    await this.page.waitForFunction(
      () => {
        const select = document.querySelector<HTMLSelectElement>('[aria-label*="game" i], [aria-label*="gioco" i]');
        return select && select.options.length > 0;
      },
      { timeout: 10000 }
    );
  }

  /**
   * Get the active game name from UI
   * @returns Currently active game name or null if none selected
   */
  async getActiveGameName(): Promise<string | null> {
    try {
      const selectedValue = await this.getSelectedGame();
      if (selectedValue) {
        // Get the text content of the selected option
        const selectedOption = await this.gameSelect.locator(`option[value="${selectedValue}"]`).textContent();
        return selectedOption?.trim() || selectedValue;
      }
      return null;
    } catch {
      return null;
    }
  }

  /**
   * Wait for streaming response to complete
   * Alternative to waitForAnswer() with explicit streaming check
   */
  async waitForResponseComplete(): Promise<void> {
    // Wait for streaming indicator to appear (if it hasn't already)
    const hasStreamingIndicator = await this.isVisible(this.streamingIndicator);

    if (hasStreamingIndicator) {
      // Wait for streaming to complete
      await this.waitForElementToDisappear(this.streamingIndicator, { timeout: 60000 });
    } else {
      // If no streaming indicator, wait for send button to be re-enabled
      await this.page.waitForFunction(
        () => {
          const sendBtn = document.querySelector('button[type="submit"]') as HTMLButtonElement;
          return sendBtn && !sendBtn.disabled;
        },
        { timeout: 60000 }
      );
    }
  }

  /**
   * Get the complete response text from the last answer
   * Handles both streaming and completed responses
   * @returns Response text
   */
  async getResponseText(): Promise<string> {
    // Wait for response to be complete first
    await this.waitForResponseComplete();

    // Get last answer
    return await this.getLastAnswer();
  }

  // ========================================================================
  // Assertions
  // ========================================================================

  /**
   * Assert chat page is visible
   */
  async assertChatPageVisible(): Promise<void> {
    await this.waitForElement(this.chatHeading);
  }

  /**
   * Assert login required message is shown (unauthenticated state)
   * @returns Which guard variant was displayed
   */
  async assertLoginRequired(): Promise<'modal' | 'inline'> {
    await this.page.waitForLoadState('networkidle');

    if (this.page.url().includes('/login')) {
      await this.waitForElement(this.loginModalHeading);
      await expect(this.loginTab).toHaveAttribute('aria-selected', 'true');
      await expect(this.registerTab).toBeVisible();
      return 'modal';
    }

    await this.waitForElement(this.unauthenticatedHeading);
    await expect(this.unauthenticatedMessage).toBeVisible();
    await expect(this.goToLoginLink).toBeVisible();
    return 'inline';
  }

  /**
   * Clicks "Go to Login" in the inline auth gate
   */
  async goToLoginFromGate(): Promise<void> {
    await this.click(this.goToLoginLink);
  }

  /**
   * Assert answer contains specific text
   * @param text - Expected text in answer
   */
  async assertAnswerContains(text: string): Promise<void> {
    await this.waitForElement(this.messagesContainer.getByText(new RegExp(text)));
  }

  /**
   * Assert citation with specific title is visible
   * @param title - Citation title
   */
  async assertCitationVisible(title: string): Promise<void> {
    await this.waitForElement(
      this.messagesContainer.getByText(new RegExp(title))
    );
  }

  /**
   * Assert specific number of messages in chat
   * @param count - Expected message count
   */
  async assertMessageCount(count: number): Promise<void> {
    const messages = this.messagesContainer.locator('.message');
    await expect(messages).toHaveCount(count);
  }

  /**
   * Assert streaming is active (indicator visible)
   */
  async assertStreaming(): Promise<void> {
    await this.waitForElement(this.streamingIndicator);
  }

  /**
   * Assert streaming is complete (indicator hidden)
   */
  async assertStreamingComplete(): Promise<void> {
    await this.waitForElementToDisappear(this.streamingIndicator);
  }
}
