/**
 * Home Page E2E Tests - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/home/HomePage.ts
 */

import { test } from '@playwright/test';
import { HomePage } from './pages/home/HomePage';

test.describe('Home Page', () => {
  test('shows hero and CTA for guests', async ({ page }) => {
    const homePage = new HomePage(page);
    await homePage.goto();
    await homePage.assertHeroLoaded();
    await homePage.assertBrandLinkVisible();
    await homePage.assertPrimaryCtaVisible();
  });

  test('opens registration form from navigation CTA', async ({ page }) => {
    const homePage = new HomePage(page);
    await homePage.goto();
    await homePage.openAuthModal();
    await homePage.assertRegistrationFormFields();
  });

  test('allows switching back to login form', async ({ page }) => {
    const homePage = new HomePage(page);
    await homePage.goto();
    await homePage.openAuthModal();
    await homePage.assertLoginFormFields();
  });

  test('displays localized features section', async ({ page }) => {
    const homePage = new HomePage(page);
    await homePage.goto();
    await homePage.assertFeaturesOverview();
  });
});
