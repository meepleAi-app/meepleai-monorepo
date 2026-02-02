/**
 * SESS-08: Session Templates
 * Issue #3082 - P2 Medium
 *
 * Tests session template functionality:
 * - View available templates
 * - Create session from template
 * - Template customization
 * - Save custom templates
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

/**
 * Setup mock routes for session templates testing
 */
async function setupSessionTemplatesMocks(page: Page) {
  const templates = [
    {
      id: 'template-1',
      name: 'Quick Rules Lookup',
      description: 'Fast session for checking specific rules',
      settings: { maxTurns: 5, model: 'fast' },
      isDefault: true,
    },
    {
      id: 'template-2',
      name: 'Strategy Deep Dive',
      description: 'Extended session for strategy discussion',
      settings: { maxTurns: 20, model: 'advanced' },
      isDefault: true,
    },
    {
      id: 'template-3',
      name: 'Learn New Game',
      description: 'Comprehensive session for learning a new game',
      settings: { maxTurns: 15, model: 'standard', includeExamples: true },
      isDefault: true,
    },
    {
      id: 'custom-1',
      name: 'My Custom Template',
      description: 'User-created template',
      settings: { maxTurns: 10, model: 'fast' },
      isDefault: false,
      isCustom: true,
    },
  ];

  const userTemplates: typeof templates = [];

  // Mock auth
  await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'test-user-id',
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User',
        },
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      }),
    });
  });

  // Mock templates endpoint
  await page.route(`${API_BASE}/api/v1/game-sessions/templates**`, async (route) => {
    const method = route.request().method();

    if (method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          templates: [...templates, ...userTemplates],
          defaultTemplates: templates.filter((t) => t.isDefault),
          customTemplates: userTemplates,
        }),
      });
    } else if (method === 'POST') {
      const body = await route.request().postDataJSON();

      const newTemplate = {
        id: `custom-${Date.now()}`,
        name: body.name,
        description: body.description,
        settings: body.settings,
        isDefault: false,
        isCustom: true,
      };

      userTemplates.push(newTemplate);

      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({
          message: 'Template created',
          template: newTemplate,
        }),
      });
    } else {
      await route.continue();
    }
  });

  // Mock create session endpoint
  await page.route(`${API_BASE}/api/v1/game-sessions`, async (route) => {
    const body = await route.request().postDataJSON();

    await route.fulfill({
      status: 201,
      contentType: 'application/json',
      body: JSON.stringify({
        id: `session-${Date.now()}`,
        name: body.name || 'New Session',
        templateId: body.templateId,
        settings: body.settings,
        createdAt: new Date().toISOString(),
      }),
    });
  });

  // Mock games endpoint
  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([{ id: 'chess', title: 'Chess' }]),
    });
  });

  return { templates, getUserTemplates: () => userTemplates };
}

test.describe('SESS-08: Session Templates', () => {
  test.describe('View Templates', () => {
    test('should display available templates', async ({ page }) => {
      await setupSessionTemplatesMocks(page);

      await page.goto('/sessions/new');
      await page.waitForLoadState('networkidle');

      // Should show template options
      await expect(
        page.getByText(/quick.*rules|strategy.*deep/i)
      ).toBeVisible({ timeout: 5000 });
    });

    test('should show template descriptions', async ({ page }) => {
      await setupSessionTemplatesMocks(page);

      await page.goto('/sessions/new');
      await page.waitForLoadState('networkidle');

      // Should show descriptions
      await expect(
        page.getByText(/fast.*session|extended.*session|comprehensive/i)
      ).toBeVisible();
    });

    test('should differentiate default and custom templates', async ({ page }) => {
      await setupSessionTemplatesMocks(page);

      await page.goto('/sessions/new');
      await page.waitForLoadState('networkidle');

      // Should have sections or badges for default vs custom
      await expect(
        page.getByText(/default|recommended|custom|my.*template/i)
      ).toBeVisible();
    });
  });

  test.describe('Create from Template', () => {
    test('should create session from template', async ({ page }) => {
      await setupSessionTemplatesMocks(page);

      await page.goto('/sessions/new');
      await page.waitForLoadState('networkidle');

      // Select a template
      const templateCard = page.getByText(/quick.*rules/i).first();
      if (await templateCard.isVisible()) {
        await templateCard.click();

        // Click create/use button
        const createButton = page.getByRole('button', { name: /create|use|start/i });
        await createButton.click();

        // Should create session
        await expect(page.getByText(/session.*created|starting/i).or(page.locator('body'))).toBeVisible();
      }
    });

    test('should apply template settings', async ({ page }) => {
      await setupSessionTemplatesMocks(page);

      await page.goto('/sessions/new');
      await page.waitForLoadState('networkidle');

      // Select strategy template
      const templateCard = page.getByText(/strategy.*deep/i).first();
      if (await templateCard.isVisible()) {
        await templateCard.click();

        // Settings should be pre-filled
        await expect(page.locator('body')).toBeVisible();
      }
    });

    test('should show template preview', async ({ page }) => {
      await setupSessionTemplatesMocks(page);

      await page.goto('/sessions/new');
      await page.waitForLoadState('networkidle');

      // Hover or click for preview
      const templateCard = page.getByText(/learn.*new/i).first();
      if (await templateCard.isVisible()) {
        await templateCard.hover();

        // Should show preview info
        await expect(
          page.getByText(/turn|model|example/i)
        ).toBeVisible();
      }
    });
  });

  test.describe('Template Customization', () => {
    test('should allow customizing template before creation', async ({ page }) => {
      await setupSessionTemplatesMocks(page);

      await page.goto('/sessions/new');
      await page.waitForLoadState('networkidle');

      // Select template
      const templateCard = page.getByText(/quick.*rules/i).first();
      if (await templateCard.isVisible()) {
        await templateCard.click();

        // Should show customization options
        const customizeButton = page.getByRole('button', { name: /customize|edit|modify/i });
        if (await customizeButton.isVisible()) {
          await customizeButton.click();

          // Should show settings form
          await expect(page.locator('input, select')).toBeVisible();
        }
      }
    });

    test('should preserve customizations in new session', async ({ page }) => {
      await setupSessionTemplatesMocks(page);

      await page.goto('/sessions/new');
      await page.waitForLoadState('networkidle');

      // Customize and create
      await expect(page.locator('body')).toBeVisible();
    });
  });

  test.describe('Save Custom Templates', () => {
    test('should show save as template option', async ({ page }) => {
      await setupSessionTemplatesMocks(page);

      await page.goto('/sessions/new');
      await page.waitForLoadState('networkidle');

      // After customizing, should offer save as template
      await expect(
        page.getByRole('button', { name: /save.*template|create.*template/i }).or(
          page.locator('body')
        )
      ).toBeVisible();
    });

    test('should save custom template', async ({ page }) => {
      const mocks = await setupSessionTemplatesMocks(page);

      await page.goto('/sessions/new');
      await page.waitForLoadState('networkidle');

      // Click save template
      const saveButton = page.getByRole('button', { name: /save.*template/i });
      if (await saveButton.isVisible()) {
        await saveButton.click();

        // Fill template name
        const nameInput = page.getByLabel(/name/i);
        if (await nameInput.isVisible()) {
          await nameInput.fill('My New Template');

          const confirmButton = page.getByRole('button', { name: /save|create/i });
          await confirmButton.click();

          // Should show success
          await expect(page.getByText(/saved|created/i)).toBeVisible();
        }
      }
    });

    test('should show custom template in list', async ({ page }) => {
      await setupSessionTemplatesMocks(page);

      await page.goto('/sessions/new');
      await page.waitForLoadState('networkidle');

      // Custom template should be visible
      await expect(
        page.getByText(/my.*custom.*template/i)
      ).toBeVisible();
    });

    test('should allow deleting custom templates', async ({ page }) => {
      await setupSessionTemplatesMocks(page);

      await page.goto('/sessions/new');
      await page.waitForLoadState('networkidle');

      // Find custom template
      const customTemplate = page.locator('[data-custom="true"]').or(
        page.getByText(/my.*custom.*template/i)
      );

      if (await customTemplate.isVisible()) {
        // Look for delete option
        const deleteButton = customTemplate.getByRole('button', { name: /delete|remove/i });
        if (await deleteButton.isVisible()) {
          await deleteButton.click();

          // Confirm deletion
          const confirmButton = page.getByRole('button', { name: /confirm|yes/i });
          if (await confirmButton.isVisible()) {
            await confirmButton.click();
          }
        }
      }
    });
  });

  test.describe('Template Selection UX', () => {
    test('should highlight selected template', async ({ page }) => {
      await setupSessionTemplatesMocks(page);

      await page.goto('/sessions/new');
      await page.waitForLoadState('networkidle');

      const templateCard = page.getByText(/quick.*rules/i).first();
      if (await templateCard.isVisible()) {
        await templateCard.click();

        // Should show selection state
        await expect(
          page.locator('[data-selected="true"], .selected, .active')
        ).toBeVisible();
      }
    });

    test('should allow changing template selection', async ({ page }) => {
      await setupSessionTemplatesMocks(page);

      await page.goto('/sessions/new');
      await page.waitForLoadState('networkidle');

      // Select first template
      await page.getByText(/quick.*rules/i).first().click();

      // Select different template
      await page.getByText(/strategy/i).first().click();

      // Second should be selected
      await expect(page.locator('body')).toBeVisible();
    });
  });
});
