import { Locator, expect } from '@playwright/test';

import { IHomePage } from '../../types/pom-interfaces';
import { BasePage } from '../base/BasePage';

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
    // Italian: "Mai più discussioni sulle regole..."
    // English: similar pattern with "rules" or "AI"
    return this.page.locator('p.text-muted-foreground').filter({ hasText: /regole|rules|AI/i }).first();
  }

  private get brandLink(): Locator {
    return this.page.getByRole('link', { name: /MeepleAI/i });
  }

  private get navigationCta(): Locator {
    // Updated to match HeroSection.tsx which uses 'get-started-button'
    return this.page.getByTestId('get-started-button');
  }

  private get authModal(): Locator {
    // Updated to match AuthModal.tsx which uses 'auth-modal' testId
    return this.page.getByTestId('auth-modal');
  }

  private get tabList(): Locator {
    return this.page.getByRole('tablist');
  }

  private get loginTab(): Locator {
    // AuthModal uses English text "Login" for the tab
    return this.page.getByTestId('auth-tab-login');
  }

  private get registerTab(): Locator {
    // AuthModal uses English text "Register" for the tab
    return this.page.getByTestId('auth-tab-register');
  }

  private get featuresSection(): Locator {
    return this.page.locator('#features');
  }

  private get featuresHeading(): Locator {
    // FeaturesSection.tsx uses hardcoded Italian text "Caratteristiche"
    return this.featuresSection.getByRole('heading', {
      name: /Caratteristiche|Features/i,
    });
  }

  private getFeatureCardTitle(title: string): Locator {
    // FeaturesSection uses CardTitle (div) with font-heading class
    // Simply find text within the features section
    return this.featuresSection.getByText(new RegExp(title, 'i')).first();
  }

  async goto(): Promise<void> {
    await this.page.goto('/');
    await this.waitForLoad();
  }

  async assertHeroLoaded(): Promise<void> {
    await this.waitForElement(this.heroHeading);
    // Italian: "Il tuo Assistente AI per Regolamenti..."
    // Match key terms: AI, Assistente/Assistant, Giochi/Games
    await expect(this.heroHeading).toContainText(/AI|Assistente|Assistant/i);
    await expect(this.heroSubtitle).toBeVisible();
  }

  async assertBrandLinkVisible(): Promise<void> {
    await expect(this.brandLink).toBeVisible();
  }

  async assertPrimaryCtaVisible(): Promise<void> {
    await expect(this.navigationCta).toBeVisible();
  }

  async openAuthModal(): Promise<void> {
    // Click CTA navigates to /register page which shows AuthModal
    await this.click(this.navigationCta);
    // Wait for navigation to /register and modal to appear
    await this.page.waitForURL(/\/(register|login)/);
    await this.waitForElement(this.authModal);
  }

  async switchAuthTab(tab: 'login' | 'register'): Promise<void> {
    const targetTab = tab === 'login' ? this.loginTab : this.registerTab;
    await this.click(targetTab);
    await expect(targetTab).toHaveAttribute('aria-selected', 'true');
  }

  async assertRegistrationFormFields(): Promise<void> {
    await this.switchAuthTab('register');

    // Updated to use testIds from RegisterForm.tsx
    await expect(this.page.getByTestId('register-email')).toBeVisible();
    await expect(this.page.getByTestId('register-display-name')).toBeVisible();
    await expect(this.page.getByTestId('register-password')).toBeVisible();
    await expect(this.page.getByTestId('register-confirm-password')).toBeVisible();
    await expect(this.page.getByTestId('register-form').locator('button[type="submit"]')).toBeVisible();
  }

  async assertLoginFormFields(): Promise<void> {
    await this.switchAuthTab('login');

    // Updated to use testIds from LoginForm.tsx
    await expect(this.page.getByTestId('login-email')).toBeVisible();
    await expect(this.page.getByTestId('login-password')).toBeVisible();
    await expect(this.page.getByTestId('login-form').locator('button[type="submit"]')).toBeVisible();
  }

  async assertFeaturesOverview(): Promise<void> {
    await this.featuresHeading.scrollIntoViewIfNeeded();
    await expect(this.featuresHeading).toBeVisible();

    // FeaturesSection.tsx uses hardcoded Italian titles
    await expect(this.getFeatureCardTitle('AI Intelligente')).toBeVisible();
    await expect(this.getFeatureCardTitle('Catalogo Ampio')).toBeVisible();
    await expect(this.getFeatureCardTitle('Mobile-First')).toBeVisible();
  }
}
