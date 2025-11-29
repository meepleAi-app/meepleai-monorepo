/**
 * LoginPage - Page Object for authentication login page
 *
 * Handles all login-related interactions.
 * Compatible with existing BasePage architecture.
 */

import { BasePage } from '../base/BasePage';

export class LoginPage extends BasePage {
  /**
   * Navigate to login page (implements abstract goto)
   */
  async goto(): Promise<void> {
    await this.page.goto('/login');
    await this.waitForLoad();
  }

  /**
   * Alternative navigate method for compatibility
   */
  async navigate(): Promise<void> {
    await this.goto();
  }

  /**
   * Perform login with email and password
   */
  async login(email: string, password: string): Promise<void> {
    await this.fill(this.page.getByLabel(/email/i), email);
    await this.fill(this.page.getByLabel(/password/i), password);
    await this.click(this.page.getByRole('button', { name: /log in|sign in/i }));
  }

  /**
   * Click OAuth provider button
   */
  async clickOAuthProvider(provider: 'google' | 'github' | 'discord'): Promise<void> {
    await this.click(this.page.getByRole('button', { name: new RegExp(provider, 'i') }));
  }

  /**
   * Navigate to registration page
   */
  async goToRegister(): Promise<void> {
    await this.click(this.page.getByRole('link', { name: /register|sign up/i }));
  }

  /**
   * Navigate to forgot password page
   */
  async goToForgotPassword(): Promise<void> {
    await this.click(this.page.getByRole('link', { name: /forgot password/i }));
  }

  /**
   * Verify all OAuth buttons are visible
   */
  async verifyOAuthButtonsVisible(): Promise<void> {
    await this.waitForElement(this.page.getByRole('button', { name: /google/i }));
    await this.waitForElement(this.page.getByRole('button', { name: /github/i }));
    await this.waitForElement(this.page.getByRole('button', { name: /discord/i }));
  }
}
