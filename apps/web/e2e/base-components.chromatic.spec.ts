/**
 * Chromatic visual regression for base components (Button, Card, Input, Form)
 * Issue: #989 - Base components (Playful Boardroom)
 *
 * Uses the Chromatic Playwright fixture to capture stable snapshots from
 * the Components Showcase page. Each section is screenshotted individually
 * to minimize flakiness and reduce diff noise.
 */

import { test, expect } from './fixtures/chromatic';

import type { Page } from '@playwright/test';

// Desktop viewport to capture full component blocks without wrapping
test.use({
  viewport: { width: 1280, height: 900 },
});

const goToShowcase = async (page: Page) => {
  await page.goto('/components-showcase', { waitUntil: 'domcontentloaded' });
  // Wait for the showcase hero to render to avoid networkidle flakiness
  await page.getByRole('heading', { name: /component.*showcase/i }).waitFor();
  // Small buffer for fonts/tokens
  await page.waitForTimeout(150);
};

const sectionLocator = (page: Page, title: string) =>
  page
    .locator('section')
    .filter({ has: page.getByRole('heading', { name: title }) })
    .first();

test.describe('Base Components - Chromatic', () => {
  test('Buttons section', async ({ page }) => {
    await goToShowcase(page);
    const section = sectionLocator(page, 'Buttons');
    await expect(section).toBeVisible();
    await expect(section).toHaveScreenshot('base-components-buttons.png');
  });

  test('Inputs section', async ({ page }) => {
    await goToShowcase(page);
    const section = sectionLocator(page, 'Inputs');
    await expect(section).toBeVisible();
    await expect(section).toHaveScreenshot('base-components-inputs.png');
  });

  test('Textarea section', async ({ page }) => {
    await goToShowcase(page);
    const section = sectionLocator(page, 'Textarea');
    await expect(section).toBeVisible();
    await expect(section).toHaveScreenshot('base-components-textarea.png');
  });

  test('Select section', async ({ page }) => {
    await goToShowcase(page);
    const section = sectionLocator(page, 'Select');
    await expect(section).toBeVisible();
    await expect(section).toHaveScreenshot('base-components-select.png');
  });
});
