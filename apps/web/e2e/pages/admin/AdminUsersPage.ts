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
    await this.click(this.page.getByRole('button', { name: /invite user/i }));
  }

  async fillInvitationForm(email: string, role: string = 'user'): Promise<void> {
    await this.fill(this.page.getByLabel(/email/i), email);
    // Role selector (if visible) - use click+option pattern for shadcn
    const roleSelect = this.page
      .locator('[data-testid="invite-role-select"]')
      .or(this.page.getByLabel(/role/i));
    if (await roleSelect.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await roleSelect.click();
      await this.page.getByRole('option', { name: new RegExp(role, 'i') }).click();
    }
  }

  async submitInvitation(): Promise<void> {
    await this.click(this.page.getByRole('button', { name: /send invitation|invite|invia/i }));
  }

  async findUserRow(email: string): Promise<ReturnType<Page['locator']>> {
    return this.page.locator('tr, [data-testid="user-row"]').filter({
      hasText: email,
    });
  }

  async changeUserRole(email: string, newRole: string): Promise<void> {
    const userRow = await this.findUserRow(email);
    // InlineRoleSelect is a small Select component (w-[100px], h-7)
    const roleSelect = userRow.locator('select, [role="combobox"]').first();
    await roleSelect.click();
    await this.page.getByRole('option', { name: new RegExp(`^${newRole}$`, 'i') }).click();

    // Wait for confirmation dialog "Change Role"
    const confirmDialog = this.page.locator('[role="alertdialog"]');
    if (await confirmDialog.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await this.page.getByRole('button', { name: /confirm/i }).click();
    }
    await this.waitForNetworkIdle();
  }

  async verifyUserRole(email: string, expectedRole: string): Promise<void> {
    const userRow = await this.findUserRow(email);
    await expect(userRow).toContainText(new RegExp(expectedRole, 'i'));
  }
}
