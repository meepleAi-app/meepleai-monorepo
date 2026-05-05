// /onboarding is inside the (authenticated) route group and requires a session
// with `onboardingCompleted: false`. `userPage` provides such a user via MSW scenarios.
import { expect, test } from './fixtures/auth';

test.describe('Onboarding product tour', () => {
  test('mobile: full happy path lands on /library', async ({ userPage: page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/onboarding');

    await expect(page.getByRole('heading', { level: 1, name: /benvenuto in/i })).toBeVisible();
    await page.getByRole('button', { name: /inizia il tour/i }).click();

    // Games
    await expect(page.getByRole('heading', { level: 2, name: /ludoteca/i })).toBeVisible();
    await page.getByRole('button', { name: /catan/i }).click();
    await page.getByRole('button', { name: /azul/i }).click();
    await page.getByRole('button', { name: /wingspan/i }).click();
    await page.getByRole('button', { name: /avanti/i }).click();

    // Agents
    await expect(page.getByRole('heading', { level: 2, name: /assistenti/i })).toBeVisible();
    await page.getByRole('button', { name: /avanti/i }).click();

    // FirstSession → skip to Complete
    await expect(page.getByRole('heading', { level: 2, name: /cosa vuoi fare/i })).toBeVisible();
    await page.getByRole('button', { name: /^salta$/i }).click();

    // Complete → home
    await expect(page.getByRole('heading', { level: 1, name: /benvenuto!/i })).toBeVisible();
    await page.getByRole('button', { name: /vai alla home/i }).click();

    await page.waitForURL('**/library');
  });

  test('skip from Welcome goes to Complete then /library', async ({ userPage: page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/onboarding');

    await page.getByRole('button', { name: /salta, esploro da solo/i }).click();
    await expect(page.getByRole('heading', { level: 1, name: /benvenuto!/i })).toBeVisible();
    await page.getByRole('button', { name: /vai alla home/i }).click();

    await page.waitForURL('**/library');
  });

  test('FirstSession CTA routes to chosen href', async ({ userPage: page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/onboarding');

    await page.getByRole('button', { name: /inizia il tour/i }).click();
    await page.getByRole('button', { name: /catan/i }).click();
    await page.getByRole('button', { name: /azul/i }).click();
    await page.getByRole('button', { name: /wingspan/i }).click();
    await page.getByRole('button', { name: /avanti/i }).click();
    await page.getByRole('button', { name: /avanti/i }).click();

    await page.getByRole('button', { name: /esplora la library/i }).click();
    await page.waitForURL('**/library');
  });
});
