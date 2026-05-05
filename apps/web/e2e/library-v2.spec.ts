// /library/v2 is inside the (authenticated) route group and requires a session.
// `userPage` runs loginAsUser() before each test — see e2e/fixtures/auth.ts.
import { test, expect } from './fixtures/auth';

test.describe('Library V2 pilot', () => {
  test('mobile: opens game drawer on card tap', async ({ userPage: page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/library/v2');
    await page.getByRole('button', { name: /catan/i }).click();
    await expect(page.getByRole('tab', { name: /info/i })).toBeVisible();
    await page.getByRole('tab', { name: /sessioni/i }).click();
    await expect(page.getByText(/5 sessioni/i)).toBeVisible();
  });

  test('desktop: shows split-view with detail on selection', async ({ userPage: page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/library/v2');
    await expect(page.getByText(/seleziona un gioco/i)).toBeVisible();
    await page.getByRole('button', { name: /root/i }).click();
    await expect(page.getByText(/gioco di guerra asimmetrico/i)).toBeVisible();
  });

  test('filter tabs update counts', async ({ userPage: page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/library/v2');
    await expect(page.getByRole('tab', { name: /tutti.*2/i })).toBeVisible();
    await page.getByRole('tab', { name: /possedut/i }).click();
    await expect(page.getByRole('button', { name: /catan/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /root/i })).not.toBeVisible();
  });
});
