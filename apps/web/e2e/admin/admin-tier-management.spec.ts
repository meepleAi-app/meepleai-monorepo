/**
 * Admin Tier Management — View, Edit Limits, Validation
 *
 * Tests the tier management page at /admin/config/tiers.
 * Uses page.context().route() for API mocking (no real backend needed).
 */

import { test, expect, type Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function mockAdminAuth(page: Page) {
  await page.context().route(`${API_BASE}/api/v1/auth/me`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: { id: 'admin-1', email: 'admin@meepleai.dev', displayName: 'Admin', role: 'Admin' },
        expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      }),
    })
  );
}

function buildMockTiers() {
  return [
    {
      id: '11111111-1111-1111-1111-111111111111',
      name: 'free',
      displayName: 'Free Tier',
      description: 'Basic free access',
      monthlyPriceEur: 0,
      isActive: true,
      limits: [
        { key: 'MaxPrivateGames', value: 3 },
        { key: 'MaxPdfPerMonth', value: 5 },
        { key: 'MaxAgents', value: 1 },
        { key: 'MaxAgentQueriesPerDay', value: 10 },
        { key: 'MaxSessionQueries', value: 20 },
        { key: 'MaxPlayersPerSession', value: 4 },
        { key: 'MaxPhotosPerSession', value: 5 },
        { key: 'MaxCatalogProposalsPerWeek', value: 2 },
      ],
    },
    {
      id: '22222222-2222-2222-2222-222222222222',
      name: 'premium',
      displayName: 'Premium Plan',
      description: 'Full premium features',
      monthlyPriceEur: 9.99,
      isActive: true,
      limits: [
        { key: 'MaxPrivateGames', value: 15 },
        { key: 'MaxPdfPerMonth', value: 50 },
        { key: 'MaxAgents', value: 5 },
        { key: 'MaxAgentQueriesPerDay', value: 100 },
        { key: 'MaxSessionQueries', value: 200 },
        { key: 'MaxPlayersPerSession', value: 10 },
        { key: 'MaxPhotosPerSession', value: 20 },
        { key: 'MaxCatalogProposalsPerWeek', value: 10 },
      ],
    },
    {
      id: '33333333-3333-3333-3333-333333333333',
      name: 'admin',
      displayName: 'Admin Tier',
      description: 'Unlimited admin access',
      monthlyPriceEur: 0,
      isActive: true,
      limits: [
        { key: 'MaxPrivateGames', value: 2147483647 },
        { key: 'MaxPdfPerMonth', value: 2147483647 },
        { key: 'MaxAgents', value: 2147483647 },
        { key: 'MaxAgentQueriesPerDay', value: 2147483647 },
        { key: 'MaxSessionQueries', value: 2147483647 },
        { key: 'MaxPlayersPerSession', value: 2147483647 },
        { key: 'MaxPhotosPerSession', value: 2147483647 },
        { key: 'MaxCatalogProposalsPerWeek', value: 2147483647 },
      ],
    },
  ];
}

async function mockTiersEndpoint(page: Page) {
  await page.context().route(`${API_BASE}/api/v1/admin/tiers**`, route => {
    if (route.request().method() === 'GET') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(buildMockTiers()),
      });
    }
    // PUT handled per-test via more specific routes
    return route.continue();
  });
}

async function mockCatchAllAdmin(page: Page) {
  await page.context().route(`${API_BASE}/api/v1/admin/**`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ data: [] }),
    })
  );
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

test.describe('Tier Management — Admin Config', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);
    await mockTiersEndpoint(page);
    await mockCatchAllAdmin(page);
  });

  test('TL-01: View all tiers with correct limits displayed', async ({ page }) => {
    await page.goto('/admin/config/tiers', { waitUntil: 'domcontentloaded' });

    // Wait for tiers table to render
    const tiersTable = page.getByTestId('tiers-table');
    await expect(tiersTable).toBeVisible({ timeout: 8000 });

    // Assert all three tier rows are visible
    const freeRow = page.getByTestId('tier-row-free');
    const premiumRow = page.getByTestId('tier-row-premium');
    const adminRow = page.getByTestId('tier-row-admin');

    await expect(freeRow).toBeVisible();
    await expect(premiumRow).toBeVisible();
    await expect(adminRow).toBeVisible();

    // Free tier: name, displayName, price "—" (0), MaxPrivateGames=3, active badge
    await expect(freeRow.getByText('free')).toBeVisible();
    await expect(freeRow.getByText('Free Tier')).toBeVisible();
    await expect(freeRow.getByText('—')).toBeVisible();
    await expect(freeRow.getByText('3')).toBeVisible();
    await expect(freeRow.getByText('Attivo')).toBeVisible();

    // Premium tier: price €9.99, displayName, MaxPrivateGames=15
    await expect(premiumRow.getByText('€9.99')).toBeVisible();
    await expect(premiumRow.getByText('Premium Plan')).toBeVisible();
    await expect(premiumRow.getByText('15')).toBeVisible();

    // Admin tier: unlimited values displayed as ∞
    await expect(adminRow.getByText('∞')).toBeVisible();

    // Assert create button and all edit buttons are visible
    await expect(page.getByTestId('btn-create-tier')).toBeVisible();
    await expect(page.getByTestId('btn-edit-free')).toBeVisible();
    await expect(page.getByTestId('btn-edit-premium')).toBeVisible();
    await expect(page.getByTestId('btn-edit-admin')).toBeVisible();
  });

  test('TL-02: Edit MaxPrivateGames for Free tier', async ({ page }) => {
    // Intercept PUT for free tier and capture request body
    let capturedBody: Record<string, unknown> | null = null;

    await page.context().route(`${API_BASE}/api/v1/admin/tiers/free`, async route => {
      if (route.request().method() === 'PUT') {
        capturedBody = await route.request().postDataJSON();
        const updatedTier = {
          ...buildMockTiers()[0],
          limits: buildMockTiers()[0].limits.map(l =>
            l.key === 'MaxPrivateGames' ? { ...l, value: 5 } : l
          ),
        };
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(updatedTier),
        });
      }
      return route.continue();
    });

    await page.goto('/admin/config/tiers', { waitUntil: 'domcontentloaded' });

    // Wait for table and click edit button for free tier
    await expect(page.getByTestId('tiers-table')).toBeVisible({ timeout: 8000 });
    await page.getByTestId('btn-edit-free').click();

    // Verify dialog title
    await expect(page.getByText('Modifica tier: free')).toBeVisible({ timeout: 5000 });

    // Verify MaxPrivateGames field has current value "3"
    const limitField = page.getByTestId('field-limit-MaxPrivateGames');
    await expect(limitField).toBeVisible();
    await expect(limitField).toHaveValue('3');

    // Clear and fill with new value
    await limitField.fill('5');

    // Click save
    await page.getByTestId('btn-save').click();

    // Wait for the PUT request to complete and verify body
    await expect.poll(() => capturedBody, { timeout: 5000 }).toBeTruthy();

    const limits = (capturedBody as Record<string, unknown>).limits as Array<{
      key: string;
      value: number;
    }>;
    const maxPrivateGames = limits.find(l => l.key === 'MaxPrivateGames');
    expect(maxPrivateGames).toBeDefined();
    expect(maxPrivateGames!.value).toBe(5);

    // Verify success toast
    await expect(page.getByText('Tier aggiornato')).toBeVisible({ timeout: 5000 });
  });

  test('TL-04: Negative limit value is rejected', async ({ page }) => {
    // Mock PUT to return 400 for negative values
    await page.context().route(`${API_BASE}/api/v1/admin/tiers/free`, async route => {
      if (route.request().method() === 'PUT') {
        return route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({
            title: 'Validation Error',
            errors: { 'limits[0].value': ['Limit value must be non-negative'] },
          }),
        });
      }
      return route.continue();
    });

    await page.goto('/admin/config/tiers', { waitUntil: 'domcontentloaded' });

    // Wait for table and click edit button for free tier
    await expect(page.getByTestId('tiers-table')).toBeVisible({ timeout: 8000 });
    await page.getByTestId('btn-edit-free').click();

    // Verify dialog is open
    await expect(page.getByText('Modifica tier: free')).toBeVisible({ timeout: 5000 });

    // Fill MaxPrivateGames with negative value
    const limitField = page.getByTestId('field-limit-MaxPrivateGames');
    await limitField.fill('-1');

    // Click save
    await page.getByTestId('btn-save').click();

    // Assert error toast appears
    await expect(page.getByText(/Errore/i)).toBeVisible({ timeout: 5000 });

    // Assert dialog is still open (title still visible)
    await expect(page.getByText('Modifica tier: free')).toBeVisible();
  });
});
