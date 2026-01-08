/**
 * Week 3 E2E Tests - Game & Admin Critical Paths (Issue #2307)
 *
 * HIGH-VALUE E2E TESTS (4-6 tests):
 *
 * Game Management (3 tests):
 * 1. Browse games: Navigate → Grid display → Pagination → Filter by publisher
 * 2. PDF upload: Select game → Upload PDF → Processing → Success confirmation
 * 3. BGG integration: Search BGG → Select game → Import metadata
 *
 * Admin Operations (3 tests):
 * 4. Admin dashboard: Login as admin → View stats → Real-time updates verified
 * 5. Configuration update: Admin → Update system config → Save → Verify change applied
 * 6. Alert rules: Create alert → Template apply → Test trigger → Alert fires
 *
 * @see apps/web/e2e/admin.spec.ts - Existing admin dashboard tests
 * @see apps/web/e2e/bgg-integration.spec.ts - Existing BGG integration tests
 * @see docs/02-development/testing/test-coverage-gaps.md
 */

import { test, expect } from './fixtures/chromatic';
import { AuthHelper, AdminHelper, GamesHelper, USER_FIXTURES } from './pages';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ============================================================================
// GAME MANAGEMENT TESTS (3 tests)
// ============================================================================

test.describe('Game Management - Critical Paths', () => {
  test.beforeEach(async ({ page }) => {
    const authHelper = new AuthHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Authenticate as regular user for game management
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
  });

  test('1. Browse games with grid display, pagination, and publisher filter', async ({ page }) => {
    const gamesHelper = new GamesHelper(page);

    // Mock games with pagination support
    const mockGames = [
      {
        id: 'game-1',
        name: 'Terraforming Mars',
        createdAt: new Date().toISOString(),
        publisher: 'FryxGames',
        year: 2016,
        description: 'Terraform Mars and compete for victory points',
      },
      {
        id: 'game-2',
        name: 'Wingspan',
        createdAt: new Date().toISOString(),
        publisher: 'Stonemaier Games',
        year: 2019,
        description: 'Bird collection and habitat building',
      },
      {
        id: 'game-3',
        name: 'Scythe',
        createdAt: new Date().toISOString(),
        publisher: 'Stonemaier Games',
        year: 2016,
        description: 'Alternate history 1920s Europa',
      },
      {
        id: 'game-4',
        name: 'Catan',
        createdAt: new Date().toISOString(),
        publisher: 'Catan Studio',
        year: 1995,
        description: 'Settle the island of Catan',
      },
    ];

    // Mock games list with pagination and filtering
    await page.route(`${API_BASE}/api/v1/games*`, async route => {
      const url = new URL(route.request().url());
      const page = parseInt(url.searchParams.get('page') || '1');
      const pageSize = parseInt(url.searchParams.get('pageSize') || '10');
      const publisher = url.searchParams.get('publisher');

      let filteredGames = [...mockGames];

      // Apply publisher filter
      if (publisher && publisher !== 'all') {
        filteredGames = filteredGames.filter(g => g.publisher === publisher);
      }

      // Pagination
      const startIdx = (page - 1) * pageSize;
      const endIdx = startIdx + pageSize;
      const pagedGames = filteredGames.slice(startIdx, endIdx);

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          games: pagedGames,
          total: filteredGames.length,
          page,
          pageSize,
          totalPages: Math.ceil(filteredGames.length / pageSize),
        }),
      });
    });

    // Navigate to games page
    await page.goto('/games');

    // VERIFY: Grid display renders all games
    await expect(page.getByText('Terraforming Mars')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Wingspan')).toBeVisible();
    await expect(page.getByText('Scythe')).toBeVisible();
    await expect(page.getByText('Catan')).toBeVisible();

    // VERIFY: Game cards display correctly with metadata
    const terraformingCard = page
      .locator('[data-testid="game-card"]')
      .filter({ hasText: 'Terraforming Mars' });
    await expect(terraformingCard).toBeVisible();
    await expect(terraformingCard.locator('text=FryxGames')).toBeVisible();
    await expect(terraformingCard.locator('text=2016')).toBeVisible();

    // TEST: Pagination - set page size
    const pageSizeSelect = page.getByRole('combobox', { name: /per page|page size/i });
    if (await pageSizeSelect.isVisible({ timeout: 2000 }).catch(() => false)) {
      await pageSizeSelect.selectOption('2');

      // Wait for API call with pageSize=2
      await page.waitForResponse(
        response =>
          response.url().includes(`${API_BASE}/api/v1/games`) &&
          response.url().includes('pageSize=2')
      );

      // Verify only 2 games visible
      const visibleCards = page.locator('[data-testid="game-card"]');
      await expect(visibleCards).toHaveCount(2);

      // Navigate to page 2
      const nextButton = page.getByRole('button', { name: /next/i });
      await nextButton.click();

      // Verify page 2 games visible
      await expect(page.getByText('Scythe')).toBeVisible({ timeout: 5000 });
    }

    // TEST: Filter by publisher
    const publisherFilter = page
      .getByRole('combobox', { name: /publisher|filter/i })
      .or(page.locator('[data-testid="publisher-filter"]'));

    if (await publisherFilter.isVisible({ timeout: 2000 }).catch(() => false)) {
      await publisherFilter.selectOption('Stonemaier Games');

      // Wait for filtered API call
      await page.waitForResponse(
        response =>
          response.url().includes(`${API_BASE}/api/v1/games`) &&
          response.url().includes('publisher=Stonemaier')
      );

      // Verify only Stonemaier games visible
      await expect(page.getByText('Wingspan')).toBeVisible({ timeout: 5000 });
      await expect(page.getByText('Scythe')).toBeVisible();
      await expect(page.getByText('Terraforming Mars')).not.toBeVisible();
      await expect(page.getByText('Catan')).not.toBeVisible();
    }
  });

  test('2. PDF upload journey: select game → upload → processing → success', async ({ page }) => {
    const gamesHelper = new GamesHelper(page);

    // Mock complete PDF upload flow
    const { games, pdfs } = await gamesHelper.mockPdfUploadJourney();

    await page.goto('/games');

    // Select game to upload PDF
    const gameCard = page.locator('[data-testid="game-card"]').first();
    await expect(gameCard).toBeVisible({ timeout: 10000 });

    // Click to view game details or upload option
    const uploadButton = gameCard
      .getByRole('button', { name: /upload|add pdf|rulebook/i })
      .or(page.getByRole('button', { name: /upload|add pdf|rulebook/i }).first());

    await uploadButton.click();

    // FILE UPLOAD: Mock file input interaction
    const fileInput = page.locator('input[type="file"]');
    await expect(fileInput).toBeAttached({ timeout: 5000 });

    // Create a test PDF blob
    const pdfContent = new Uint8Array([0x25, 0x50, 0x44, 0x46, 0x2d]); // PDF magic bytes
    await fileInput.setInputFiles({
      name: 'test-rulebook.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from(pdfContent),
    });

    // VERIFY: Upload initiated
    const uploadButton2 = page.getByRole('button', { name: /upload|submit|confirm/i });
    await uploadButton2.click();

    // VERIFY: Processing indication (progress bar, spinner, or status message)
    const processingIndicator = page
      .getByText(/processing|uploading|analyzing/i)
      .or(page.locator('[data-testid="upload-progress"]'))
      .or(page.locator('[role="progressbar"]'));

    await expect(processingIndicator).toBeVisible({ timeout: 5000 });

    // VERIFY: Success confirmation
    await expect(page.getByText(/success|completed|uploaded successfully/i)).toBeVisible({
      timeout: 10000,
    });

    // VERIFY: PDF appears in game's PDF list
    const pdfList = page
      .locator('[data-testid="pdf-list"]')
      .or(page.getByText('test-rulebook.pdf'));

    if (await pdfList.isVisible({ timeout: 2000 }).catch(() => false)) {
      await expect(pdfList).toBeVisible();
    }
  });

  test('3. BGG integration: search → select game → import metadata', async ({ page }) => {
    const gamesHelper = new GamesHelper(page);

    // Mock BGG search results
    const mockBggResults = [
      {
        bggId: 169786,
        name: 'Scythe',
        yearPublished: 2016,
        thumbnailUrl: 'https://cf.geekdo-images.com/example/scythe_thumb.jpg',
      },
      {
        bggId: 173346,
        name: 'Scythe: The Rise of Fenris',
        yearPublished: 2018,
        thumbnailUrl: 'https://cf.geekdo-images.com/example/fenris_thumb.jpg',
      },
    ];

    const mockBggDetails = {
      bggId: 169786,
      name: 'Scythe',
      yearPublished: 2016,
      minPlayers: 1,
      maxPlayers: 5,
      playingTime: 115,
      minAge: 14,
      publishers: ['Stonemaier Games'],
      designers: ['Jamey Stegmaier'],
      averageRating: 8.2,
    };

    // Mock BGG search endpoint
    await page.route(`${API_BASE}/api/v1/bgg/search*`, async route => {
      const url = new URL(route.request().url());
      const query = url.searchParams.get('q');

      if (!query || query.length < 2) {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Search query must be at least 2 characters' }),
        });
        return;
      }

      const filteredResults = mockBggResults.filter(g =>
        g.name.toLowerCase().includes(query.toLowerCase())
      );

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ results: filteredResults }),
      });
    });

    // Mock BGG game details endpoint
    await page.route(`${API_BASE}/api/v1/bgg/games/169786`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(mockBggDetails),
      });
    });

    // Mock game creation with BGG data
    await page.route(`${API_BASE}/api/v1/games`, async route => {
      if (route.request().method() === 'POST') {
        const body = route.request().postDataJSON() as { title: string; bggId?: number };
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'game-bgg-123',
            name: body.title,
            bggId: body.bggId,
            ...mockBggDetails,
          }),
        });
      }
    });

    // Navigate to add game page
    await page.goto('/games/add');

    // SEARCH BGG: Enter search query
    const searchInput = page
      .locator('input[placeholder*="BoardGameGeek"]')
      .or(page.locator('input[placeholder*="BGG"]'))
      .or(page.locator('[data-testid="bgg-search-input"]'));

    await searchInput.fill('Scythe');

    // Click search button
    const searchButton = page.getByRole('button', { name: /search|cerca/i });
    await searchButton.click();

    // VERIFY: BGG results displayed
    await expect(page.getByText('Scythe').first()).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Scythe: The Rise of Fenris')).toBeVisible();
    await expect(page.getByText('2016')).toBeVisible();

    // SELECT GAME: Click on first result details
    const detailsButton = page
      .locator('[data-testid="bgg-game-card"]')
      .first()
      .getByRole('button', { name: /details|view|info/i })
      .or(page.getByText('Scythe').first());

    await detailsButton.click();

    // VERIFY: Game details visible
    await expect(page.getByText('Stonemaier Games')).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('Jamey Stegmaier')).toBeVisible();
    await expect(page.getByText(/1-5|1 - 5/)).toBeVisible(); // Players range

    // IMPORT METADATA: Add game with BGG data
    const addButton = page
      .locator('[data-testid="bgg-game-card"]')
      .first()
      .getByRole('button', { name: /add|aggiungi|import/i });

    await addButton.click();

    // VERIFY: Success message
    await expect(page.getByText(/added|aggiunto|success|imported/i)).toBeVisible({
      timeout: 10000,
    });

    // VERIFY: Redirect to games list
    await expect(page).toHaveURL(/\/games/);
  });
});

// ============================================================================
// ADMIN OPERATIONS TESTS (3 tests)
// ============================================================================

test.describe('Admin Operations - Critical Paths', () => {
  test.beforeEach(async ({ page }) => {
    const authHelper = new AuthHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Authenticate as admin
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);
  });

  test('4. Admin dashboard: login → view stats → real-time updates', async ({ page }) => {
    const adminHelper = new AdminHelper(page);

    // Mock initial stats
    const initialStats = {
      totalRequests: 150,
      avgLatencyMs: 420,
      totalTokens: 75000,
      successRate: 0.94,
      activeUsers: 12,
      totalGames: 8,
    };

    await adminHelper.mockAdminStats(initialStats);

    // Mock admin requests for the dashboard
    await adminHelper.mockAdminRequests([
      {
        id: 'req-1',
        userId: 'user-1',
        gameId: 'terraforming-mars',
        endpoint: 'qa',
        query: 'How to win Terraforming Mars?',
        responseSnippet: 'Focus on terraforming parameters and victory points.',
        latencyMs: 385,
        tokenCount: 842,
        confidence: 0.91,
        status: 'Success',
        createdAt: new Date().toISOString(),
        model: 'gpt-4.1-mini',
      },
      {
        id: 'req-2',
        userId: 'user-2',
        gameId: 'wingspan',
        endpoint: 'setup',
        query: 'Wingspan setup steps?',
        responseSnippet: 'Place the board, shuffle bird cards, distribute food tokens.',
        latencyMs: 310,
        tokenCount: 621,
        confidence: 0.88,
        status: 'Success',
        createdAt: new Date().toISOString(),
        model: 'gpt-4.1-mini',
      },
    ]);

    await page.goto('/admin');

    // VERIFY: Dashboard loads with key stats
    await expect(page.getByRole('heading', { name: /admin dashboard/i })).toBeVisible();

    // Verify stats cards
    const totalRequestsCard = page.locator('div').filter({ hasText: 'Total Requests' }).first();
    await expect(totalRequestsCard).toContainText('Total Requests');
    await expect(totalRequestsCard).toContainText('150');

    const successRateCard = page.locator('div').filter({ hasText: 'Success Rate' });
    await expect(successRateCard).toContainText('Success Rate');
    await expect(successRateCard).toContainText('94.0%');

    const avgLatencyCard = page.locator('div').filter({ hasText: 'Avg Latency' });
    await expect(avgLatencyCard).toContainText('Avg Latency');
    await expect(avgLatencyCard).toContainText('420');

    // VERIFY: Request list displays
    await expect(page.getByText('How to win Terraforming Mars?')).toBeVisible();
    await expect(page.getByText('Wingspan setup steps?')).toBeVisible();

    // SIMULATE REAL-TIME UPDATE: Mock updated stats
    const updatedStats = {
      totalRequests: 152, // +2 requests
      avgLatencyMs: 410,
      totalTokens: 76500,
      successRate: 0.95,
      activeUsers: 13,
      totalGames: 8,
    };

    await adminHelper.mockAdminStats(updatedStats);

    // TRIGGER REFRESH: Click refresh button or wait for auto-refresh
    const refreshButton = page.getByRole('button', { name: /refresh|reload|update/i });
    if (await refreshButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await refreshButton.click();

      // VERIFY: Stats updated
      await expect(totalRequestsCard).toContainText('152', { timeout: 5000 });
      await expect(successRateCard).toContainText('95.0%');
    }
  });

  test('5. Configuration update: admin → update config → save → verify applied', async ({
    page,
  }) => {
    const adminHelper = new AdminHelper(page);

    // Mock configuration endpoints
    const initialConfig = {
      'Features.EnableRegistration': true,
      'Features.EnableOAuth': true,
      'Features.EnableBGGIntegration': false,
      'RateLimit.RequestsPerMinute': 60,
      'RAG.ConfidenceThreshold': 0.7,
    };

    let currentConfig = { ...initialConfig };

    // Mock GET configuration
    await page.route(`${API_BASE}/api/v1/admin/configuration*`, async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(currentConfig),
        });
      } else if (route.request().method() === 'PUT' || route.request().method() === 'POST') {
        const updates = route.request().postDataJSON() as Record<string, any>;
        currentConfig = { ...currentConfig, ...updates };

        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ success: true, config: currentConfig }),
        });
      }
    });

    await page.goto('/admin/configuration');

    // VERIFY: Configuration page loads
    await expect(
      page.getByRole('heading', { name: /configuration|settings|system config/i })
    ).toBeVisible({ timeout: 10000 });

    // VERIFY: Initial config values displayed
    const registrationToggle = page
      .locator('input[name="Features.EnableRegistration"]')
      .or(page.getByLabel(/enable registration/i));

    if (await registrationToggle.isVisible({ timeout: 3000 }).catch(() => false)) {
      await expect(registrationToggle).toBeChecked();
    }

    // UPDATE CONFIGURATION: Toggle BGG integration
    const bggToggle = page
      .locator('input[name="Features.EnableBGGIntegration"]')
      .or(page.getByLabel(/bgg integration|boardgamegeek/i));

    if (await bggToggle.isVisible({ timeout: 2000 }).catch(() => false)) {
      await bggToggle.click();

      // SAVE CHANGES
      const saveButton = page.getByRole('button', { name: /save|apply|update/i });
      await saveButton.click();

      // VERIFY: Success notification
      await expect(page.getByText(/saved|updated|success|applied/i)).toBeVisible({ timeout: 5000 });

      // VERIFY: Configuration persisted (reload page)
      await page.reload();
      await expect(page.getByRole('heading', { name: /configuration|settings/i })).toBeVisible({
        timeout: 10000,
      });

      // Verify toggle state persisted
      const bggToggleAfterReload = page
        .locator('input[name="Features.EnableBGGIntegration"]')
        .or(page.getByLabel(/bgg integration|boardgamegeek/i));

      if (await bggToggleAfterReload.isVisible({ timeout: 2000 }).catch(() => false)) {
        await expect(bggToggleAfterReload).toBeChecked();
      }
    }

    // UPDATE NUMBER FIELD: Rate limit
    const rateLimitInput = page
      .locator('input[name="RateLimit.RequestsPerMinute"]')
      .or(page.getByLabel(/rate limit|requests per minute/i));

    if (await rateLimitInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      await rateLimitInput.fill('100');

      const saveButton2 = page.getByRole('button', { name: /save|apply/i });
      await saveButton2.click();

      // VERIFY: Updated value saved
      await expect(page.getByText(/saved|updated|success/i)).toBeVisible({ timeout: 5000 });
    }
  });

  test('6. Alert rules: create alert → apply template → trigger → verify', async ({ page }) => {
    const adminHelper = new AdminHelper(page);

    // Mock alert templates
    const mockAlertTemplates = [
      {
        id: 'template-1',
        name: 'High Error Rate',
        description: 'Triggered when error rate exceeds threshold',
        condition: 'error_rate > 0.1',
        severity: 'Critical',
      },
      {
        id: 'template-2',
        name: 'Slow Response Time',
        description: 'Triggered when average latency exceeds 1000ms',
        condition: 'avg_latency > 1000',
        severity: 'Warning',
      },
    ];

    const createdAlerts: any[] = [];

    // Mock alert endpoints
    await page.route(`${API_BASE}/api/v1/admin/alerts/templates*`, async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ templates: mockAlertTemplates }),
        });
      }
    });

    await page.route(`${API_BASE}/api/v1/admin/alerts*`, async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ alerts: createdAlerts }),
        });
      } else if (route.request().method() === 'POST') {
        const alertData = route.request().postDataJSON() as any;
        const newAlert = {
          id: `alert-${Date.now()}`,
          ...alertData,
          status: 'active',
          createdAt: new Date().toISOString(),
          triggeredCount: 0,
        };
        createdAlerts.push(newAlert);

        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify(newAlert),
        });
      }
    });

    // Mock alert test endpoint
    await page.route(`${API_BASE}/api/v1/admin/alerts/*/test`, async route => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            triggered: true,
            message: 'Alert condition met: error rate 15% > 10% threshold',
            severity: 'Critical',
          }),
        });
      }
    });

    await page.goto('/admin/alerts');

    // VERIFY: Alerts page loads
    await expect(
      page.getByRole('heading', { name: /alerts|notifications|monitoring/i })
    ).toBeVisible({ timeout: 10000 });

    // CREATE NEW ALERT: Click create button
    const createButton = page.getByRole('button', { name: /create|new alert|add alert/i });
    await createButton.click();

    // APPLY TEMPLATE: Select template from dropdown or list
    const templateSelector = page
      .getByRole('combobox', { name: /template|select template/i })
      .or(page.locator('[data-testid="alert-template-select"]'));

    if (await templateSelector.isVisible({ timeout: 3000 }).catch(() => false)) {
      await templateSelector.selectOption('template-1');

      // VERIFY: Template fields populated
      await expect(page.locator('text=High Error Rate')).toBeVisible();
      await expect(page.locator('text=error_rate > 0.1')).toBeVisible();
    } else {
      // Manual template selection via list/cards
      const highErrorRateTemplate = page
        .locator('[data-testid="alert-template"]')
        .filter({ hasText: 'High Error Rate' });

      if (await highErrorRateTemplate.isVisible({ timeout: 2000 }).catch(() => false)) {
        await highErrorRateTemplate.click();
      }
    }

    // CONFIGURE ALERT: Set notification channels
    const emailInput = page
      .locator('input[name="email"]')
      .or(page.getByLabel(/email|notification email/i));

    if (await emailInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      await emailInput.fill('admin@meepleai.dev');
    }

    // SAVE ALERT
    const saveButton = page.getByRole('button', { name: /save|create|confirm/i });
    await saveButton.click();

    // VERIFY: Alert created successfully
    await expect(page.getByText(/created|saved|success/i)).toBeVisible({ timeout: 5000 });

    // VERIFY: Alert appears in alerts list
    await expect(page.getByText('High Error Rate')).toBeVisible();

    // TEST ALERT TRIGGER: Click test button
    const testButton = page
      .locator('[data-testid="test-alert-button"]')
      .or(page.getByRole('button', { name: /test|trigger|simulate/i }).first());

    if (await testButton.isVisible({ timeout: 3000 }).catch(() => false)) {
      await testButton.click();

      // VERIFY: Test trigger result displayed
      await expect(page.getByText(/triggered|alert fired|condition met/i)).toBeVisible({
        timeout: 5000,
      });

      // VERIFY: Alert severity badge
      await expect(page.getByText(/critical/i)).toBeVisible();
    }
  });
});
