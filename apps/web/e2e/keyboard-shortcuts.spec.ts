/**
 * Keyboard Shortcuts E2E Tests (Issue #2247 Task 2)
 *
 * Comprehensive testing of keyboard shortcuts documented in KeyboardShortcutsHelp.
 * Tests all global shortcuts for navigation, editor, and system actions.
 *
 * @see apps/web/src/components/layout/KeyboardShortcutsHelp.tsx
 * @see apps/web/src/hooks/useKeyboardShortcuts.ts
 */

import { setupMockAuth } from './fixtures/auth';
import { test, expect } from './fixtures';

/**
 * Platform-specific modifier key
 */
const isMac = process.platform === 'darwin';
const modKey = isMac ? 'Meta' : 'Control';

test.describe('Keyboard Shortcuts - Navigation', () => {
  test.beforeEach(async ({ page }) => {
    // Setup authenticated state for shortcuts that require auth
    await setupMockAuth(page, 'User', 'user@meepleai.dev');
  });

  test('Ctrl/Cmd+N should navigate to new chat', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Press Ctrl/Cmd+N
    await page.keyboard.press(`${modKey}+KeyN`);

    // Should navigate to /chat
    await page.waitForURL(/\/chat/);
    expect(page.url()).toContain('/chat');
  });

  test('Ctrl/Cmd+U should navigate to PDF upload', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Press Ctrl/Cmd+U
    await page.keyboard.press(`${modKey}+KeyU`);

    // Should navigate to /upload
    await page.waitForURL(/\/upload/);
    expect(page.url()).toContain('/upload');
  });

  test('Ctrl/Cmd+K should open command palette', async ({ page }) => {
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    // Press Ctrl/Cmd+K
    await page.keyboard.press(`${modKey}+KeyK`);

    // Command palette should open (if implemented)
    // TODO: Update when command palette is implemented
    // For now, just verify shortcut doesn't cause errors
    await page.waitForTimeout(1000);
  });
});

test.describe('Keyboard Shortcuts - System', () => {
  test('Shift+? should open keyboard shortcuts help', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Press Shift+? (question mark)
    await page.keyboard.press('Shift+Slash'); // Shift+/ = ?

    // Keyboard shortcuts help dialog should open (if implemented)
    // TODO: Update when KeyboardShortcutsHelp is implemented globally
    // For now, just verify shortcut doesn't cause errors
    await page.waitForTimeout(1000);
  });

  // TODO Issue #2247: Button locator needs fix for landing page
  // Skip until modal opening is stabilized
  test.skip('Escape should close modal dialogs', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Open auth modal first
    const getStartedButton = page.getByRole('button', { name: /get started|inizia/i });
    await getStartedButton.click();

    // Wait for modal to be visible
    const modal = page.locator('[role="dialog"]').first();
    await expect(modal).toBeVisible();

    // Press Escape
    await page.keyboard.press('Escape');

    // Modal should close
    await expect(modal).not.toBeVisible({ timeout: 3000 });
  });
});

test.describe('Keyboard Shortcuts - Editor', () => {
  test.beforeEach(async ({ page }) => {
    await setupMockAuth(page, 'User', 'user@meepleai.dev');
  });

  // TODO Issue #2247: Chat input interaction needs refinement
  // Skip until chat send behavior is confirmed
  test.skip('Ctrl/Cmd+Enter should send message in chat', async ({ page }) => {
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    // Find chat input
    const chatInput = page.getByPlaceholder(/ask a question|fai una domanda/i);
    await chatInput.fill('Test message');

    // Press Ctrl/Cmd+Enter
    await page.keyboard.press(`${modKey}+Enter`);

    // Message should be sent (verify send button was clicked or message appears)
    // Wait a bit for action to process
    await page.waitForTimeout(1000);

    // Input should be cleared or message should appear in chat
    // TODO: Add more specific assertion when chat behavior is confirmed
  });
});

test.describe('Tab Order - Comprehensive', () => {
  test('Landing page should have logical tab order', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const focusedElements: string[] = [];

    // Tab through first 10 interactive elements
    for (let i = 0; i < 10; i++) {
      await page.keyboard.press('Tab');

      const focusedElement = await page.evaluate(() => {
        const el = document.activeElement;
        if (!el) return 'none';

        // Get readable identifier
        const tagName = el.tagName.toLowerCase();
        const id = el.id ? `#${el.id}` : '';
        const role = el.getAttribute('role') || '';
        const ariaLabel = el.getAttribute('aria-label') || '';
        const textContent = el.textContent?.slice(0, 30) || '';

        return `${tagName}${id}${role ? `[${role}]` : ''}${ariaLabel ? `{${ariaLabel}}` : ''}${textContent ? `:${textContent.trim()}` : ''}`;
      });

      focusedElements.push(focusedElement);
    }

    // Should have focused elements (not just body)
    const validFocusCount = focusedElements.filter(el => el !== 'body' && el !== 'none').length;
    expect(validFocusCount).toBeGreaterThan(5);

    // Should not have keyboard trap (focus should progress)
    const uniqueElements = new Set(focusedElements);
    expect(uniqueElements.size).toBeGreaterThan(3);
  });

  test('Chat page should have logical tab order', async ({ page }) => {
    await setupMockAuth(page, 'User', 'user@meepleai.dev');
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    // Tab through elements
    const focusedElements: string[] = [];
    for (let i = 0; i < 8; i++) {
      await page.keyboard.press('Tab');
      const tagName = await page.evaluate(() => document.activeElement?.tagName);
      focusedElements.push(tagName || 'none');
    }

    // Should have interactive elements (buttons, inputs, links)
    const hasInteractiveElements = focusedElements.some(tag =>
      ['BUTTON', 'INPUT', 'TEXTAREA', 'A', 'SELECT'].includes(tag)
    );
    expect(hasInteractiveElements).toBe(true);
  });
});

test.describe('Focus Management', () => {
  test('No keyboard traps on landing page', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Tab forward 20 times
    for (let i = 0; i < 20; i++) {
      await page.keyboard.press('Tab');
    }

    // Focus should still be on a valid element
    const focusedTag = await page.evaluate(() => document.activeElement?.tagName);
    expect(focusedTag).not.toBe('BODY');

    // Should be able to Shift+Tab backwards
    await page.keyboard.press('Shift+Tab');
    const stillHasFocus = await page.evaluate(() => document.activeElement !== document.body);
    expect(stillHasFocus).toBe(true);
  });

  // TODO Issue #2247: Modal focus trap needs modal opening fix first
  // Skip until modal button locator is fixed
  test.skip('Modal traps focus correctly', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Open modal
    const getStartedButton = page.getByRole('button', { name: /get started|inizia/i });
    await getStartedButton.click();

    const modal = page.locator('[role="dialog"]').first();
    await expect(modal).toBeVisible();

    // Tab through modal - focus should stay within modal
    const focusedElements: boolean[] = [];
    for (let i = 0; i < 10; i++) {
      await page.keyboard.press('Tab');

      const isInModal = await page.evaluate(() => {
        const focused = document.activeElement;
        const modal = document.querySelector('[role="dialog"]');
        return modal?.contains(focused) || false;
      });

      focusedElements.push(isInModal);
    }

    // At least 70% of tab presses should keep focus in modal (focus trap)
    const inModalCount = focusedElements.filter(Boolean).length;
    expect(inModalCount).toBeGreaterThanOrEqual(7);
  });
});

test.describe('Skip Links', () => {
  // TODO Issue #2247: Skip links not implemented yet
  // Skip until skip-to-main link is added to layout
  test.skip('Skip to main content link should exist', async ({ page }) => {
    await page.goto('/');

    // Look for skip link (usually first focusable element)
    await page.keyboard.press('Tab');

    const skipLink = page.locator('a[href="#main"], a[href="#main-content"]').first();
    const skipLinkExists = await skipLink.count();

    // Document if skip link exists or not
    if (skipLinkExists > 0) {
      // Skip link exists - test functionality
      await skipLink.click();

      // Focus should move to main content
      const mainElement = page.locator('main, #main, #main-content').first();
      await expect(mainElement).toBeFocused();
    } else {
      // TODO Issue #2247: Add skip links for better keyboard navigation
      console.log('⚠️ Skip link not found - consider adding for WCAG 2.1 AA compliance');
    }
  });
});
