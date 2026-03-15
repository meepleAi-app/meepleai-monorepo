import { type Page, expect } from '@playwright/test';

import { BasePage } from '../base/BasePage';

export class AdminUsersPage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  async goto(): Promise<void> {
    await this.page.goto('/admin/users');
    await this.waitForLoad();
  }

  async clickInviteButton(): Promise<void> {
    await this.click(
      this.page
        .locator('[data-testid="invite-user-button"]')
        .or(this.page.getByRole('button', { name: /invite/i }))
    );
  }

  async fillInvitationForm(email: string, role: string = 'user'): Promise<void> {
    await this.fill(this.page.getByLabel(/email/i), email);
    const roleSelect = this.page
      .locator('[data-testid="invite-role-select"]')
      .or(this.page.getByLabel(/role/i));
    if (await roleSelect.isVisible()) {
      await roleSelect.click();
      await this.page.getByRole('option', { name: new RegExp(role, 'i') }).click();
    }
  }

  async submitInvitation(): Promise<void> {
    await this.click(
      this.page
        .locator('[data-testid="send-invitation-button"]')
        .or(this.page.getByRole('button', { name: /send invitation|invite/i }))
    );
  }

  async findUserRow(email: string): Promise<ReturnType<Page['locator']>> {
    return this.page.locator(`tr, [data-testid="user-row"]`).filter({
      hasText: email,
    });
  }

  async changeUserRole(email: string, newRole: string): Promise<void> {
    const userRow = await this.findUserRow(email);
    const roleSelect = userRow
      .locator('[data-testid="inline-role-select"]')
      .or(userRow.getByRole('combobox'));
    await roleSelect.click();
    await this.page.getByRole('option', { name: new RegExp(newRole, 'i') }).click();
  }

  async verifyUserRole(email: string, expectedRole: string): Promise<void> {
    const userRow = await this.findUserRow(email);
    await expect(userRow).toContainText(new RegExp(expectedRole, 'i'));
  }
}
