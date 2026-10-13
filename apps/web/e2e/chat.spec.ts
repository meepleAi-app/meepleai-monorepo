/**
 * Chat Page Access Control E2E Tests - MIGRATED TO POM
 *
 * #4248: there is no auth modal and no inline gate. `proxy.ts` sends a guest to
 * `/login?from=/chat`, and the login page brings them back to the chat. The old
 * "close the modal to go home" test had no surviving feature to test and was removed.
 *
 * @see apps/web/e2e/pages/chat/ChatPage.ts
 */

import {
  MISSING_CREDENTIALS_REASON,
  REAL_ADMIN_EMAIL,
  REAL_ADMIN_PASSWORD,
  hasRealAdminCredentials,
} from './_helpers/realAdminAuth';
import { seedCookieConsent } from './_helpers/seedCookieConsent';
import { test, expect } from './fixtures';
import { ChatPage } from './pages/chat/ChatPage';

test.describe('Chat Page Access Control', () => {
  test.beforeEach(async ({ page }) => {
    await seedCookieConsent(page);
  });

  test('sends an unauthenticated visitor to the login page, remembering the chat', async ({
    page,
  }) => {
    const chatPage = new ChatPage(page);
    await chatPage.goto();
    await chatPage.assertLoginRequired();
  });

  test('after logging in from that page the visitor lands back on the chat', async ({ page }) => {
    test.skip(!hasRealAdminCredentials, MISSING_CREDENTIALS_REASON);
    const chatPage = new ChatPage(page);
    await chatPage.goto();
    await chatPage.assertLoginRequired();

    await chatPage.loginFromRedirect(REAL_ADMIN_EMAIL as string, REAL_ADMIN_PASSWORD as string);

    await page.waitForURL(url => url.pathname === '/chat', { timeout: 30000 });
    await expect(page).toHaveURL(/\/chat$/);
  });
});
