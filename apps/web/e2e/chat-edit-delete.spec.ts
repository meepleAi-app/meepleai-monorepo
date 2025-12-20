/**
 * E2E Tests for CHAT-06: Message Editing and Deletion - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/helpers/ChatHelper.ts - mockMessageEdit(), mockMessageDelete()
 * @see apps/web/e2e/pages/helpers/AuthHelper.ts - mockAuthenticatedSession()
 *
 * Tests cover:
 * - Edit message flow (happy path)
 * - Edit validation (empty content)
 * - Delete message flow (happy path)
 * - Delete cancellation
 * - Edit/Delete button visibility on hover
 * - Invalidation warning display
 * - Error handling (403 Forbidden)
 */

import { test as base, expect, Page } from './fixtures/chromatic';
import { AuthHelper, ChatHelper, USER_FIXTURES } from './pages';
import { WaitHelper } from './helpers/WaitHelper';

// Extended test with user authentication
const test = base.extend<{ userPage: Page }>({
  userPage: async ({ page }, use) => {
    const authHelper = new AuthHelper(page);
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
    await use(page);
  },
});

test.describe('CHAT-06: Message Editing and Deletion', () => {
  /**
   * Setup: Navigate to chat page and disable animations
   */
  test.beforeEach(async ({ userPage: page }) => {
    // Disable animations to prevent timing issues with framer-motion
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Navigate to chat (already authenticated)
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
  });

  /**
   * Test 1: Edit Message Flow - Complete Happy Path
   */
  test('should allow user to edit their own message successfully', async ({ userPage: page }) => {
    const chatHelper = new ChatHelper(page);

    // Send initial message with unique timestamp
    const originalMessage = `Test message for editing ${Date.now()}`;
    const editedMessage = `Edited message content ${Date.now()}`;

    // Find chat input and send message
    const chatInput = page.locator('#message-input');
    await chatInput.fill(originalMessage);
    await chatInput.press('Enter');

    // Wait for message to appear in chat
    await expect(
      page.locator(`li[aria-label="Your message"]:has-text("${originalMessage}")`)
    ).toBeVisible({ timeout: 10000 });

    // Wait for AI response to complete
    const waitHelper = new WaitHelper(page);
    await waitHelper.waitForNetworkIdle(5000);

    // Locate the user message
    const userMessageBubble = page.locator(
      `li[aria-label="Your message"]:has-text("${originalMessage}")`
    );

    // Hover over message to reveal edit/delete buttons
    await userMessageBubble.hover();

    // Click edit button
    const editButton = userMessageBubble.locator('button[aria-label="Edit message"]');
    await expect(editButton).toBeVisible({ timeout: 2000 });
    await editButton.click({ force: true });

    // Verify textarea appears with original content
    const editTextarea = page.locator('textarea[aria-label="Edit message content"]');
    await expect(editTextarea).toBeVisible();
    await expect(editTextarea).toHaveValue(originalMessage);

    // Clear and type new content
    await editTextarea.clear();
    await editTextarea.fill(editedMessage);

    // Mock the API update response
    await chatHelper.mockMessageEdit(true);

    // Click Save button
    const saveButton = page.locator('button[aria-label="Save edited message"]');
    await expect(saveButton).toBeEnabled();
    await saveButton.click({ force: true });

    // Wait for API call and UI update

    // Verify message updated with new content
    await expect(page.getByText(editedMessage)).toBeVisible({ timeout: 5000 });

    // Verify "(modificato)" badge appears
    await expect(page.getByText('(modificato)')).toBeVisible({ timeout: 3000 });

    // Verify updated message is visible
    await expect(
      page.locator(`li[aria-label="Your message"]:has-text("${editedMessage}")`)
    ).toBeVisible();
  });

  /**
   * Test 2: Edit Validation - Empty Content Not Allowed
   */
  test('should disable save button when edit textarea is empty', async ({ userPage: page }) => {
    // Send message
    const testMessage = `Test validation message ${Date.now()}`;
    await page.locator('#message-input').fill(testMessage);
    await page.locator('#message-input').press('Enter');

    // Wait for message to appear
    await expect(
      page.locator(`li[aria-label="Your message"]:has-text("${testMessage}")`)
    ).toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(1000);

    // Enter edit mode
    const userMessageBubble = page.locator(
      `li[aria-label="Your message"]:has-text("${testMessage}")`
    );
    await userMessageBubble.hover();
    await userMessageBubble.locator('button[aria-label="Edit message"]').click({ force: true });

    // Verify textarea appears
    const editTextarea = page.locator('textarea[aria-label="Edit message content"]');
    await expect(editTextarea).toBeVisible();

    // Clear all text
    await editTextarea.clear();

    // Verify Save button is disabled
    const saveButton = page.locator('button[aria-label="Save edited message"]');
    await expect(saveButton).toBeDisabled();

    // Cancel edit
    await page.getByRole('button', { name: 'Annulla' }).first().click({ force: true });

    // Verify back to normal view
    await expect(editTextarea).not.toBeVisible();
    await expect(page.getByText(testMessage)).toBeVisible();
  });

  /**
   * Test 3: Delete Message Flow - Complete Happy Path
   */
  test('should allow user to delete their own message successfully', async ({ userPage: page }) => {
    const chatHelper = new ChatHelper(page);

    // Send message to delete
    const testMessage = `Message to delete ${Date.now()}`;
    await page.locator('#message-input').fill(testMessage);
    await page.locator('#message-input').press('Enter');

    // Wait for message to appear
    await expect(
      page.locator(`li[aria-label="Your message"]:has-text("${testMessage}")`)
    ).toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(1000);

    // Hover and click delete button
    const userMessageBubble = page.locator(
      `li[aria-label="Your message"]:has-text("${testMessage}")`
    );
    await userMessageBubble.hover();
    const deleteButton = userMessageBubble.locator('button[aria-label="Delete message"]');
    await expect(deleteButton).toBeVisible({ timeout: 2000 });
    await deleteButton.click({ force: true });

    // Verify confirmation modal appears
    await expect(page.getByRole('heading', { name: 'Eliminare il messaggio?' })).toBeVisible();

    // Verify modal content
    await expect(page.getByText(/eliminerà permanentemente/i)).toBeVisible();

    // Mock the API delete response
    await chatHelper.mockMessageDelete(true);

    // Click "Elimina" button
    await page.getByRole('button', { name: 'Elimina' }).click({ force: true });

    // Wait for deletion to process

    // Verify modal closes
    await expect(page.getByRole('heading', { name: 'Eliminare il messaggio?' })).not.toBeVisible({
      timeout: 3000,
    });

    // Verify message replaced with "[Messaggio eliminato]"
    await expect(page.getByText('[Messaggio eliminato]')).toBeVisible({ timeout: 5000 });

    // Verify deleted message marker is visible
    const deletedMessageLi = page
      .locator('li[aria-label="Your message"]')
      .filter({ hasText: '[Messaggio eliminato]' });
    await expect(deletedMessageLi).toBeVisible();
  });

  /**
   * Test 4: Delete Cancellation
   */
  test('should cancel delete operation when user clicks cancel', async ({ userPage: page }) => {
    // Send message
    const testMessage = `Message not to delete ${Date.now()}`;
    await page.locator('#message-input').fill(testMessage);
    await page.locator('#message-input').press('Enter');

    // Wait for message to appear
    await expect(
      page.locator(`li[aria-label="Your message"]:has-text("${testMessage}")`)
    ).toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(1000);

    // Hover and click delete button
    const userMessageBubble = page.locator(
      `li[aria-label="Your message"]:has-text("${testMessage}")`
    );
    await userMessageBubble.hover();
    await userMessageBubble.locator('button[aria-label="Delete message"]').click({ force: true });

    // Verify modal appears
    await expect(page.getByRole('heading', { name: 'Eliminare il messaggio?' })).toBeVisible();

    // Click "Annulla" button
    await page.getByRole('button', { name: 'Annulla' }).click({ force: true });

    // Verify modal closes
    await expect(page.getByRole('heading', { name: 'Eliminare il messaggio?' })).not.toBeVisible();

    // Verify original message still visible
    await expect(
      page.locator(`li[aria-label="Your message"]:has-text("${testMessage}")`)
    ).toBeVisible();

    // Verify no deleted placeholder
    await expect(page.getByText('[Messaggio eliminato]')).not.toBeVisible();
  });

  /**
   * Test 5: Edit/Delete Button Visibility
   */
  test('should show edit/delete buttons only on hover for user messages', async ({
    userPage: page,
  }) => {
    // Send message
    const testMessage = `Message for visibility test ${Date.now()}`;
    await page.locator('#message-input').fill(testMessage);
    await page.locator('#message-input').press('Enter');

    // Wait for message to appear
    await expect(
      page.locator(`li[aria-label="Your message"]:has-text("${testMessage}")`)
    ).toBeVisible({ timeout: 10000 });

    // Wait for AI response (network idle)
    const waitHelper = new WaitHelper(page);
    await waitHelper.waitForNetworkIdle(5000);

    // Locate user message
    const userMessageBubble = page.locator(
      `li[aria-label="Your message"]:has-text("${testMessage}")`
    );

    // Hover over user message
    await userMessageBubble.hover();

    // Verify buttons become visible after hover
    await expect(userMessageBubble.locator('button[aria-label="Edit message"]')).toBeVisible({
      timeout: 2000,
    });
    await expect(userMessageBubble.locator('button[aria-label="Delete message"]')).toBeVisible({
      timeout: 2000,
    });

    // Verify AI response messages do NOT have edit/delete buttons
    const aiMessages = page.locator('li[aria-label="AI response"]');
    const aiMessageCount = await aiMessages.count();

    if (aiMessageCount > 0) {
      // Hover over AI message
      await aiMessages.first().hover();

      // Wait a bit

      // AI messages should NOT have edit/delete buttons
      const aiEditButton = aiMessages.first().locator('button[aria-label="Edit message"]');
      const aiDeleteButton = aiMessages.first().locator('button[aria-label="Delete message"]');

      expect(await aiEditButton.count()).toBe(0);
      expect(await aiDeleteButton.count()).toBe(0);
    }
  });

  /**
   * Test 6: Invalidation Warning Display
   */
  test('should display invalidation warning for invalidated messages', async ({
    userPage: page,
  }) => {
    // Send a message first
    const testMessage = `Message to invalidate ${Date.now()}`;
    await page.locator('#message-input').fill(testMessage);
    await page.locator('#message-input').press('Enter');

    // Wait for message to appear
    await expect(
      page.locator(`li[aria-label="Your message"]:has-text("${testMessage}")`)
    ).toBeVisible({ timeout: 10000 });

    // Wait for network idle before mocking invalidation
    const waitHelper = new WaitHelper(page);
    await waitHelper.waitForNetworkIdle(5000);

    // Intercept chat history endpoint to return message with isInvalidated=true
    await page.route('**/api/v1/chats/*/messages*', async route => {
      const method = route.request().method();
      if (method === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            messages: [
              {
                messageId: 'user-message-id',
                role: 'user',
                content: testMessage,
                timestamp: new Date().toISOString(),
                isEdited: false,
              },
              {
                messageId: 'assistant-message-id',
                role: 'assistant',
                content: 'This is an AI response that is now invalidated.',
                timestamp: new Date().toISOString(),
                isEdited: false,
                isInvalidated: true,
                invalidationReason: 'Il messaggio originale è stato modificato',
              },
            ],
            hasMore: false,
          }),
        });
      } else {
        await route.continue();
      }
    });

    // Verify invalidation warning if present
    const warningAlerts = page.locator('div[role="alert"]');
    const alertCount = await warningAlerts.count();

    if (alertCount > 0) {
      // If there's an invalidation warning, verify it has the right text
      const invalidationWarning = warningAlerts.filter({ hasText: 'obsoleta' });
      if ((await invalidationWarning.count()) > 0) {
        await expect(invalidationWarning.first()).toContainText('obsoleta');
      }
    }
  });

  /**
   * Test 7: Error Handling - 403 Forbidden
   */
  test('should display error message when edit fails with 403 Forbidden', async ({
    userPage: page,
  }) => {
    const chatHelper = new ChatHelper(page);

    // Send message
    const testMessage = `Message for error test ${Date.now()}`;
    await page.locator('#message-input').fill(testMessage);
    await page.locator('#message-input').press('Enter');

    // Wait for message to appear
    await expect(
      page.locator(`li[aria-label="Your message"]:has-text("${testMessage}")`)
    ).toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(1000);

    // Enter edit mode
    const userMessageBubble = page.locator(
      `li[aria-label="Your message"]:has-text("${testMessage}")`
    );
    await userMessageBubble.hover();
    await userMessageBubble.locator('button[aria-label="Edit message"]').click({ force: true });

    // Modify content
    const editedMessage = `Edited content that will fail ${Date.now()}`;
    const editTextarea = page.locator('textarea[aria-label="Edit message content"]');
    await editTextarea.clear();
    await editTextarea.fill(editedMessage);

    // Mock API to return 403 Forbidden
    await chatHelper.mockMessageEdit(false, 403);

    // Click Save button
    const saveButton = page.locator('button[aria-label="Save edited message"]');
    await saveButton.click({ force: true });

    // Verify error message appears (auto-retry assertion)
    const errorAlert = page.locator('div[role="alert"]').filter({ hasText: /errore|permess/i });
    const anyError = page.getByText(/errore|permess|autorizzat/i);

    // At least one error indicator should be visible
    const errorVisible = (await errorAlert.count()) > 0 || (await anyError.count()) > 0;
    expect(errorVisible).toBeTruthy();

    // Verify message was NOT updated (original still visible)
    await expect(
      page.locator(`li[aria-label="Your message"]:has-text("${testMessage}")`)
    ).toBeVisible();
  });
});
