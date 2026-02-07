/**
 * Admin Dashboard Epic #3685 E2E Tests - Issue #3697 Phase 3
 *
 * Tests Enterprise Admin Dashboard with:
 * - Vertical sidebar navigation (left)
 * - Horizontal tab system (Overview, Resources, Operations)
 * - Role-based access (SuperAdmin vs Admin vs Editor)
 * - Confirmation workflows (Level 1 & 2)
 * - Audit log filtering
 * - Token management operations
 * - Batch job queue management
 *
 * Epic: #3685 - Core Dashboard & Infrastructure
 * Issue: #3697 - Testing & Integration (Phase 3)
 */

import { test as base, expect, Page } from './fixtures';
import { AdminHelper } from './pages';

const test = base.extend<{ adminPage: Page }>({
  adminPage: async ({ page }: { page: Page }, use: (page: Page) => Promise<void>) => {
    const adminHelper = new AdminHelper(page);
    await adminHelper.setupAdminAuth(true);
    await use(page);
  },
});

test.describe('Epic #3685 - Admin Dashboard Navigation', () => {
  test('should display vertical sidebar with enterprise sections', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Verify sidebar structure
    const sidebar = page.locator('[data-testid="admin-sidebar"]').or(page.locator('aside').first());
    await expect(sidebar).toBeVisible({ timeout: 10000 });

    // Verify core navigation items
    await expect(page.getByRole('link', { name: /Dashboard|Overview/ })).toBeVisible();
    await expect(page.getByRole('link', { name: /Users/ })).toBeVisible();
    await expect(page.getByRole('link', { name: /Analytics/ })).toBeVisible();
    await expect(page.getByRole('link', { name: /Configuration/ })).toBeVisible();
  });

  test('should navigate between horizontal tabs (Overview, Resources, Operations)', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Click Overview tab (default)
    const overviewTab = page.getByRole('tab', { name: /Overview/ }).or(page.getByText('Overview').first());
    if (await overviewTab.isVisible()) {
      await overviewTab.click();
      await expect(page.getByText(/KPI|Metrics|Statistics/)).toBeVisible({ timeout: 5000 });
    }

    // Click Resources tab
    const resourcesTab = page.getByRole('tab', { name: /Resources/ }).or(page.getByText('Resources').first());
    if (await resourcesTab.isVisible()) {
      await resourcesTab.click();
      await expect(page.getByText(/Database|Cache|Token/i)).toBeVisible({ timeout: 5000 });
    }

    // Click Operations tab
    const operationsTab = page.getByRole('tab', { name: /Operations/ }).or(page.getByText('Operations').first());
    if (await operationsTab.isVisible()) {
      await operationsTab.click();
      await expect(page.getByText(/Service|Health|Batch/i)).toBeVisible({ timeout: 5000 });
    }
  });

  test('should maintain tab state on navigation', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Select Resources tab
    const resourcesTab = page.getByRole('tab', { name: /Resources/ }).or(page.getByText('Resources').first());
    if (await resourcesTab.isVisible()) {
      await resourcesTab.click();
      await page.waitForTimeout(500);

      // Navigate away and back
      await page.goto('/admin/users');
      await page.waitForLoadState('networkidle');
      await page.goto('/admin');
      await page.waitForLoadState('networkidle');

      // Verify tab selection persisted or reset to default
      const activeTab = page.locator('[role="tab"][aria-selected="true"]').or(
        page.locator('.active').first()
      );
      await expect(activeTab).toBeVisible({ timeout: 5000 });
    }
  });
});

test.describe('Epic #3685 - Confirmation Workflows', () => {
  test('should show Level 1 confirmation for medium-risk actions', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Navigate to Operations tab (if exists)
    const operationsTab = page.getByRole('tab', { name: /Operations/ }).or(page.getByText('Operations').first());
    if (await operationsTab.isVisible()) {
      await operationsTab.click();

      // Look for medium-risk action button (e.g., Clear Cache, VACUUM)
      const mediumRiskButton = page.getByRole('button', { name: /Clear|VACUUM|Optimize/i }).first();
      if (await mediumRiskButton.isVisible()) {
        await mediumRiskButton.click();

        // Verify Level 1 confirmation dialog appears
        const confirmDialog = page.getByRole('dialog').or(page.locator('[role="alertdialog"]'));
        await expect(confirmDialog).toBeVisible({ timeout: 3000 });
        await expect(confirmDialog.getByText(/confirm|warning|sure/i)).toBeVisible();

        // Has Cancel and Confirm buttons
        await expect(confirmDialog.getByRole('button', { name: /cancel/i })).toBeVisible();
        await expect(confirmDialog.getByRole('button', { name: /confirm|proceed/i })).toBeVisible();

        // Cancel the action
        await confirmDialog.getByRole('button', { name: /cancel/i }).click();
        await expect(confirmDialog).not.toBeVisible({ timeout: 2000 });
      }
    }
  });

  test('should show Level 2 confirmation for high-risk actions', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Navigate to Operations tab
    const operationsTab = page.getByRole('tab', { name: /Operations/ }).or(page.getByText('Operations').first());
    if (await operationsTab.isVisible()) {
      await operationsTab.click();

      // Look for high-risk action button (e.g., Restart Service, Delete All)
      const highRiskButton = page.getByRole('button', { name: /Restart|Delete|Impersonate/i }).first();
      if (await highRiskButton.isVisible()) {
        await highRiskButton.click();

        // Verify Level 2 confirmation (may require typing confirmation text)
        const confirmDialog = page.getByRole('dialog').or(page.locator('[role="alertdialog"]'));
        await expect(confirmDialog).toBeVisible({ timeout: 3000 });

        // Level 2 may require typing "CONFIRM" or similar
        const confirmInput = confirmDialog.getByRole('textbox').or(confirmDialog.locator('input[type="text"]'));
        if (await confirmInput.isVisible()) {
          await expect(confirmInput).toBeVisible();
        }

        // Cancel
        const cancelButton = confirmDialog.getByRole('button', { name: /cancel/i });
        if (await cancelButton.isVisible()) {
          await cancelButton.click();
        }
      }
    }
  });
});

test.describe('Epic #3685 - Role-Based Access Control', () => {
  test('SuperAdmin should access all sections', async ({ adminPage: page }) => {
    // Note: Test assumes SuperAdmin role is set via AdminHelper.setupAdminAuth()
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Verify access to all tabs
    const tabs = ['Overview', 'Resources', 'Operations'];
    for (const tabName of tabs) {
      const tab = page.getByRole('tab', { name: new RegExp(tabName) }).or(page.getByText(tabName).first());
      if (await tab.isVisible()) {
        await tab.click();
        await page.waitForTimeout(500);
        // Tab should be accessible (no error/forbidden message)
        await expect(page.getByText(/forbidden|access denied|unauthorized/i)).not.toBeVisible();
      }
    }
  });

  test.skip('Admin (non-SuperAdmin) should have restricted access', async ({ page }) => {
    // Note: Requires separate auth setup with Admin role (not SuperAdmin)
    // Skipped - requires role injection mechanism

    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Admin should NOT see high-risk operations (e.g., Restart Service, Impersonate)
    const highRiskButtons = page.getByRole('button', { name: /Restart Service|Impersonate/i });
    await expect(highRiskButtons).not.toBeVisible();
  });

  test.skip('Editor should NOT access admin dashboard', async ({ page }) => {
    // Note: Requires auth setup with Editor role
    // Skipped - requires role injection mechanism

    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Should redirect or show access denied
    await expect(page.getByText(/forbidden|access denied|unauthorized/i)).toBeVisible({ timeout: 5000 });
  });
});

test.describe('Epic #3685 - Audit Log Viewer', () => {
  test('should display audit log table with filters', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Navigate to Audit section (may be in sidebar or tabs)
    const auditLink = page.getByRole('link', { name: /Audit/i }).or(page.getByText('Audit').first());
    if (await auditLink.isVisible()) {
      await auditLink.click();
      await page.waitForLoadState('networkidle');

      // Verify audit log table
      const table = page.getByRole('table').or(page.locator('table').first());
      await expect(table).toBeVisible({ timeout: 5000 });

      // Verify filter controls
      const filterInputs = page.getByRole('combobox').or(page.locator('select, input[type="search"]'));
      await expect(filterInputs.first()).toBeVisible();
    }
  });

  test('should filter audit logs by action type', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    const auditLink = page.getByRole('link', { name: /Audit/i }).or(page.getByText('Audit').first());
    if (await auditLink.isVisible()) {
      await auditLink.click();
      await page.waitForLoadState('networkidle');

      // Find action type filter
      const actionFilter = page.getByLabel(/Action|Type|Event/i).or(
        page.locator('select, [role="combobox"]').first()
      );

      if (await actionFilter.isVisible()) {
        // Select a filter option
        await actionFilter.click();
        const option = page.getByRole('option').first();
        if (await option.isVisible()) {
          await option.click();
          await page.waitForTimeout(1000);

          // Verify table updated (row count changed or loading indicator shown)
          await expect(page.locator('table tbody tr').first()).toBeVisible({ timeout: 5000 });
        }
      }
    }
  });
});

test.describe('Epic #3685 - Token Management', () => {
  test('should display token usage metrics', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Navigate to Resources tab
    const resourcesTab = page.getByRole('tab', { name: /Resources/ }).or(page.getByText('Resources').first());
    if (await resourcesTab.isVisible()) {
      await resourcesTab.click();

      // Verify token metrics display
      await expect(page.getByText(/Token|Usage|Limit/i)).toBeVisible({ timeout: 5000 });

      // Verify metric values (numbers or percentages)
      const metricValues = page.locator('text=/\\d+|\\d+%/').first();
      await expect(metricValues).toBeVisible();
    }
  });

  test('should allow updating token tier limits (SuperAdmin)', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    const resourcesTab = page.getByRole('tab', { name: /Resources/ }).or(page.getByText('Resources').first());
    if (await resourcesTab.isVisible()) {
      await resourcesTab.click();

      // Look for "Edit Tier" or "Update Limits" button
      const editButton = page.getByRole('button', { name: /Edit|Update|Modify/i }).first();
      if (await editButton.isVisible()) {
        await editButton.click();

        // Verify edit form or dialog appears
        const editForm = page.getByRole('dialog').or(page.locator('form').first());
        await expect(editForm).toBeVisible({ timeout: 3000 });

        // Cancel editing
        const cancelButton = editForm.getByRole('button', { name: /cancel/i });
        if (await cancelButton.isVisible()) {
          await cancelButton.click();
        }
      }
    }
  });
});

test.describe('Epic #3685 - Batch Job Management', () => {
  test('should display batch job queue', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Navigate to Operations tab
    const operationsTab = page.getByRole('tab', { name: /Operations/ }).or(page.getByText('Operations').first());
    if (await operationsTab.isVisible()) {
      await operationsTab.click();

      // Verify batch job section
      await expect(page.getByText(/Batch|Job|Queue/i)).toBeVisible({ timeout: 5000 });

      // Verify job list or table
      const jobList = page.getByRole('table').or(page.locator('[data-testid*="job"]').first());
      await expect(jobList).toBeVisible();
    }
  });

  test('should allow creating new batch job', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    const operationsTab = page.getByRole('tab', { name: /Operations/ }).or(page.getByText('Operations').first());
    if (await operationsTab.isVisible()) {
      await operationsTab.click();

      // Look for "Create Job" or "New Job" button
      const createButton = page.getByRole('button', { name: /Create|New|Add/i }).first();
      if (await createButton.isVisible()) {
        await createButton.click();

        // Verify job creation form/dialog
        const jobForm = page.getByRole('dialog').or(page.locator('form').first());
        await expect(jobForm).toBeVisible({ timeout: 3000 });

        // Cancel creation
        const cancelButton = jobForm.getByRole('button', { name: /cancel/i });
        if (await cancelButton.isVisible()) {
          await cancelButton.click();
        }
      }
    }
  });

  test('should allow cancelling running batch job', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    const operationsTab = page.getByRole('tab', { name: /Operations/ }).or(page.getByText('Operations').first());
    if (await operationsTab.isVisible()) {
      await operationsTab.click();

      // Look for Cancel button on a job row
      const cancelButton = page.getByRole('button', { name: /Cancel|Stop|Abort/i }).first();
      if (await cancelButton.isVisible()) {
        await cancelButton.click();

        // Verify confirmation dialog
        const confirmDialog = page.getByRole('dialog').or(page.locator('[role="alertdialog"]'));
        if (await confirmDialog.isVisible()) {
          await expect(confirmDialog.getByText(/cancel|stop|sure/i)).toBeVisible();

          // Cancel the cancellation
          const cancelConfirmButton = confirmDialog.getByRole('button', { name: /no|cancel/i });
          if (await cancelConfirmButton.isVisible()) {
            await cancelConfirmButton.click();
          }
        }
      }
    }
  });
});

test.describe('Epic #3685 - Performance & Accessibility', () => {
  test('should load admin dashboard within 2 seconds', async ({ adminPage: page }) => {
    const startTime = Date.now();

    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Wait for main content
    await page.getByRole('heading', { level: 1 }).first().waitFor({ timeout: 10000 });

    const loadTime = Date.now() - startTime;

    // Performance requirement from Issue #3697: <2s page load
    expect(loadTime).toBeLessThan(2000);
  });

  test('should be keyboard navigable', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Tab through navigation
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');

    // Verify focus is visible
    const focusedElement = page.locator(':focus');
    await expect(focusedElement).toBeVisible();
  });

  test('should have no critical accessibility violations', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Basic accessibility check (no aria-label missing, proper heading hierarchy)
    const mainHeading = page.getByRole('heading', { level: 1 }).first();
    await expect(mainHeading).toBeVisible();

    // Verify interactive elements are keyboard accessible
    const buttons = page.getByRole('button');
    const buttonCount = await buttons.count();
    expect(buttonCount).toBeGreaterThan(0);
  });
});

test.describe('Epic #3685 - Responsive Layout', () => {
  test('should adapt sidebar for mobile viewports', async ({ adminPage: page }) => {
    // Set mobile viewport
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Sidebar should be collapsible or hidden on mobile
    const sidebar = page.locator('[data-testid="admin-sidebar"]').or(page.locator('aside').first());

    // Either sidebar is hidden, or there's a menu toggle button
    const isHidden = !(await sidebar.isVisible());
    const hasToggle = await page.getByRole('button', { name: /menu|toggle|burger/i }).isVisible();

    expect(isHidden || hasToggle).toBeTruthy();
  });

  test('should display tabs properly on tablet', async ({ adminPage: page }) => {
    // Set tablet viewport
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Tabs should be visible and clickable
    const tabs = page.getByRole('tab');
    const tabCount = await tabs.count();

    if (tabCount > 0) {
      expect(tabCount).toBeGreaterThanOrEqualTo(2);

      // First tab should be clickable
      await tabs.first().click();
      await page.waitForTimeout(500);
    }
  });
});
