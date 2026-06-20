// apps/web/e2e/translations-fe-hook.spec.ts
import { test, expect } from '@playwright/test';

/**
 * Issue #2339 sub-PR 2/3 — E2E happy path verifying that an IT user sees
 * IT-localized titles when seed translations exist. Seed translation data
 * lands in sub-PR 3/3; this test is guarded by `test.skip()` until then.
 *
 * The test asserts behavior, not the absence of UI flicker (which is covered
 * by unit tests around useMemo identity preservation).
 *
 * aria-label assertions match both EN ("localized from English") and IT
 * ("tradotto da") substrings per DEC-FE-9 i18n chiave (react-intl resolves
 * the `common.localizedFromEnglish` key based on UI locale).
 */

test.describe('useGameTitle E2E', () => {
  test.beforeEach(async ({ page }) => {
    // Force browser locale via context override
    await page.context().addInitScript(() => {
      Object.defineProperty(navigator, 'language', { get: () => 'it-IT' });
      Object.defineProperty(navigator, 'languages', { get: () => ['it-IT', 'it'] });
    });
  });

  test.skip(
    ({}, testInfo) => !testInfo.project.metadata?.seedTranslations,
    'Requires sub-PR 3/3 seed translations to land first'
  );

  test('IT user sees IT-localized title on Library page', async ({ page }) => {
    await page.goto('/library');

    // After seed sub-PR 3/3 lands, "Catan" → "I Coloni di Catan" in card heading
    const catanCard = page.getByRole('article').filter({ hasText: /Coloni di Catan|Catan/ });
    await expect(catanCard).toBeVisible();

    const heading = catanCard.getByRole('heading', { level: 3 });
    await expect(heading).toHaveText('I Coloni di Catan');
    // DEC-FE-9: aria-label resolves via react-intl key common.localizedFromEnglish
    // EN UI → "(localized from English: Catan)"
    // IT UI → "(tradotto da: Catan)"
    // Match the originalTitle "Catan" plus either localization phrase.
    await expect(heading).toHaveAttribute(
      'aria-label',
      /(localized from English|tradotto da):\s*Catan/i
    );
  });

  test('IT user sees IT-localized title on Discover page', async ({ page }) => {
    await page.goto('/games?tab=discover');
    const heading = page.getByRole('heading', { name: /Coloni di Catan/i });
    await expect(heading).toBeVisible();
  });

  test('EN-override user sees canonical EN even with browser it-IT', async ({ page }) => {
    // Set profile override via cookie or login as user with Language='en'
    await page
      .context()
      .addCookies([{ name: 'preferredLocale', value: 'en', url: 'http://localhost:3000' }]);
    await page.goto('/library');

    const heading = page.getByRole('heading', { name: /^Catan$/ });
    await expect(heading).toBeVisible();
    // Canonical → no localization aria-label augmentation
    await expect(heading).not.toHaveAttribute('aria-label', /localized from English|tradotto da/i);
  });
});
