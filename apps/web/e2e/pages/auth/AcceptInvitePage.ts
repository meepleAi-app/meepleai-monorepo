import { type Page, expect } from '@playwright/test';

import { BasePage } from '../base/BasePage';

export class AcceptInvitePage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  async goto(): Promise<void> {
    await this.page.goto('/accept-invite');
    await this.waitForLoad();
  }

  async gotoWithToken(token: string): Promise<void> {
    await this.page.goto(`/accept-invite?token=${token}`);
    await this.waitForLoad();
  }

  async gotoUrl(url: string): Promise<void> {
    await this.page.goto(url);
    await this.waitForLoad();
  }

  async setPassword(password: string): Promise<void> {
    await this.fill(this.page.getByLabel(/^password/i), password);
    await this.fill(this.page.getByLabel(/confirm password/i), password);
  }

  async verifyStrengthIndicator(expectedLevel: string): Promise<void> {
    await expect(this.page.getByText(new RegExp(`Strength:.*${expectedLevel}`, 'i'))).toBeVisible();
  }

  async submit(): Promise<void> {
    await this.click(this.page.getByRole('button', { name: /create account/i }));
  }

  async waitForRedirectAfterAccept(): Promise<void> {
    // Accept-invite may auto-login and redirect immediately, or show success then redirect.
    // Just wait until we're no longer on the accept-invite page.
    const maxWait = Date.now() + 15_000;
    while (Date.now() < maxWait) {
      const url = this.page.url();
      if (!url.includes('/accept-invite')) return;
      await this.page.waitForTimeout(500);
    }
    // If still on accept-invite after 15s, check for success message
    const hasSuccess = await this.page
      .getByText(/success|account created|welcome/i)
      .isVisible()
      .catch(() => false);
    if (hasSuccess) return;
    throw new Error(`Still on accept-invite page after 15s: ${this.page.url()}`);
  }
}
