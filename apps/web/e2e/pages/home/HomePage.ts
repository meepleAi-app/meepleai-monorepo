import { Locator, expect } from '@playwright/test';
import { BasePage } from '../base/BasePage';
import { getFlexibleMatcher, getTextMatcher } from '../../fixtures/i18n';
import { IHomePage } from '../../types/pom-interfaces';

/**
 * HomePage - Landing page interactions and assertions
 *
 * Provides high-level helpers to keep tests free from direct Playwright calls.
 */
export class HomePage extends BasePage implements IHomePage {
  private get heroHeading(): Locator {
    return this.page.getByRole('heading', { level: 1 });
  }

  private get heroSubtitle(): Locator {
    return this.page.getByText(getTextMatcher('home.hero.subtitle'));
  }

  private get brandLink(): Locator {
    return this.page.getByRole('link', { name: /MeepleAI/i });
  }

  private get navigationCta(): Locator {
    return this.page.getByTestId('nav-get-started');
  }

  private get authModal(): Locator {
    return this.page.getByRole('dialog');
  }

  private get tabList(): Locator {
    return this.page.getByRole('tablist');
  }

  private get loginTab(): Locator {
    return this.page.getByRole('tab', { name: getTextMatcher('navigation.login') });
  }

  private get registerTab(): Locator {
    return this.page.getByRole('tab', { name: getTextMatcher('navigation.register') });
  }

  private get featuresSection(): Locator {
    return this.page.locator('#features');
  }

  private get featuresHeading(): Locator {
    return this.featuresSection.getByRole('heading', { name: getTextMatcher('home.features.title') });
  }

  private getFeatureCardHeading(key: string): Locator {
    return this.featuresSection.getByRole('heading', { name: getFlexibleMatcher(key) }).first();
  }

  async goto(): Promise<void> {
    await this.page.goto('/');
    await this.waitForLoad();
  }

  async assertHeroLoaded(): Promise<void> {
    await this.waitForElement(this.heroHeading);
    await expect(this.heroHeading).toContainText(getTextMatcher('home.hero.title'));
    await expect(this.heroSubtitle).toBeVisible();
  }

  async assertBrandLinkVisible(): Promise<void> {
    await expect(this.brandLink).toBeVisible();
  }

  async assertPrimaryCtaVisible(): Promise<void> {
    await expect(this.navigationCta).toBeVisible();
  }

  async openAuthModal(): Promise<void> {
    await this.click(this.navigationCta);
    await this.waitForElement(this.authModal);
    await expect(this.tabList).toBeVisible();
  }

  async switchAuthTab(tab: 'login' | 'register'): Promise<void> {
    const targetTab = tab === 'login' ? this.loginTab : this.registerTab;
    await this.click(targetTab);
    await expect(targetTab).toHaveAttribute('aria-selected', 'true');
  }

  async assertRegistrationFormFields(): Promise<void> {
    const modal = this.authModal;
    await this.switchAuthTab('register');

    await expect(modal.locator('input[name="email"]')).toBeVisible();
    await expect(modal.locator('input[name="displayName"]')).toBeVisible();
    await expect(modal.locator('input[name="password"]')).toBeVisible();
    await expect(modal.locator('input[name="confirmPassword"]')).toBeVisible();
    await expect(modal.locator('button[type="submit"]')).toBeVisible();
  }

  async assertLoginFormFields(): Promise<void> {
    const modal = this.authModal;
    await this.switchAuthTab('login');

    await expect(modal.locator('input[name="email"]')).toBeVisible();
    await expect(modal.locator('input[name="password"]')).toBeVisible();
    await expect(modal.locator('button[type="submit"]')).toBeVisible();
  }

  async assertFeaturesOverview(): Promise<void> {
    await this.featuresHeading.scrollIntoViewIfNeeded();
    await expect(this.featuresHeading).toBeVisible();

    await expect(this.getFeatureCardHeading('home.features.upload.title')).toBeVisible();
    await expect(this.getFeatureCardHeading('home.features.ask.title')).toBeVisible();
    await expect(this.getFeatureCardHeading('home.features.play.title')).toBeVisible();
  }
}
