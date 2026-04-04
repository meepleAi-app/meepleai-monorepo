import { Page } from '@playwright/test';

/**
 * Dismiss cookie consent banner if present.
 * Logs outcome instead of silently swallowing errors.
 *
 * @param page - Playwright Page instance
 * @param label - Test step label for debugging (e.g., '[T3]')
 */
export async function dismissCookieConsent(page: Page, label = '[cookie]'): Promise<void> {
  const cookieBtn = page.getByRole('button', { name: /essential only|accept all/i }).first();

  const visible = await cookieBtn.isVisible({ timeout: 2_000 }).catch(() => false);

  if (visible) {
    await cookieBtn.click();
    console.log(`${label} Cookie consent dismissed`);
    // Wait for banner to disappear — use waitFor, not waitForTimeout
    await page
      .locator('[data-testid="cookie-banner"], .cookie-consent')
      .waitFor({ state: 'hidden', timeout: 3_000 })
      .catch(() => console.log(`${label} Cookie banner hide timeout (non-blocking)`));
  } else {
    console.log(`${label} No cookie consent banner found`);
  }
}
