import { type Page, expect } from '@playwright/test';

import { BasePage } from '../base/BasePage';

export class AuditLogPage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  async goto(): Promise<void> {
    await this.page.goto('/admin/analytics?tab=audit');
    await this.waitForLoad();
  }

  async verifyRoleChangeEntry(
    targetIdentifier: string,
    options?: { oldRole?: string; newRole?: string }
  ): Promise<void> {
    await this.waitForNetworkIdle();

    const auditEntries = this.page.locator('[data-testid="audit-log-entry"], tr, [role="row"]');

    const matchingEntry = auditEntries
      .filter({
        hasText: targetIdentifier,
      })
      .filter({
        hasText: /role/i,
      });

    await expect(matchingEntry.first()).toBeVisible({ timeout: 10_000 });

    if (options?.newRole) {
      await expect(matchingEntry.first()).toContainText(new RegExp(options.newRole, 'i'));
    }
  }
}
