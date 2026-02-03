/**
 * System Configuration - Visual Documentation (Admin Role)
 *
 * Captures visual documentation for system configuration:
 * - Library limits configuration
 * - Upload quotas
 * - Feature flags
 * - System settings
 *
 * @see docs/08-user-flows/admin-role/03-system-configuration.md
 */

import { test } from '../../fixtures';
import { AuthHelper, USER_FIXTURES } from '../../pages';
import {
  ScreenshotHelper,
  ADMIN_FLOWS,
  disableAnimations,
  waitForStableState,
  ANNOTATION_COLORS,
} from '../fixtures/screenshot-helpers';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Mock configuration data
const MOCK_LIBRARY_LIMITS = {
  free: 5,
  normal: 20,
  premium: 50,
};

const MOCK_UPLOAD_QUOTAS = {
  free: { daily: 5, weekly: 20 },
  normal: { daily: 20, weekly: 100 },
  premium: { daily: 100, weekly: 500 },
};

const MOCK_FEATURE_FLAGS = {
  enableOAuth: true,
  enable2FA: true,
  enablePublicCatalog: true,
  enableChatExport: false,
  maintenanceMode: false,
};

test.describe('System Configuration - Visual Documentation (Admin)', () => {
  let helper: ScreenshotHelper;
  let authHelper: AuthHelper;

  test.beforeEach(async ({ page }) => {
    helper = new ScreenshotHelper({
      outputDir: ADMIN_FLOWS.systemConfiguration.outputDir,
      flow: ADMIN_FLOWS.systemConfiguration.name,
      role: ADMIN_FLOWS.systemConfiguration.role,
    });
    authHelper = new AuthHelper(page);
    await disableAnimations(page);

    // Setup admin session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

    // Mock configuration endpoints
    await page.route(`${API_BASE}/api/v1/admin/system/game-library-limits`, async route => {
      if (route.request().method() === 'PUT') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ success: true }),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_LIBRARY_LIMITS),
        });
      }
    });

    await page.route(`${API_BASE}/api/v1/admin/system/upload-quotas`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_UPLOAD_QUOTAS),
      });
    });

    await page.route(`${API_BASE}/api/v1/admin/system/feature-flags`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_FEATURE_FLAGS),
      });
    });
  });

  test('configuration dashboard - overview', async ({ page }) => {
    // Step 1: Navigate to configuration
    await page.goto('/admin/configuration');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'System Configuration',
      description: 'Central hub for all system settings',
      annotations: [
        { selector: 'h1, [data-testid="config-heading"]', label: 'Configuration', color: ANNOTATION_COLORS.primary },
      ],
      nextAction: 'Select configuration area',
    });

    // Step 2: Configuration sections
    const sections = page.locator('[data-testid="config-section"], .config-section, nav').first();
    if (await sections.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Configuration Areas',
        description: 'Different configuration sections available',
        annotations: [
          { selector: 'text=/Library|Limits/i', label: 'Library Limits', color: ANNOTATION_COLORS.info },
          { selector: 'text=/Upload|Quota/i', label: 'Upload Quotas', color: ANNOTATION_COLORS.info },
          { selector: 'text=/Feature|Flags/i', label: 'Feature Flags', color: ANNOTATION_COLORS.info },
        ],
        previousAction: 'View config',
        nextAction: 'Select section',
      });
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Config dashboard captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('library limits - tier configuration', async ({ page }) => {
    // Step 1: Navigate to library limits
    await page.goto('/admin/configuration/library-limits');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Library Limits',
      description: 'Configure game library limits per tier',
      annotations: [
        { selector: 'input[name="free"], [data-tier="free"]', label: 'Free Limit', color: ANNOTATION_COLORS.neutral },
        { selector: 'input[name="normal"], [data-tier="normal"]', label: 'Normal Limit', color: ANNOTATION_COLORS.info },
        { selector: 'input[name="premium"], [data-tier="premium"]', label: 'Premium Limit', color: ANNOTATION_COLORS.success },
      ],
      nextAction: 'Modify limits',
    });

    // Step 2: Edit a limit
    const freeInput = page.locator('input[name="free"], [data-tier="free"] input').first();
    if (await freeInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      await freeInput.clear();
      await freeInput.fill('10');
      await waitForStableState(page);

      await helper.capture(page, {
        step: 2,
        title: 'Limit Modified',
        description: 'Changed Free tier limit from 5 to 10',
        annotations: [
          { selector: 'input[name="free"]', label: 'Updated', color: ANNOTATION_COLORS.warning },
          { selector: 'button[type="submit"], button:has-text("Save")', label: 'Save', color: ANNOTATION_COLORS.success },
        ],
        previousAction: 'Edit limit',
        nextAction: 'Save changes',
      });
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Library limits captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('upload quotas - daily and weekly limits', async ({ page }) => {
    // Step 1: Navigate to upload quotas
    await page.goto('/admin/configuration/upload-quotas');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Upload Quotas',
      description: 'Configure PDF upload quotas per tier',
      annotations: [
        { selector: 'text=/Daily/i', label: 'Daily Limits', color: ANNOTATION_COLORS.info },
        { selector: 'text=/Weekly/i', label: 'Weekly Limits', color: ANNOTATION_COLORS.info },
      ],
      nextAction: 'Modify quotas',
    });

    // Step 2: Quota table/form
    const quotaForm = page.locator('form, table, [data-testid="quota-form"]').first();
    if (await quotaForm.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Quota Configuration',
        description: 'Daily and weekly upload limits by tier',
        annotations: [
          { selector: 'input[name*="daily"], [data-period="daily"]', label: 'Daily', color: ANNOTATION_COLORS.primary },
          { selector: 'input[name*="weekly"], [data-period="weekly"]', label: 'Weekly', color: ANNOTATION_COLORS.primary },
        ],
        previousAction: 'View quotas',
        nextAction: 'Adjust quotas',
      });
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Upload quotas captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('feature flags - toggle features', async ({ page }) => {
    // Step 1: Navigate to feature flags
    await page.goto('/admin/configuration/feature-flags');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Feature Flags',
      description: 'Enable or disable system features',
      annotations: [
        { selector: '[data-testid="feature-oauth"], text=/OAuth/i', label: 'OAuth', color: ANNOTATION_COLORS.success },
        { selector: '[data-testid="feature-2fa"], text=/2FA/i', label: '2FA', color: ANNOTATION_COLORS.success },
      ],
      nextAction: 'Toggle features',
    });

    // Step 2: Feature toggle
    const toggle = page.locator('[role="switch"], input[type="checkbox"]').first();
    if (await toggle.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Feature Toggle',
        description: 'Individual feature toggle control',
        annotations: [
          { selector: '[role="switch"], input[type="checkbox"]', label: 'Toggle', color: ANNOTATION_COLORS.primary },
        ],
        previousAction: 'View flags',
        nextAction: 'Click to toggle',
      });
    }

    // Step 3: Maintenance mode (special)
    const maintenanceToggle = page.locator('text=/Maintenance Mode/i').first();
    if (await maintenanceToggle.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 3,
        title: 'Maintenance Mode',
        description: 'Enable maintenance mode to block user access',
        annotations: [
          { selector: 'text=/Maintenance/i', label: 'Maintenance', color: ANNOTATION_COLORS.error },
        ],
        previousAction: 'View flags',
        nextAction: 'Toggle maintenance',
      });
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ Feature flags captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('save configuration - audit logging', async ({ page }) => {
    // Step 1: Navigate to configuration
    await page.goto('/admin/configuration/library-limits');
    await waitForStableState(page);

    // Make a change
    const input = page.locator('input[name="free"], input[type="number"]').first();
    if (await input.isVisible({ timeout: 2000 }).catch(() => false)) {
      await input.clear();
      await input.fill('15');

      await helper.capture(page, {
        step: 1,
        title: 'Configuration Changed',
        description: 'Configuration modified, pending save',
        annotations: [
          { selector: 'button[type="submit"], button:has-text("Save")', label: 'Save', color: ANNOTATION_COLORS.success },
        ],
        nextAction: 'Save changes',
      });

      // Step 2: Save
      const saveBtn = page.locator('button[type="submit"], button:has-text("Save")').first();
      if (await saveBtn.isVisible({ timeout: 1000 }).catch(() => false)) {
        await saveBtn.click();
        await page.waitForTimeout(500);
        await waitForStableState(page);

        // Step 3: Success message
        const successMsg = page.locator('text=/saved|success|updated/i, [role="alert"]').first();
        if (await successMsg.isVisible({ timeout: 2000 }).catch(() => false)) {
          await helper.capture(page, {
            step: 2,
            title: 'Configuration Saved',
            description: 'Changes saved and logged in audit trail',
            annotations: [
              { selector: 'text=/saved|success/i, [role="alert"]', label: 'Success', color: ANNOTATION_COLORS.success },
            ],
            previousAction: 'Save changes',
            nextAction: 'View audit log',
          });
        }
      }
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Save config captured: ${helper.getCapturedSteps().length} screenshots`);
  });
});
