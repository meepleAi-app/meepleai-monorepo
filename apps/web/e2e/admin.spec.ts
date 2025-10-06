import { test, expect, Page } from '@playwright/test';

const apiBase = 'http://localhost:8080';

async function mockAuthenticatedUser(page: Page) {
  await page.route(`${apiBase}/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'admin-1',
          email: 'admin@example.com',
          displayName: 'Admin Example',
          role: 'Admin'
        },
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString()
      })
    });
  });
}

test.describe('Admin dashboard', () => {
  test('renders analytics, supports filtering and exports CSV', async ({ page }) => {
    await mockAuthenticatedUser(page);

    const allRequests = [
      {
        id: 'req-1',
        userId: 'user-1',
        gameId: 'terraforming-mars',
        endpoint: 'qa',
        query: 'Strategie per Terraforming Mars?',
        responseSnippet: 'Concentra la produzione di ossigeno.',
        latencyMs: 320,
        tokenCount: 740,
        promptTokens: 400,
        completionTokens: 340,
        confidence: 0.92,
        status: 'Success',
        errorMessage: null,
        ipAddress: '127.0.0.1',
        userAgent: 'Playwright',
        createdAt: new Date('2025-01-06T10:15:00Z').toISOString(),
        model: 'gpt-4.1-mini',
        finishReason: 'stop'
      },
      {
        id: 'req-2',
        userId: 'user-2',
        gameId: 'meeple-arena',
        endpoint: 'setup',
        query: 'Meeple setup guidance',
        responseSnippet: 'Disponi tutti i meeple sul tabellone iniziale.',
        latencyMs: 480,
        tokenCount: 560,
        promptTokens: 260,
        completionTokens: 300,
        confidence: 0.76,
        status: 'Error',
        errorMessage: 'Timeout backend',
        ipAddress: '127.0.0.1',
        userAgent: 'Playwright',
        createdAt: new Date('2025-01-06T10:20:00Z').toISOString(),
        model: 'gpt-4.1-mini',
        finishReason: 'length'
      }
    ];

    const qaOnly = [allRequests[0]];

    await page.route(new RegExp(`${apiBase}/admin/requests.*`), async (route) => {
      const url = new URL(route.request().url());
      const endpoint = url.searchParams.get('endpoint');
      const body = endpoint === 'qa' ? { requests: qaOnly } : { requests: allRequests };

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(body)
      });
    });

    await page.route(`${apiBase}/admin/stats`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          totalRequests: 2,
          avgLatencyMs: 400,
          totalTokens: 1300,
          successRate: 0.5,
          endpointCounts: {
            qa: 1,
            setup: 1
          },
          feedbackCounts: {
            helpful: 7,
            'not-helpful': 3
          },
          totalFeedback: 10
        })
      });
    });

    await page.addInitScript(() => {
      (window as any).__downloadBlobs = [] as Blob[];
      const originalCreateObjectURL = URL.createObjectURL.bind(URL);
      URL.createObjectURL = (blob: Blob) => {
        (window as any).__downloadBlobs.push(blob);
        return originalCreateObjectURL(blob);
      };
    });

    await page.goto('/admin');

    await expect(page.getByRole('heading', { name: 'Admin Dashboard' })).toBeVisible();
    const totalRequestsCard = page.locator('div').filter({ hasText: 'Total Requests' }).first();
    await expect(totalRequestsCard).toContainText('Total Requests');
    await expect(totalRequestsCard).toContainText('2');
    await expect(page.getByText('Success Rate')).toBeVisible();
    await expect(page.getByText('50.0%')).toBeVisible();

    await expect(page.getByText('Strategie per Terraforming Mars?')).toBeVisible();
    await expect(page.getByText('Meeple setup guidance')).toBeVisible();

    const filterInput = page.getByPlaceholder('Filter by query, endpoint, user ID, or game ID...');
    await filterInput.fill('Terraforming');

    await expect(page.locator('text=Meeple setup guidance')).toHaveCount(0);
    await expect(page.getByText('Strategie per Terraforming Mars?')).toBeVisible();

    await filterInput.fill('');

    const endpointSelect = page.getByRole('combobox');
    await endpointSelect.selectOption('qa');

    await page.waitForResponse((response) => {
      return (
        response.url().startsWith(`${apiBase}/admin/requests`) &&
        response.url().includes('endpoint=qa') &&
        response.request().method() === 'GET'
      );
    });

    await expect(page.locator('text=Meeple setup guidance')).toHaveCount(0);
    await expect(page.getByText('Strategie per Terraforming Mars?')).toBeVisible();

    await page.getByRole('button', { name: 'Export CSV' }).click();

    const csvContent = await page.evaluate(async () => {
      const blobs = (window as any).__downloadBlobs as Blob[];
      if (!blobs?.length) {
        return '';
      }
      return await blobs[0].text();
    });

    expect(csvContent).toContain('Timestamp');
    expect(csvContent).toContain('Strategie per Terraforming Mars?');
    expect(csvContent).toContain('qa');

    await expect(page.getByText('Feedback Totali')).toBeVisible();
    await expect(page.getByText('👍 Utile: 7')).toBeVisible();
    await expect(page.getByText('👎 Non utile: 3')).toBeVisible();
  });

  test('shows an error state when analytics APIs fail', async ({ page }) => {
    await mockAuthenticatedUser(page);

    await page.route(new RegExp(`${apiBase}/admin/requests.*`), async (route) => {
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Internal server error' })
      });
    });

    await page.goto('/admin');

    await expect(page.getByRole('heading', { name: 'Error' })).toBeVisible();
    await expect(page.getByText('Failed to fetch requests')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Back to Home' })).toBeVisible();
  });
});
