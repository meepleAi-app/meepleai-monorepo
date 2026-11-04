import { Locator, expect } from '@playwright/test';

import { IHomePage } from '../../types/pom-interfaces';
import { BasePage } from '../base/BasePage';

/**
 * HomePage - Landing page interactions and assertions
 *
 * Provides high-level helpers to keep tests free from direct Playwright calls.
 *
 * #4248: the landing has no auth modal. Its calls to action are links to the `/register`
 * page, which links to `/login`. The landing copy is hardcoded Italian; the auth pages
 * are translated, so their fields are found by id and testid, not by text.
 */
export class HomePage extends BasePage implements IHomePage {
  private get heroHeading(): Locator {
    return this.page.getByRole('heading', { level: 1 });
  }

  private get heroSubtitle(): Locator {
    return this.page.getByText(/un agente AI che conosce il tuo gioco/i);
  }

  private get brandLink(): Locator {
    return this.page.getByRole('link', { name: 'MeepleAI Home' });
  }

  private get primaryCta(): Locator {
    // WelcomeHero: «Inizia gratis» → /register. The same label repeats in WelcomeCTA.
    return this.page.getByRole('link', { name: 'Inizia gratis' }).first();
  }

  private get howItWorksHeading(): Locator {
    return this.page.locator('#come-funziona').getByRole('heading', { name: /Come funziona/i });
  }

  async goto(): Promise<void> {
    await this.page.goto('/');
    await this.waitForLoad();
  }

  /**
   * The register page asks the backend whether registration is public
   * (`GET /api/v1/auth/registration-mode`), from the browser, through the Next proxy:
   * hence a relative glob, not an absolute `http://localhost:8080` URL, which would
   * never match.
   */
  async mockRegistrationMode(publicRegistrationEnabled: boolean): Promise<void> {
    await this.page.route('**/api/v1/auth/registration-mode', route =>
      route.fulfill({ json: { publicRegistrationEnabled, oauthEnabled: false } })
    );
  }

  async assertHeroLoaded(): Promise<void> {
    await this.waitForElement(this.heroHeading);
    await expect(this.heroHeading).toContainText(/AI/);
    await expect(this.heroSubtitle).toBeVisible();
  }

  async assertBrandLinkVisible(): Promise<void> {
    await expect(this.brandLink).toBeVisible();
  }

  async assertPrimaryCtaVisible(): Promise<void> {
    await expect(this.primaryCta).toBeVisible();
    await expect(this.primaryCta).toHaveAttribute('href', '/register');
  }

  async openRegistration(): Promise<void> {
    await this.click(this.primaryCta);
    await this.page.waitForURL(/\/register$/);
  }

  async goToLoginFromRegistration(): Promise<void> {
    await this.click(this.page.getByTestId('register-login-link'));
    await this.page.waitForURL(/\/login$/);
  }

  async assertRegistrationFormFields(): Promise<void> {
    const form = this.page.getByTestId('register-form');
    await expect(form).toBeVisible();
    await expect(form.locator('#register-email')).toBeVisible();
    await expect(form.locator('#register-password')).toBeVisible();
    await expect(form.getByTestId('register-terms')).toBeVisible();
    await expect(form.locator('button[type="submit"]')).toBeVisible();
  }

  async assertRequestAccessForm(): Promise<void> {
    await expect(this.page.getByTestId('request-access-form')).toBeVisible();
    await expect(this.page.getByTestId('request-access-email')).toBeVisible();
    await expect(this.page.getByTestId('register-form')).toHaveCount(0);
  }

  async assertLoginFormFields(): Promise<void> {
    const form = this.page.getByTestId('login-form');
    await expect(form).toBeVisible();
    await expect(form.locator('#login-email')).toBeVisible();
    await expect(form.locator('#login-password')).toBeVisible();
    await expect(form.locator('button[type="submit"]')).toBeVisible();
  }

  async assertHowItWorksSection(): Promise<void> {
    await this.howItWorksHeading.scrollIntoViewIfNeeded();
    await expect(this.howItWorksHeading).toBeVisible();
  }
}
