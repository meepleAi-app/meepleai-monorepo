/**
 * Agent Integration E2E Tests - Issue #4230
 *
 * Tests complete agent creation/linking workflow in SharedGame detail page.
 *
 * Flow:
 * 1. Navigate to SharedGame detail page
 * 2. Open AI Agent tab
 * 3. Click "Create Agent" button
 * 4. Fill AgentBuilderModal form
 * 5. Submit and verify agent linked
 * 6. Verify LinkedAgentCard displays
 * 7. Click "Unlink Agent" and confirm
 * 8. Verify "Create Agent" button shown again
 */

import { test, expect } from '@playwright/test';

test.describe('Agent Integration in SharedGame Detail', () => {
  test.beforeEach(async ({ page }) => {
    // Login as admin
    await page.goto('/auth/login');
    await page.fill('input[name="email"]', 'admin@test.com');
    await page.fill('input[name="password"]', 'Admin123!');
    await page.click('button[type="submit"]');
    await page.waitForURL('/admin/**');

    // Navigate to SharedGames list
    await page.goto('/admin/shared-games');
    await page.waitForLoadState('networkidle');

    // Click first game in list to open detail page
    await page.click('table tbody tr:first-child');
    await page.waitForURL(/\/admin\/shared-games\/[a-f0-9-]+/);
  });

  test('should create and link agent to SharedGame', async ({ page }) => {
    // Step 1: Open AI Agent tab
    await page.click('button[role="tab"]:has-text("AI Agent")');
    await expect(page.locator('text=AI Agent')).toBeVisible();

    // Step 2: Verify "Create Agent" button is visible (no agent linked initially)
    const createButton = page.locator('button:has-text("Create Agent")');
    await expect(createButton).toBeVisible();

    // Step 3: Click "Create Agent" to open modal
    await createButton.click();
    await expect(page.locator('text=Create AI Agent for')).toBeVisible();

    // Step 4: Verify form is pre-populated with game context
    const nameInput = page.locator('input[name="name"]');
    await expect(nameInput).toHaveValue(/Arbitro$/); // Ends with "Arbitro"

    const descriptionTextarea = page.locator('textarea[name="description"]');
    const description = await descriptionTextarea.inputValue();
    expect(description).toContain('AI assistant for');

    // Step 5: Verify default model selection
    const modelSelect = page.locator('select[name="model"]');
    await expect(modelSelect).toHaveValue('gpt-4');

    // Step 6: Modify agent name for test uniqueness
    await nameInput.fill(`Test Agent ${Date.now()}`);

    // Step 7: Submit form
    const submitButton = page.locator('button[type="submit"]:has-text("Create")');
    await submitButton.click();

    // Step 8: Wait for success toast
    await expect(page.locator('text=/created and linked/')).toBeVisible({ timeout: 10000 });

    // Step 9: Verify modal closed
    await expect(page.locator('text=Create AI Agent for')).not.toBeVisible();

    // Step 10: Verify LinkedAgentCard is now displayed
    await expect(page.locator('text=Manage Agent')).toBeVisible();
    await expect(page.locator('button:has-text("Unlink")')).toBeVisible();

    // Step 11: Verify "Create Agent" button is gone
    await expect(page.locator('button:has-text("Create Agent")')).not.toBeVisible();
  });

  test('should unlink agent from SharedGame', async ({ page }) => {
    // Prerequisite: Ensure an agent is linked (create one first)
    await page.click('button[role="tab"]:has-text("AI Agent")');

    // If no agent exists, create one first
    const createButton = page.locator('button:has-text("Create Agent")');
    if (await createButton.isVisible()) {
      await createButton.click();
      await page.locator('input[name="name"]').fill(`Agent for Unlinking ${Date.now()}`);
      await page.locator('button[type="submit"]:has-text("Create")').click();
      await expect(page.locator('text=/created and linked/')).toBeVisible({ timeout: 10000 });
    }

    // Step 1: Verify agent is linked
    const unlinkButton = page.locator('button:has-text("Unlink")');
    await expect(unlinkButton).toBeVisible();

    // Step 2: Click "Unlink" button
    await unlinkButton.click();

    // Step 3: Verify confirmation dialog appears
    await expect(page.locator('text=Unlink Agent?')).toBeVisible();
    await expect(page.locator('text=/remove the link between this game/')).toBeVisible();

    // Step 4: Click confirm in dialog
    const confirmButton = page.locator('button:has-text("Unlink Agent")').last();
    await confirmButton.click();

    // Step 5: Wait for success toast
    await expect(page.locator('text=/unlinked successfully/')).toBeVisible({ timeout: 10000 });

    // Step 6: Verify LinkedAgentCard is gone
    await expect(page.locator('button:has-text("Unlink")')).not.toBeVisible();

    // Step 7: Verify "Create Agent" button is back
    await expect(page.locator('button:has-text("Create Agent")')).toBeVisible();
  });

  test('should link existing agent to SharedGame', async ({ page }) => {
    // Step 1: Navigate to AI Agent tab
    await page.click('button[role="tab"]:has-text("AI Agent")');

    // Step 2: Ensure no agent is currently linked
    const unlinkButton = page.locator('button:has-text("Unlink")');
    if (await unlinkButton.isVisible()) {
      await unlinkButton.click();
      await page.locator('button:has-text("Unlink Agent")').last().click();
      await expect(page.locator('text=/unlinked successfully/')).toBeVisible();
    }

    // Step 3: Click "Link Existing Agent" dropdown
    const linkButton = page.locator('button:has-text("Link Existing Agent")');
    await linkButton.click();

    // Step 4: Verify dropdown shows available agents
    await expect(page.locator('text=Available Agents')).toBeVisible();

    // Step 5: Select first available agent (if any exist)
    const firstAgent = page.locator('[cmdk-item]').first();
    const hasAgents = await firstAgent.isVisible();

    if (hasAgents) {
      await firstAgent.click();

      // Step 6: Wait for success toast
      await expect(page.locator('text=/linked successfully/')).toBeVisible({ timeout: 10000 });

      // Step 7: Verify LinkedAgentCard displays
      await expect(page.locator('button:has-text("Unlink")')).toBeVisible();
    } else {
      // No agents available - verify "No agents found" message
      await expect(page.locator('text=No agents found')).toBeVisible();
    }
  });

  test('should show error handling for failed operations', async ({ page }) => {
    // Step 1: Navigate to AI Agent tab
    await page.click('button[role="tab"]:has-text("AI Agent")');

    // Step 2: Open Create Agent modal
    await page.locator('button:has-text("Create Agent")').click();

    // Step 3: Try to submit with invalid data (empty name)
    const nameInput = page.locator('input[name="name"]');
    await nameInput.fill('');

    const submitButton = page.locator('button[type="submit"]:has-text("Create")');
    await submitButton.click();

    // Step 4: Verify validation error appears
    await expect(page.locator('text=/required|must be at least/')).toBeVisible();
  });
});
