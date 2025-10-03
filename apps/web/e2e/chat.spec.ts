import { test, expect } from '@playwright/test';

test.describe('Chat Page', () => {
  test('should require authentication', async ({ page }) => {
    await page.goto('/chat');

    // Should show login required message
    await expect(page.getByRole('heading', { name: 'Accesso richiesto' })).toBeVisible();
    await expect(page.getByText('Devi effettuare l\'accesso per utilizzare la chat')).toBeVisible();

    // Should have login link
    await expect(page.getByRole('link', { name: 'Vai al Login' })).toBeVisible();
  });

  test('should have return to home link', async ({ page }) => {
    await page.goto('/chat');

    await expect(page.getByRole('link', { name: 'Torna alla Home' })).toBeVisible();
  });
});
