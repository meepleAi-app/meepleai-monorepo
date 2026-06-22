/**
 * @ci E2E happy path for the gamebook checkout modal (#953).
 *
 * Quota state is seeded at 47/50 via `?fixture=quota-soft` URL hatch
 * (visual-test-fixture pattern; no backend endpoint exists — Gate B).
 * sessionStorage is cleared on init so the soft warning auto-shows.
 */

import { test, expect } from '@playwright/test';

import { seedAuthSession, mockAuthEndpoints } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

test.describe('@ci gamebook checkout modal', () => {
  test.beforeEach(async ({ page }) => {
    await seedAuthSession(page);
    await mockAuthEndpoints(page);
    await seedCookieConsent(page);
    await page.addInitScript(() => {
      try {
        window.sessionStorage.removeItem('gamebook.soft-warning.dismissed-at');
      } catch {
        // ignore — sessionStorage may not exist yet
      }
    });
  });

  test('soft warning visible → opens checkout modal → step 4 success → close', async ({
    page,
  }) => {
    await page.goto('/gamebook?fixture=quota-soft');

    // Soft warning visible (either mobile toast or desktop modal, depending on viewport)
    const softWarning = page
      .locator('[data-slot="soft-warning-toast"], [data-slot="soft-warning-modal"]')
      .first();
    await expect(softWarning).toBeVisible({ timeout: 10000 });

    // Click "Acquista crediti" CTA from the soft warning → opens checkout at Step 2
    await softWarning.getByRole('button', { name: /Acquista crediti/i }).click();

    const modal = page.getByRole('dialog').filter({ has: page.locator('[data-slot="checkout-modal"]') });
    await expect(modal).toBeVisible();
    await expect(modal.getByRole('heading', { name: 'Scegli il tuo pacchetto' })).toBeVisible();

    // Starter is pre-selected; click Continua → Step 3
    await modal.getByRole('button', { name: /Continua/i }).click();
    await expect(modal.getByRole('heading', { name: 'Pagamento' })).toBeVisible();

    // Click "Paga" → loading state, then Step 4 success after ~2s
    await modal.getByRole('button', { name: /^Paga/i }).click();
    await expect(modal.getByRole('button', { name: /Elaborazione/i })).toBeVisible();

    await expect(modal.getByRole('heading', { name: 'Crediti aggiunti!' })).toBeVisible({
      timeout: 5000,
    });

    // Click "Torna al gioco" → modal closes
    await modal.getByRole('button', { name: /Torna al gioco/i }).click();
    await expect(modal).not.toBeVisible();
  });

  test('ESC closes the modal at any step', async ({ page }) => {
    await page.goto('/gamebook?fixture=quota-soft');

    const softWarning = page
      .locator('[data-slot="soft-warning-toast"], [data-slot="soft-warning-modal"]')
      .first();
    await expect(softWarning).toBeVisible({ timeout: 10000 });
    await softWarning.getByRole('button', { name: /Acquista crediti/i }).click();

    const modal = page.getByRole('dialog').filter({ has: page.locator('[data-slot="checkout-modal"]') });
    await expect(modal).toBeVisible();

    await page.keyboard.press('Escape');
    await expect(modal).not.toBeVisible();
  });
});
