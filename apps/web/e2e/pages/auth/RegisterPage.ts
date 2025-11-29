/**
 * RegisterPage - Page Object for user registration
 *
 * Handles registration form interactions and validation.
 * Compatible with existing BasePage architecture.
 */

import { BasePage } from '../base/BasePage';

export class RegisterPage extends BasePage {
  /**
   * Navigate to registration page (implements abstract goto)
   */
  async goto(): Promise<void> {
    await this.page.goto('/register');
    await this.waitForLoad();
  }

  /**
   * Perform registration
   */
  async register(email: string, password: string, displayName: string): Promise<void> {
    await this.fill(this.page.getByLabel(/email/i), email);
    await this.fill(this.page.getByLabel(/^password$/i), password);
    await this.fill(this.page.getByLabel(/confirm password/i), password);
    await this.fill(this.page.getByLabel(/display name|name/i), displayName);
    await this.click(this.page.getByRole('button', { name: /register|sign up/i }));
  }

  /**
   * Navigate to login page
   */
  async goToLogin(): Promise<void> {
    await this.click(this.page.getByRole('link', { name: /log in|sign in/i }));
  }
}
