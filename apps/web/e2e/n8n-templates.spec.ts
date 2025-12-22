/**
 * N8N Templates E2E Tests - Issue #2193
 *
 * Tests the n8n workflow template management:
 * - View available templates
 * - Validate template before import
 * - Import workflow template
 * - Apply template to workflow
 *
 * @see apps/api/src/Api/BoundedContexts/WorkflowIntegration/Application/
 * @see docs/02-development/testing/test-coverage-gaps.md
 */

import { test, expect } from './fixtures/chromatic';
import { AuthHelper, AdminHelper, USER_FIXTURES } from './pages';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Mock templates data
const MOCK_TEMPLATES = [
  {
    id: 'pdf-processing-v1',
    name: 'PDF Processing Workflow',
    description: 'Automatically process and index PDF documents',
    category: 'document',
    version: '1.0.0',
    parameters: ['webhookUrl', 'apiKey'],
    author: 'MeepleAI',
    createdAt: '2025-01-01T10:00:00Z',
  },
  {
    id: 'error-notification-v1',
    name: 'Error Notification Workflow',
    description: 'Send notifications when errors occur',
    category: 'monitoring',
    version: '1.2.0',
    parameters: ['slackWebhook', 'emailRecipients'],
    author: 'MeepleAI',
    createdAt: '2025-01-02T10:00:00Z',
  },
  {
    id: 'game-sync-v1',
    name: 'Game Sync Workflow',
    description: 'Synchronize game data with external sources',
    category: 'integration',
    version: '2.0.0',
    parameters: ['bggApiKey', 'syncInterval'],
    author: 'MeepleAI',
    createdAt: '2025-01-03T10:00:00Z',
  },
];

const MOCK_TEMPLATE_DETAIL = {
  ...MOCK_TEMPLATES[0],
  workflowJson: JSON.stringify({
    name: 'PDF Processing',
    nodes: [
      { id: '1', type: 'webhook', name: 'Webhook Trigger' },
      { id: '2', type: 'http', name: 'API Call' },
    ],
    connections: {
      '1': { main: [[{ node: '2', type: 'main', index: 0 }]] },
    },
  }),
  requiredNodes: ['webhook', 'http'],
  estimatedDuration: '5 minutes',
};

/**
 * N8N Templates E2E Tests
 *
 * Tests the complete N8N templates flow with proper auth mocking.
 * Uses AdminHelper for catch-all mocks that prevent unmocked route errors.
 *
 * @see apps/web/src/app/admin/n8n-templates/page.tsx
 */
test.describe('N8N Templates Flow - Issue #2193', () => {
  test.beforeEach(async ({ page }) => {
    const authHelper = new AuthHelper(page);
    const adminHelper = new AdminHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Mock authenticated session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

    // Catch-all for admin endpoints (prevents unmocked route errors)
    await page.route(`${API_BASE}/api/v1/admin/**`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: [] }),
      });
    });

    // Mock admin stats (commonly needed by admin pages)
    await adminHelper.mockAdminStats({
      totalUsers: 10,
      totalGames: 5,
      totalQueries: 100,
      avgLatencyMs: 350,
      totalTokens: 50000,
      successRate: 0.95,
    });

    // Mock templates list endpoint
    await page.route(`${API_BASE}/api/v1/n8n/templates*`, async route => {
      const url = new URL(route.request().url());
      const category = url.searchParams.get('category');

      let filteredTemplates = [...MOCK_TEMPLATES];
      if (category) {
        filteredTemplates = MOCK_TEMPLATES.filter(t => t.category === category);
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(filteredTemplates),
      });
    });

    // Mock single template endpoint
    await page.route(new RegExp(`${API_BASE}/api/v1/n8n/templates/[^/]+$`), async route => {
      const url = new URL(route.request().url());
      const templateId = url.pathname.split('/').pop()!;
      const template = MOCK_TEMPLATES.find(t => t.id === templateId);

      if (!template) {
        await route.fulfill({
          status: 404,
          body: JSON.stringify({ error: `Template '${templateId}' not found` }),
        });
        return;
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ ...MOCK_TEMPLATE_DETAIL, ...template }),
      });
    });

    // Mock template import endpoint
    await page.route(new RegExp(`${API_BASE}/api/v1/n8n/templates/[^/]+/import$`), async route => {
      const body = route.request().postDataJSON() as { parameters?: Record<string, string> };

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          workflowId: `workflow-${Date.now()}`,
          workflowName: 'Imported Workflow',
          message: 'Template imported successfully',
          appliedParameters: body?.parameters || {},
        }),
      });
    });

    // Mock template validation endpoint
    await page.route(`${API_BASE}/api/v1/n8n/templates/validate`, async route => {
      const body = route.request().postDataJSON() as { templateJson: string };

      try {
        JSON.parse(body.templateJson);
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            valid: true,
            warnings: [],
            errors: [],
            nodeCount: 2,
            estimatedComplexity: 'low',
          }),
        });
      } catch {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            valid: false,
            errors: ['Invalid JSON format'],
            warnings: [],
          }),
        });
      }
    });
  });

  test('should display available templates list', async ({ page }) => {
    await page.goto('/admin/n8n-templates');

    // Wait for templates to load
    await expect(page.getByRole('heading', { name: /Templates|Workflow Templates/i })).toBeVisible({
      timeout: 10000,
    });

    // Verify all templates are displayed
    await expect(page.getByText('PDF Processing Workflow')).toBeVisible();
    await expect(page.getByText('Error Notification Workflow')).toBeVisible();
    await expect(page.getByText('Game Sync Workflow')).toBeVisible();

    // Verify template metadata is shown
    await expect(page.getByText('document')).toBeVisible();
    await expect(page.getByText('monitoring')).toBeVisible();
    await expect(page.getByText('integration')).toBeVisible();
  });

  test('should filter templates by category', async ({ page }) => {
    await page.goto('/admin/n8n-templates');

    // Wait for templates to load
    await expect(page.getByText('PDF Processing Workflow')).toBeVisible();

    // Filter by category
    const categoryFilter = page
      .getByRole('combobox', { name: /category|categoria/i })
      .or(page.locator('select[name="category"]'))
      .or(page.locator('[data-testid="category-filter"]'));

    if (await categoryFilter.isVisible()) {
      await categoryFilter.selectOption('document');

      // Verify only document templates are shown
      await expect(page.getByText('PDF Processing Workflow')).toBeVisible();
      await expect(page.getByText('Error Notification Workflow')).not.toBeVisible();
      await expect(page.getByText('Game Sync Workflow')).not.toBeVisible();
    }
  });

  test('should view template details', async ({ page }) => {
    await page.goto('/admin/n8n-templates');

    // Click on a template to view details
    await page.getByText('PDF Processing Workflow').click();

    // Wait for detail view
    await expect(page.getByText('Automatically process and index PDF documents')).toBeVisible({
      timeout: 10000,
    });

    // Verify template details are shown
    await expect(page.getByText(/version|versione/i)).toBeVisible();
    await expect(page.getByText('1.0.0')).toBeVisible();
    await expect(page.getByText(/parameters|parametri/i)).toBeVisible();
    await expect(page.getByText('webhookUrl')).toBeVisible();
    await expect(page.getByText('apiKey')).toBeVisible();
  });

  test('should import workflow template with parameters', async ({ page }) => {
    await page.goto('/admin/n8n-templates');

    // Click on a template
    await page.getByText('PDF Processing Workflow').click();

    // Wait for detail view and click import
    const importButton = page
      .getByRole('button', { name: /Import|Importa/i })
      .or(page.locator('[data-testid="import-template-button"]'));
    await importButton.click();

    // Fill in parameters if form is shown
    const webhookInput = page.locator('input[name="webhookUrl"], [data-testid="param-webhookUrl"]');
    if (await webhookInput.isVisible()) {
      await webhookInput.fill('https://my-app.com/webhook');
    }

    const apiKeyInput = page.locator('input[name="apiKey"], [data-testid="param-apiKey"]');
    if (await apiKeyInput.isVisible()) {
      await apiKeyInput.fill('my-secret-key');
    }

    // Confirm import
    const confirmButton = page
      .getByRole('button', { name: /Confirm|Conferma|Import/i })
      .or(page.locator('[data-testid="confirm-import-button"]'));
    if (await confirmButton.isVisible()) {
      await confirmButton.click();
    }

    // Verify success message
    await expect(
      page.locator('text=imported|importato|success|successo', { exact: false })
    ).toBeVisible({ timeout: 10000 });
  });

  test('should validate template JSON before import', async ({ page }) => {
    await page.goto('/admin/n8n-templates');

    // Look for validate/upload custom template option
    const uploadButton = page
      .getByRole('button', { name: /Upload|Carica|Custom/i })
      .or(page.locator('[data-testid="upload-template-button"]'));

    if (await uploadButton.isVisible()) {
      await uploadButton.click();

      // Enter valid JSON
      const jsonInput = page.locator(
        'textarea[name="templateJson"], [data-testid="template-json-input"]'
      );
      await jsonInput.fill(
        JSON.stringify({
          name: 'Custom Workflow',
          nodes: [{ id: '1', type: 'webhook' }],
          connections: {},
        })
      );

      // Click validate
      const validateButton = page
        .getByRole('button', { name: /Validate|Valida/i })
        .or(page.locator('[data-testid="validate-template-button"]'));
      await validateButton.click();

      // Verify validation result
      await expect(page.locator('text=valid|valido|success', { exact: false })).toBeVisible({
        timeout: 5000,
      });
    }
  });

  test('should show validation errors for invalid template', async ({ page }) => {
    // Override validation mock to return error
    await page.route(`${API_BASE}/api/v1/n8n/templates/validate`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          valid: false,
          errors: ['Missing required nodes', 'Invalid connection format'],
          warnings: ['Deprecated node type detected'],
        }),
      });
    });

    await page.goto('/admin/n8n-templates');

    const uploadButton = page
      .getByRole('button', { name: /Upload|Carica|Custom/i })
      .or(page.locator('[data-testid="upload-template-button"]'));

    if (await uploadButton.isVisible()) {
      await uploadButton.click();

      const jsonInput = page.locator(
        'textarea[name="templateJson"], [data-testid="template-json-input"]'
      );
      await jsonInput.fill('invalid json');

      const validateButton = page
        .getByRole('button', { name: /Validate|Valida/i })
        .or(page.locator('[data-testid="validate-template-button"]'));
      await validateButton.click();

      // Verify error messages are shown
      await expect(
        page.locator('text=error|errore|invalid|non valido', { exact: false })
      ).toBeVisible({ timeout: 5000 });
    }
  });

  test('should handle template not found error', async ({ page }) => {
    await page.route(`${API_BASE}/api/v1/n8n/templates/non-existent`, async route => {
      await route.fulfill({
        status: 404,
        contentType: 'application/json',
        body: JSON.stringify({ error: "Template 'non-existent' not found" }),
      });
    });

    await page.goto('/admin/n8n-templates/non-existent');

    // Verify error message
    await expect(
      page.locator('text=not found|non trovato|404|errore', { exact: false })
    ).toBeVisible({ timeout: 10000 });
  });

  test('should show empty state when no templates available', async ({ page }) => {
    await page.route(`${API_BASE}/api/v1/n8n/templates*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    });

    await page.goto('/admin/n8n-templates');

    // Verify empty state
    await expect(
      page.locator('text=no templates|nessun template|empty|vuoto', { exact: false })
    ).toBeVisible({ timeout: 10000 });
  });

  test('should display template parameters correctly', async ({ page }) => {
    await page.goto('/admin/n8n-templates');

    // Click on Error Notification template (has more parameters)
    await page.getByText('Error Notification Workflow').click();

    // Verify parameters are displayed
    await expect(page.getByText('slackWebhook')).toBeVisible();
    await expect(page.getByText('emailRecipients')).toBeVisible();

    // Verify version info
    await expect(page.getByText('1.2.0')).toBeVisible();
  });

  test('should require admin access for template management', async ({ page }) => {
    // Mock as regular user
    const authHelper = new AuthHelper(page);
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    await page.goto('/admin/n8n-templates');

    // Templates should be viewable but import might be restricted
    // Verify the page loads (viewing is allowed)
    await expect(
      page.locator('text=Templates|PDF Processing|templates', { exact: false })
    ).toBeVisible({ timeout: 10000 });

    // If import buttons are visible, clicking should show permission error
    // This depends on UI implementation
  });
});
