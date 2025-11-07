import { test, expect } from '@playwright/test';
import { getTextMatcher, t } from './fixtures/i18n';

test.describe('Chat Page', () => {
  test('should require authentication', async ({ page }) => {
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    // Should show login required message
    await expect(page.getByRole('heading', { name: getTextMatcher('chat.loginRequired') })).toBeVisible();
    await expect(page.getByText(getTextMatcher('chat.loginRequiredMessage'))).toBeVisible();

    // Should have login link
    await expect(page.getByRole('link', { name: getTextMatcher('chat.goToLogin') })).toBeVisible();
  });

  test('should have return to home link', async ({ page }) => {
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('link', { name: getTextMatcher('setup.backToHome') })).toBeVisible();
  });
});