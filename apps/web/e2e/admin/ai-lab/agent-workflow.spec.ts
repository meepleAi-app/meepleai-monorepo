import { test, expect } from '@playwright/test';

/**
 * E2E Test: AI Lab - Agent Creation Workflow
 * Issue #3819 (Epic #3687)
 */

test.describe('AI Lab - Agent Workflow', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to agent builder
    await page.goto('/admin/agent-definitions');
  });

  test('should create new agent definition', async ({ page }) => {
    // Click create button
    await page.click('text=Create Agent');
    await expect(page).toHaveURL(/\/admin\/agent-definitions\/create/);

    // Fill form
    await page.fill('input[name="name"]', 'E2E Test Agent');
    await page.fill('textarea[name="description"]', 'Created by E2E test');
    await page.selectOption('select[name="model"]', 'gpt-4');

    // Submit
    await page.click('button[type="submit"]');

    // Verify redirect to list
    await expect(page).toHaveURL('/admin/agent-definitions');
    await expect(page.locator('text=E2E Test Agent')).toBeVisible();
  });

  test('should navigate to playground', async ({ page }) => {
    await page.goto('/admin/agent-definitions/playground');
    await expect(page.locator('h1:has-text("Agent Playground")')).toBeVisible();
  });

  test('should load strategy editor', async ({ page }) => {
    await page.goto('/admin/strategies');
    await expect(page.locator('text=Create Strategy')).toBeVisible();
  });
});
