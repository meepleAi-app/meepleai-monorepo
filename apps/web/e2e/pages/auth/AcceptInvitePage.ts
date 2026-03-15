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
    // Wait for either:
    // 1. URL changes away from /accept-invite (SPA navigation)
    // 2. Success message appears (may stay on same URL briefly)
    const successOrRedirect = Promise.race([
      this.page.waitForURL(url => !url.pathname.includes('/accept-invite'), {
        timeout: 15_000,
        waitUntil: 'commit',
      }),
      this.page.getByText(/success|account created|welcome/i).waitFor({ timeout: 15_000 }),
    ]);
    await successOrRedirect;
  }
}
