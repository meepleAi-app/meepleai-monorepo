import { test, expect } from '@playwright/test';

type N8nConfig = {
  id: string;
  name: string;
  baseUrl: string;
  webhookUrl: string | null;
  isActive: boolean;
  lastTestedAt: string | null;
  lastTestResult: string | null;
  createdAt: string;
  updatedAt: string;
};

const apiBase = 'http://localhost:8080';

type N8nConfig = {
  id: string;
  name: string;
  baseUrl: string;
  webhookUrl: string | null;
  isActive: boolean;
  lastTestedAt: string | null;
  lastTestResult: string | null;
  createdAt: string;
  updatedAt: string;
};

test.describe('n8n workflow management', () => {
  test('allows full lifecycle management of n8n configurations', async ({ page }) => {
    const configs: N8nConfig[] = [
      {
        id: 'cfg-1',
        name: 'Production n8n',
        baseUrl: 'https://n8n.example.com',
        webhookUrl: 'https://n8n.example.com/webhook',
        isActive: true,
        lastTestedAt: new Date('2025-01-05T09:00:00Z').toISOString(),
        lastTestResult: 'Last test successful (120ms)',
        createdAt: new Date('2025-01-01T10:00:00Z').toISOString(),
        updatedAt: new Date('2025-01-05T09:05:00Z').toISOString()
      }
    ];

    await page.addInitScript(() => {
      (window as any).__alerts = [] as string[];
      window.alert = (message?: any) => {
        (window as any).__alerts.push(String(message));
      };
      window.confirm = (message?: string) => {
        (window as any).__confirmMessages = ((window as any).__confirmMessages ?? []) as string[];
        (window as any).__confirmMessages.push(String(message ?? ''));
        return true;
      };
    });

    await page.route(`${apiBase}/admin/n8n`, async (route) => {
      const method = route.request().method();
      if (method === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ configs })
        });
        return;
      }

      if (method === 'POST') {
        const body = route.request().postDataJSON() as {
          name: string;
          baseUrl: string;
          apiKey: string;
          webhookUrl: string | null;
        };

        const newConfig: N8nConfig = {
          id: `cfg-${configs.length + 1}`,
          name: body.name,
          baseUrl: body.baseUrl,
          webhookUrl: body.webhookUrl,
          isActive: true,
          lastTestedAt: null,
          lastTestResult: null,
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString()
        };
        configs.push(newConfig);

        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(newConfig)
        });
        return;
      }

      await route.fulfill({ status: 405, body: 'Method not allowed' });
    });

    await page.route(new RegExp(`${apiBase}/admin/n8n/([^/]+)$`), async (route) => {
      const url = new URL(route.request().url());
      const id = url.pathname.split('/').pop()!;
      const config = configs.find((c) => c.id === id);

      if (!config) {
        await route.fulfill({ status: 404, body: 'Not found' });
        return;
      }

      const method = route.request().method();
      if (method === 'PUT') {
        const body = route.request().postDataJSON() as Partial<N8nConfig> & { apiKey?: string };
        if (body.name !== undefined) {
          config.name = body.name;
        }
        if (body.baseUrl !== undefined) {
          config.baseUrl = body.baseUrl;
        }
        if (body.webhookUrl !== undefined) {
          config.webhookUrl = body.webhookUrl;
        }
        if (body.isActive !== undefined) {
          config.isActive = body.isActive;
        }
        config.updatedAt = new Date().toISOString();

        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(config)
        });
        return;
      }

      if (method === 'DELETE') {
        const index = configs.findIndex((c) => c.id === id);
        if (index >= 0) {
          configs.splice(index, 1);
        }
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ success: true })
        });
        return;
      }

      await route.fulfill({ status: 405, body: 'Method not allowed' });
    });

    await page.route(new RegExp(`${apiBase}/admin/n8n/([^/]+)/test$`), async (route) => {
      const url = new URL(route.request().url());
      const id = url.pathname.split('/').slice(-2, -1)[0];
      const config = configs.find((c) => c.id === id);
      if (!config) {
        await route.fulfill({ status: 404, body: 'Not found' });
        return;
      }

      config.lastTestedAt = new Date().toISOString();
      config.lastTestResult = `Test successful for ${config.name}`;

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true, message: config.lastTestResult, latencyMs: 85 })
      });
    });

    await page.goto('/n8n');

    await expect(page.getByRole('heading', { name: 'n8n Workflow Management' })).toBeVisible();
    await expect(page.getByText('Production n8n')).toBeVisible();
    await expect(page.getByText('Active')).toBeVisible();

    await page.getByRole('button', { name: 'Test' }).click();

    await expect(page.getByText('Test successful for Production n8n')).toBeVisible();

    const alerts = await page.evaluate(() => (window as any).__alerts as string[]);
    expect(alerts.some((message) => message.includes('Test successful for Production n8n'))).toBeTruthy();

    await page.getByRole('button', { name: 'Deactivate' }).click();
    await expect(page.getByText('Inactive')).toBeVisible();

    await page.getByRole('button', { name: 'Edit' }).click();
    await expect(page.getByText('Edit Configuration')).toBeVisible();

    await page.getByLabel('Name *').fill('Production n8n Updated');
    await page.getByLabel('Base URL *').fill('https://n8n.example.com/updated');
    await page.getByLabel('API Key (leave empty to keep current)').fill('new-api-key');
    await page.getByLabel('Webhook URL (optional)').fill('https://n8n.example.com/new-webhook');
    await page.getByRole('button', { name: 'Update Configuration' }).click();

    await expect(page.getByText('Production n8n Updated')).toBeVisible();
    await expect(page.getByText('https://n8n.example.com/updated')).toBeVisible();
    await expect(page.getByText('Webhook: https://n8n.example.com/new-webhook')).toBeVisible();

    await page.getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText('No n8n configurations found. Click "Add Configuration" to create one.')).toBeVisible();

    await page.getByRole('button', { name: 'Add Configuration' }).click();
    await expect(page.getByText('New Configuration')).toBeVisible();

    await page.getByPlaceholder('Production n8n').fill('Staging n8n');
    await page.getByPlaceholder('http://localhost:5678').fill('https://staging.n8n.example.com');
    await page.getByPlaceholder('n8n API key').fill('staging-key');
    await page.getByPlaceholder('http://localhost:5678/webhook').fill('https://staging.n8n.example.com/webhook');
    await page.getByRole('button', { name: 'Create Configuration' }).click();

    await expect(page.getByText('Staging n8n')).toBeVisible();
    await expect(page.getByText('https://staging.n8n.example.com')).toBeVisible();
  });

  test('surfaces error when loading n8n configurations fails', async ({ page }) => {
    await page.route(`${apiBase}/admin/n8n`, async (route) => {
      await route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Unauthorized' })
      });
    });

    await page.goto('/n8n');

    await expect(page.getByRole('heading', { name: 'Error' })).toBeVisible();
    await expect(page.getByText('Failed to fetch n8n configurations')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Back to Home' })).toBeVisible();
  });
});
