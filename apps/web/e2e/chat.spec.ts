/**
 * Chat Page Access Control E2E Tests - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/chat/ChatPage.ts
 * @see apps/web/e2e/pages/home/HomePage.ts
 */

import { test, expect } from '@playwright/test';
import { ChatPage } from './pages/chat/ChatPage';
import { HomePage } from './pages/home/HomePage';

test.describe('Chat Page Access Control', () => {
  test('redirects unauthenticated visitors to login modal', async ({ page }) => {
    const chatPage = new ChatPage(page);
    await chatPage.goto();

    const variant = await chatPage.assertLoginRequired();

    if (variant === 'modal') {
      await expect(page).toHaveURL(/\/login/);
    } else {
      await expect(page).toHaveURL(/\/chat/);
      await expect(page.getByRole('link', { name: /login/i })).toBeVisible();
    }
  });

  test('allows returning home by closing the login modal', async ({ page }) => {
    const chatPage = new ChatPage(page);
    await chatPage.goto();
    const variant = await chatPage.assertLoginRequired();

    if (variant === 'modal') {
      await page.getByLabel(/close dialog/i).click();
    } else {
      await chatPage.goToLoginFromGate();
      await page.waitForURL(/\/login/);
      await page.getByLabel(/close dialog/i).click();
    }

    await page.waitForURL(url => url.pathname === '/', { timeout: 10000 });
    const homePage = new HomePage(page);
    await homePage.assertHeroLoaded();
    await homePage.assertBrandLinkVisible();
  });
});
