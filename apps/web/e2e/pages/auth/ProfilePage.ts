/**
 * ProfilePage - Page Object for user profile management
 *
 * Handles profile viewing and editing operations.
 * Compatible with existing BasePage architecture.
 */

import { BasePage } from '../base/BasePage';

export class ProfilePage extends BasePage {
  /**
   * Navigate to profile page (implements abstract goto)
   */
  async goto(): Promise<void> {
    await this.page.goto('/profile');
    await this.waitForLoad();
  }

  /**
   * Update display name
   */
  async updateDisplayName(newName: string): Promise<void> {
    await this.fill(this.page.getByLabel(/display name/i), newName);
    await this.click(this.page.getByRole('button', { name: /save|update/i }));
  }

  /**
   * Change password
   */
  async changePassword(currentPassword: string, newPassword: string): Promise<void> {
    await this.click(this.page.getByRole('button', { name: /change password/i }));
    await this.fill(this.page.getByLabel(/current password/i), currentPassword);
    await this.fill(this.page.getByLabel(/new password/i), newPassword);
    await this.fill(this.page.getByLabel(/confirm new password/i), newPassword);
    await this.click(this.page.getByRole('button', { name: /save|update/i }));
  }

  /**
   * Get current display name value
   */
  async getDisplayName(): Promise<string> {
    return await this.page.getByLabel(/display name/i).inputValue();
  }
}
