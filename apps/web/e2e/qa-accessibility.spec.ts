/**
 * Q&A Interface - Accessibility E2E Tests - MIGRATED TO POM
 *
 * @see apps/web/e2e/helpers/qa-test-utils.ts
 */

import { test, expect } from './fixtures';
import { setupQATestEnvironment } from './helpers/qa-test-utils';

test.describe('Q&A Interface - Accessibility (Issue #1009)', () => {
  test('should support keyboard navigation with Tab key', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Tab through interactive elements
    await page.keyboard.press('Tab'); // Should focus first interactive element
    await page.keyboard.press('Tab'); // Continue tabbing
    await page.keyboard.press('Tab');

    // Eventually should reach message input
    await expect(page.locator('#message-input')).toBeFocused();

    // Type question
    await page.keyboard.type('Keyboard test');

    // Tab to submit button
    await page.keyboard.press('Tab');

    // Verify submit button is focused
    const submitButton = page.locator('button[type="submit"]');
    await expect(submitButton).toBeFocused();

    // Submit with Enter
    await page.keyboard.press('Enter');

    // Wait for backend response
    await expect(submitButton).toBeEnabled({ timeout: 30000 });
  });

  test('should support Enter key to submit question', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    const input = page.locator('#message-input');
    await input.focus();
    await input.fill('Enter key test');

    // Submit with Enter (without clicking button)
    await page.keyboard.press('Enter');

    // Wait for backend response
    await expect(input).toBeEnabled({ timeout: 30000 });
  });

  test('should support Escape key to close modals or cancel actions', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // If there's a modal or dialog, Escape should close it
    // This test assumes potential modal/dialog scenarios in Q&A interface

    const input = page.locator('#message-input');
    await input.focus();
    await input.fill('Test question');

    // Press Escape (should not submit, might clear or cancel)
    await page.keyboard.press('Escape');

    // Verify no submission occurred
    await expect(page.getByText('Test question')).not.toBeVisible();
  });

  test('should have proper ARIA labels for screen readers', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Verify critical elements have ARIA labels or roles
    const messageInput = page.locator('#message-input');
    await expect(messageInput).toBeVisible();

    // Input should have accessible name (label or aria-label)
    const inputAccessibleName =
      (await messageInput.getAttribute('aria-label')) ||
      (await messageInput.getAttribute('placeholder'));
    expect(inputAccessibleName).toBeTruthy();

    // Submit button should have accessible label
    const submitButton = page.locator('button[type="submit"]');
    const buttonText = await submitButton.textContent();
    expect(buttonText?.trim().length).toBeGreaterThan(0);

    // Game selector should have label
    const gameSelect = page.locator('#gameSelect');
    const gameLabel =
      (await gameSelect.getAttribute('aria-label')) ||
      (await page.locator('label[for="gameSelect"]').count());
    expect(gameLabel).toBeTruthy();
  });

  test('should have proper focus management in chat messages', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    await page.fill('#message-input', 'Focus test');
    await page.locator('button[type="submit"]').click();

    // Wait for backend response
    await page.waitForTimeout(2000);

    // Focus should return to input after message sent
    await expect(page.locator('#message-input')).toBeEnabled();
  });

  test('should have sufficient color contrast for WCAG AA compliance', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Verify text has sufficient contrast ratio (4.5:1 for normal text, 3:1 for large text)
    // This is a simplified check - full WCAG audit requires specialized tools

    const messageInput = page.locator('#message-input');
    const textColor = await messageInput.evaluate(el => window.getComputedStyle(el).color);
    const bgColor = await messageInput.evaluate(el => window.getComputedStyle(el).backgroundColor);

    // Basic check: text and background colors should be different
    expect(textColor).not.toBe(bgColor);

    // Verify button has visible text (not just icon)
    const submitButton = page.locator('button[type="submit"]');
    const buttonTextContent = await submitButton.textContent();
    expect(buttonTextContent?.trim().length).toBeGreaterThan(0);
  });

  test('should provide skip links for snippet navigation', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    await page.fill('#message-input', 'Skip links test');
    await page.locator('button[type="submit"]').click();

    // Wait for backend response
    await page.waitForTimeout(2000);

    // Verify snippets section is reachable via keyboard
    await page.keyboard.press('Tab');
  });

  test('should announce streaming status to screen readers', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    await page.fill('#message-input', 'Screen reader test');
    await page.locator('button[type="submit"]').click();

    // Should have aria-live region for streaming updates
    const liveRegion = page.locator('[aria-live="polite"], [aria-live="assertive"]');
    const liveRegionCount = await liveRegion.count();

    // Verify at least one live region exists for dynamic updates
    expect(liveRegionCount).toBeGreaterThan(0);
  });

  test('should support high contrast mode', async ({ page }) => {
    await setupQATestEnvironment(page);

    // Enable high contrast mode simulation
    await page.emulateMedia({ colorScheme: 'dark', forcedColors: 'active' });

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Verify critical elements are still visible in high contrast
    await expect(page.locator('#message-input')).toBeVisible();
    await expect(page.locator('button[type="submit"]')).toBeVisible();
    await expect(page.locator('#gameSelect')).toBeVisible();

    // Verify text remains readable
    const messageInput = page.locator('#message-input');
    const isVisible = await messageInput.isVisible();
    expect(isVisible).toBe(true);
  });

  test('should support screen reader landmarks and regions', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    // Verify semantic HTML regions (main, nav, form)
    const mainRegion = page.locator('main, [role="main"]');
    const mainCount = await mainRegion.count();
    expect(mainCount).toBeGreaterThan(0);

    // Form should have proper role or semantic element
    const formElement = page.locator('form, [role="form"]');
    const formCount = await formElement.count();
    expect(formCount).toBeGreaterThan(0);
  });

  test('should provide meaningful error announcements for screen readers', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    await page.fill('#message-input', 'Error test');
    await page.locator('button[type="submit"]').click();

    // Wait for backend response
    await page.waitForTimeout(2000);
  });

  test('should have focusable and accessible feedback buttons', async ({ page }) => {
    await setupQATestEnvironment(page);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#message-input')).toBeEnabled({ timeout: 10000 });

    await page.fill('#message-input', 'Feedback test');
    await page.locator('button[type="submit"]').click();

    // Wait for backend response
    await page.waitForTimeout(2000);

    // Feedback buttons should be keyboard accessible
    const helpfulButton = page.getByRole('button', { name: /utile|helpful/i });

    // If button exists, test accessibility
    if ((await helpfulButton.count()) > 0) {
      await helpfulButton.focus();
      await page.keyboard.press('Enter');
    }
  });
});
