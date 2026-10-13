/**
 * Home Page E2E Tests - MIGRATED TO POM
 *
 * #4248: rewritten on the real pages. The landing's calls to action lead to `/register`,
 * which links to `/login`; the auth modal these tests used to open no longer exists.
 *
 * @see apps/web/e2e/pages/home/HomePage.ts
 */

import { seedCookieConsent } from './_helpers/seedCookieConsent';
import { test } from './fixtures';
import { HomePage } from './pages/home/HomePage';

test.describe('Home Page', () => {
  test.beforeEach(async ({ page }) => {
    // The consent banner covers the page and intercepts clicks.
    await seedCookieConsent(page);
  });

  test('shows hero and CTA for guests', async ({ page }) => {
    const homePage = new HomePage(page);
    await homePage.goto();
    await homePage.assertHeroLoaded();
    await homePage.assertBrandLinkVisible();
    await homePage.assertPrimaryCtaVisible();
  });

  test('the hero CTA opens the registration page with its form', async ({ page }) => {
    const homePage = new HomePage(page);
    await homePage.mockRegistrationMode(true);
    await homePage.goto();
    await homePage.openRegistration();
    await homePage.assertRegistrationFormFields();
  });

  test('with invite-only registration the page offers the access request instead', async ({
    page,
  }) => {
    const homePage = new HomePage(page);
    await homePage.mockRegistrationMode(false);
    await homePage.goto();
    await homePage.openRegistration();
    await homePage.assertRequestAccessForm();
  });

  test('the registration page links to the login page', async ({ page }) => {
    const homePage = new HomePage(page);
    await homePage.mockRegistrationMode(true);
    await homePage.goto();
    await homePage.openRegistration();
    await homePage.goToLoginFromRegistration();
    await homePage.assertLoginFormFields();
  });

  test('shows the how-it-works section', async ({ page }) => {
    const homePage = new HomePage(page);
    await homePage.goto();
    await homePage.assertHowItWorksSection();
  });
});
