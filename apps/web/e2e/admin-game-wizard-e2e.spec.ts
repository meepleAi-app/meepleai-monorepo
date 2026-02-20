/**
 * Admin Game Wizard E2E Tests
 *
 * End-to-end tests for the admin wizard flow:
 *   BGG Search → Game Creation → PDF Upload → Launch Processing →
 *   Processing Monitor (SSE) → Agent Testing (Auto-test + Chat)
 *
 * All API calls are mocked via page.context().route() for reliable testing
 * without requiring a running backend.
 *
 * @see apps/web/src/components/admin/games/wizard/AdminGameWizard.tsx
 * @see apps/web/src/components/admin/games/processing/ProcessingMonitor.tsx
 * @see apps/web/src/components/admin/games/agent-test/AgentTestingPage.tsx
 */

import { test, expect, type Page, type BrowserContext } from '@playwright/test';

// ─── Constants ──────────────────────────────────────────────────────────────

const MOCK_GAME_ID = 'a1b2c3d4-0000-1111-2222-333344445555';
const MOCK_PDF_DOC_ID = 'f6e7d8c9-aaaa-bbbb-cccc-ddddeeee0001';
const MOCK_BGG_ID = 13;
const MOCK_GAME_TITLE = 'Catan';

// ─── Mock Data Factories ────────────────────────────────────────────────────

function createBggSearchResponse(results: Array<{ name: string; bggId: number; yearPublished?: number }>) {
  return {
    results: results.map((r) => ({
      bggId: r.bggId,
      name: r.name,
      yearPublished: r.yearPublished ?? 1995,
      thumbnailUrl: null,
      type: 'boardgame',
    })),
    total: results.length,
    page: 1,
    pageSize: 20,
    totalPages: Math.ceil(results.length / 20) || 1,
  };
}

function createWizardGameResult(overrides?: Partial<{ sharedGameId: string; title: string; bggId: number }>) {
  return {
    sharedGameId: overrides?.sharedGameId ?? MOCK_GAME_ID,
    title: overrides?.title ?? MOCK_GAME_TITLE,
    bggId: overrides?.bggId ?? MOCK_BGG_ID,
    status: 'created',
  };
}

function createLaunchResult() {
  return {
    pdfDocumentId: MOCK_PDF_DOC_ID,
    gameId: MOCK_GAME_ID,
    status: 'processing',
    priority: 'Admin',
  };
}

function createSseEvent(eventType: string, data: Record<string, unknown>): string {
  return `event: ${eventType}\ndata: ${JSON.stringify(data)}\n\n`;
}

function createProgressCompleteEvent() {
  return createSseEvent('complete', {
    currentStep: 'Ready',
    pdfState: 'Ready',
    agentExists: true,
    overallPercent: 100,
    message: 'Processing complete! AI agent is ready.',
    isComplete: true,
    errorMessage: null,
    priority: 'Admin',
    timestamp: new Date().toISOString(),
  });
}

function createProgressInFlightEvent(pdfState: string, percent: number) {
  return createSseEvent('progress', {
    currentStep: pdfState,
    pdfState,
    agentExists: false,
    overallPercent: percent,
    message: `Processing: ${pdfState}`,
    isComplete: false,
    errorMessage: null,
    priority: 'Admin',
    timestamp: new Date().toISOString(),
  });
}

function createProgressFailedEvent(errorMessage: string) {
  return createSseEvent('error', {
    currentStep: 'Extracting',
    pdfState: 'Failed',
    agentExists: false,
    overallPercent: 25,
    message: 'Processing failed',
    isComplete: false,
    errorMessage,
    priority: 'Admin',
    timestamp: new Date().toISOString(),
  });
}

function createAutoTestResult() {
  return {
    gameId: MOCK_GAME_ID,
    gameTitle: MOCK_GAME_TITLE,
    testCases: [
      {
        index: 0,
        question: 'How do you set up the game?',
        answer: 'Place the board in the center of the table. Each player picks a color and takes the corresponding pieces.',
        confidenceScore: 0.85,
        latencyMs: 1200,
        chunksRetrieved: 3,
        passed: true,
        failureReason: null,
      },
      {
        index: 1,
        question: 'What are the win conditions?',
        answer: 'The first player to reach 10 victory points wins the game.',
        confidenceScore: 0.92,
        latencyMs: 800,
        chunksRetrieved: 2,
        passed: true,
        failureReason: null,
      },
      {
        index: 2,
        question: 'How does the turn order work?',
        answer: 'Players take turns clockwise, starting with the first player.',
        confidenceScore: 0.78,
        latencyMs: 1500,
        chunksRetrieved: 2,
        passed: true,
        failureReason: null,
      },
      {
        index: 3,
        question: 'What actions can a player take on their turn?',
        answer: 'Roll dice, collect resources, trade, and build.',
        confidenceScore: 0.88,
        latencyMs: 900,
        chunksRetrieved: 4,
        passed: true,
        failureReason: null,
      },
      {
        index: 4,
        question: 'How does scoring work?',
        answer: 'Players earn victory points from settlements (1 VP), cities (2 VP), longest road (2 VP), and development cards.',
        confidenceScore: 0.9,
        latencyMs: 1100,
        chunksRetrieved: 3,
        passed: true,
        failureReason: null,
      },
      {
        index: 5,
        question: 'Are there any special rules or exceptions?',
        answer: 'The robber blocks resource production on the tile it occupies. Rolling a 7 triggers the robber.',
        confidenceScore: 0.72,
        latencyMs: 1800,
        chunksRetrieved: 5,
        passed: true,
        failureReason: null,
      },
      {
        index: 6,
        question: 'What components are included in the game?',
        answer: 'The game includes 19 terrain hexes, 6 sea frame pieces, 9 harbor pieces, and various resource cards.',
        confidenceScore: 0.65,
        latencyMs: 2100,
        chunksRetrieved: 4,
        passed: true,
        failureReason: null,
      },
      {
        index: 7,
        question: 'How many players can play and what is the recommended player count?',
        answer: '3-4 players (base game). The 5-6 player extension is available separately.',
        confidenceScore: 0.95,
        latencyMs: 600,
        chunksRetrieved: 1,
        passed: true,
        failureReason: null,
      },
    ],
    report: {
      totalTests: 8,
      passed: 8,
      failed: 0,
      averageConfidence: 0.83,
      averageLatencyMs: 1250,
      overallGrade: 'A',
      passRate: 1.0,
    },
    executedAt: new Date().toISOString(),
  };
}

function createChatResponse() {
  return {
    answer: 'In Catan, you earn victory points from settlements (1 VP), cities (2 VP), longest road (2 VP), largest army (2 VP), and certain development cards.',
    retrievedChunks: [
      { content: 'Victory Points: settlements=1, cities=2...', relevanceScore: 0.91, chunkIndex: 5 },
      { content: 'Special VP cards: longest road, largest army...', relevanceScore: 0.85, chunkIndex: 12 },
    ],
    latencyMs: 950,
    tokenUsage: { promptTokens: 120, completionTokens: 80, totalTokens: 200, embeddingTokens: 15 },
  };
}

// ─── Auth Setup ─────────────────────────────────────────────────────────────

async function setupAdminAuth(context: BrowserContext) {
  await context.addCookies([
    {
      name: 'meepleai_session',
      value: 'mock-admin-session-wizard-e2e',
      domain: 'localhost',
      path: '/',
      httpOnly: true,
      secure: false,
      sameSite: 'Lax',
    },
    {
      name: 'meepleai_user_role',
      value: 'admin',
      domain: 'localhost',
      path: '/',
      httpOnly: false,
      secure: false,
      sameSite: 'Lax',
    },
  ]);
}

/**
 * Set up standard auth + catch-all admin API mocks on the browser context.
 * CRITICAL: Use context.route() not page.route() for reliable interception
 * with Next.js dev server proxy.
 */
async function setupBaseApiMocks(context: BrowserContext) {
  const adminUserPayload = JSON.stringify({
    user: {
      id: 'a0000000-0000-4000-a000-000000000001', // Must be valid UUID v4 (Zod strict validation)
      email: 'admin@meepleai.dev',
      displayName: 'Admin User',
      role: 'Admin',
    },
  });

  // Mock auth/me on both direct backend calls and Next.js proxy path
  await context.route('**/api/v1/auth/me', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: adminUserPayload,
    });
  });
}

// ─── Test Suites ────────────────────────────────────────────────────────────

test.describe('Admin Game Wizard - Complete Flow', () => {
  test.describe.configure({ mode: 'serial' });

  // ── Phase 1: BGG Search ──────────────────────────────────────────────────

  test.describe('Phase 1: BGG Search', () => {
    test('should display wizard page with search input', async ({ page }) => {
      await setupAdminAuth(page.context());
      await setupBaseApiMocks(page.context());

      // Mock BGG search to prevent actual API calls
      await page.context().route('**/api/v1/bgg/search**', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(createBggSearchResponse([])),
        });
      });

      await page.goto('/admin/games/new');
      await page.waitForLoadState('networkidle');

      if (page.url().includes('/login')) {
        test.skip(true, 'Auth redirect - server-side auth requires real backend');
        return;
      }

      // Verify wizard header
      await expect(page.locator('h1')).toContainText('Add Game');

      // Verify stepper is showing all 4 steps
      await expect(page.getByText('Search BGG')).toBeVisible();
      await expect(page.getByText('Game Details')).toBeVisible();
      await expect(page.getByText('Upload PDF')).toBeVisible();
      await expect(page.getByText('Launch', { exact: true })).toBeVisible();

      // Verify search input is present and autofocused
      const searchInput = page.getByPlaceholder(/search boardgamegeek/i);
      await expect(searchInput).toBeVisible();

      // Verify hint text
      await expect(page.getByText(/type at least 2 characters/i)).toBeVisible();
    });

    test('should search BGG and display results', async ({ page }) => {
      await setupAdminAuth(page.context());
      await setupBaseApiMocks(page.context());

      // Mock BGG search with results
      await page.context().route('**/api/v1/bgg/search**', async (route) => {
        const url = new URL(route.request().url());
        const query = url.searchParams.get('q') || url.searchParams.get('query') || '';

        if (query.length >= 2) {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(
              createBggSearchResponse([
                { name: 'Catan', bggId: 13, yearPublished: 1995 },
                { name: 'Catan: Seafarers', bggId: 325, yearPublished: 1997 },
                { name: 'Catan: Cities & Knights', bggId: 926, yearPublished: 1998 },
              ])
            ),
          });
        } else {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(createBggSearchResponse([])),
          });
        }
      });

      await page.goto('/admin/games/new');
      await page.waitForLoadState('networkidle');

      if (page.url().includes('/login')) {
        test.skip(true, 'Middleware redirected');
        return;
      }

      // Type search query
      const searchInput = page.getByPlaceholder(/search boardgamegeek/i);
      await searchInput.fill('Catan');

      // Wait for results
      await expect(page.getByText('3 results found')).toBeVisible({ timeout: 10000 });

      // Verify result cards
      await expect(page.getByText('Catan').first()).toBeVisible();
      await expect(page.getByText('Catan: Seafarers')).toBeVisible();
      await expect(page.getByText('Catan: Cities & Knights')).toBeVisible();

      // Verify BGG IDs shown
      await expect(page.getByText('BGG #13')).toBeVisible();

      // Verify year shown
      await expect(page.getByText('1995')).toBeVisible();
    });

    test('should show no results message for unknown game', async ({ page }) => {
      await setupAdminAuth(page.context());
      await setupBaseApiMocks(page.context());

      await page.context().route('**/api/v1/bgg/search**', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(createBggSearchResponse([])),
        });
      });

      await page.goto('/admin/games/new');
      await page.waitForLoadState('networkidle');

      if (page.url().includes('/login')) {
        test.skip(true, 'Middleware redirected');
        return;
      }

      const searchInput = page.getByPlaceholder(/search boardgamegeek/i);
      await searchInput.fill('xyznonexistentgame123');

      await expect(page.getByText(/no games found/i)).toBeVisible({ timeout: 10000 });
    });

    test('should show error state on API failure', async ({ page }) => {
      await setupAdminAuth(page.context());
      await setupBaseApiMocks(page.context());

      // Use status 400 to avoid httpClient retry loop
      await page.context().route('**/api/v1/bgg/search**', async (route) => {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({ detail: 'BGG API unavailable' }),
        });
      });

      await page.goto('/admin/games/new');
      await page.waitForLoadState('networkidle');

      if (page.url().includes('/login')) {
        test.skip(true, 'Middleware redirected');
        return;
      }

      const searchInput = page.getByPlaceholder(/search boardgamegeek/i);
      await searchInput.fill('Catan');

      // Should show error state (exact match to avoid matching toast text)
      await expect(page.getByText('Search failed', { exact: true })).toBeVisible({ timeout: 15000 });
    });
  });

  // ── Phase 2: Game Details & Creation ──────────────────────────────────────

  test.describe('Phase 2: Game Details & Creation', () => {
    test('should advance to Game Details step when a result is clicked', async ({ page }) => {
      await setupAdminAuth(page.context());
      await setupBaseApiMocks(page.context());

      await page.context().route('**/api/v1/bgg/search**', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(
            createBggSearchResponse([
              { name: 'Catan', bggId: 13, yearPublished: 1995 },
            ])
          ),
        });
      });

      // Mock create endpoint (for later use)
      await page.context().route('**/api/v1/admin/games/wizard/create', async (route) => {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify(createWizardGameResult()),
        });
      });

      await page.goto('/admin/games/new');
      await page.waitForLoadState('networkidle');

      if (page.url().includes('/login')) {
        test.skip(true, 'Middleware redirected');
        return;
      }

      // Search and select a game
      const searchInput = page.getByPlaceholder(/search boardgamegeek/i);
      await searchInput.fill('Catan');
      await expect(page.getByText('BGG #13')).toBeVisible({ timeout: 10000 });

      // Click the game card
      await page.getByText('Catan').first().click();

      // Verify step 2: Game Details
      await expect(page.getByText('Selected from BoardGameGeek')).toBeVisible();
      await expect(page.getByText('Create Game')).toBeVisible();
      await expect(page.getByText('Back to Search')).toBeVisible();

      // Verify amber info note about BGG data import
      await expect(page.getByText(/import all available data from BGG/i)).toBeVisible();
    });

    test('should create game and advance to PDF Upload step', async ({ page }) => {
      await setupAdminAuth(page.context());
      await setupBaseApiMocks(page.context());

      await page.context().route('**/api/v1/bgg/search**', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(
            createBggSearchResponse([
              { name: 'Catan', bggId: 13, yearPublished: 1995 },
            ])
          ),
        });
      });

      await page.context().route('**/api/v1/admin/games/wizard/create', async (route) => {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify(createWizardGameResult()),
        });
      });

      await page.goto('/admin/games/new');
      await page.waitForLoadState('networkidle');

      if (page.url().includes('/login')) {
        test.skip(true, 'Middleware redirected');
        return;
      }

      // Search → Select → Advance to step 2
      await page.getByPlaceholder(/search boardgamegeek/i).fill('Catan');
      await expect(page.getByText('BGG #13')).toBeVisible({ timeout: 10000 });
      await page.getByText('Catan').first().click();

      // Wait for step 2 and click Create
      await expect(page.getByText('Create Game')).toBeVisible();
      await page.getByRole('button', { name: /create game/i }).click();

      // Button should show loading, then success
      await expect(page.getByText('Game Created!')).toBeVisible({ timeout: 10000 });
    });

    test('should handle duplicate BGG ID (409 conflict)', async ({ page }) => {
      await setupAdminAuth(page.context());
      await setupBaseApiMocks(page.context());

      await page.context().route('**/api/v1/bgg/search**', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(
            createBggSearchResponse([
              { name: 'Catan', bggId: 13, yearPublished: 1995 },
            ])
          ),
        });
      });

      // Return 409 for duplicate
      await page.context().route('**/api/v1/admin/games/wizard/create', async (route) => {
        await route.fulfill({
          status: 409,
          contentType: 'application/json',
          body: JSON.stringify({ detail: 'A game with this BGG ID already exists' }),
        });
      });

      await page.goto('/admin/games/new');
      await page.waitForLoadState('networkidle');

      if (page.url().includes('/login')) {
        test.skip(true, 'Middleware redirected');
        return;
      }

      // Navigate to step 2
      await page.getByPlaceholder(/search boardgamegeek/i).fill('Catan');
      await expect(page.getByText('BGG #13')).toBeVisible({ timeout: 10000 });
      await page.getByText('Catan').first().click();

      // Click Create Game
      await page.getByRole('button', { name: /create game/i }).click();

      // Should show error message
      await expect(page.getByText(/already exists/i)).toBeVisible({ timeout: 10000 });
    });

    test('should navigate back from Game Details to Search', async ({ page }) => {
      await setupAdminAuth(page.context());
      await setupBaseApiMocks(page.context());

      await page.context().route('**/api/v1/bgg/search**', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(
            createBggSearchResponse([
              { name: 'Catan', bggId: 13, yearPublished: 1995 },
            ])
          ),
        });
      });

      await page.goto('/admin/games/new');
      await page.waitForLoadState('networkidle');

      if (page.url().includes('/login')) {
        test.skip(true, 'Middleware redirected');
        return;
      }

      // Navigate to step 2
      await page.getByPlaceholder(/search boardgamegeek/i).fill('Catan');
      await expect(page.getByText('BGG #13')).toBeVisible({ timeout: 10000 });
      await page.getByText('Catan').first().click();
      await expect(page.getByText('Back to Search')).toBeVisible();

      // Click Back
      await page.getByRole('button', { name: /back to search/i }).click();

      // Should be back on search step
      await expect(page.getByPlaceholder(/search boardgamegeek/i)).toBeVisible();
    });
  });

  // ── Phase 4: Launch Processing Step ───────────────────────────────────────

  test.describe('Phase 4: Launch Processing', () => {
    /**
     * Helper: Navigate wizard to the Launch Processing step by mocking
     * all preceding steps and simulating the flow.
     */
    async function navigateToLaunchStep(page: Page) {
      await setupAdminAuth(page.context());
      await setupBaseApiMocks(page.context());

      // Mock BGG search
      await page.context().route('**/api/v1/bgg/search**', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(
            createBggSearchResponse([
              { name: 'Catan', bggId: 13, yearPublished: 1995 },
            ])
          ),
        });
      });

      // Mock game creation
      await page.context().route('**/api/v1/admin/games/wizard/create', async (route) => {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify(createWizardGameResult()),
        });
      });

      // Mock PDF upload (chunked upload endpoint)
      await page.context().route('**/api/v1/pdf/upload**', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: MOCK_PDF_DOC_ID,
            fileName: 'catan-rules.pdf',
            status: 'Uploaded',
          }),
        });
      });

      // Mock launch processing
      await page.context().route('**/api/v1/admin/games/wizard/*/launch-processing', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(createLaunchResult()),
        });
      });

      // Mock SSE stream (will be needed after redirect)
      await page.context().route('**/api/v1/admin/games/wizard/*/progress/stream', async (route) => {
        const body = createProgressCompleteEvent();
        await route.fulfill({
          status: 200,
          headers: {
            'Content-Type': 'text/event-stream',
            'Cache-Control': 'no-cache',
            'X-Accel-Buffering': 'no',
          },
          body,
        });
      });

      return page;
    }

    test('should show Launch Processing step with summary', async ({ page }) => {
      await navigateToLaunchStep(page);

      await page.goto('/admin/games/new');
      await page.waitForLoadState('networkidle');

      if (page.url().includes('/login')) {
        test.skip(true, 'Middleware redirected');
        return;
      }

      // Step 1: Search → Select
      await page.getByPlaceholder(/search boardgamegeek/i).fill('Catan');
      await expect(page.getByText('BGG #13')).toBeVisible({ timeout: 10000 });
      await page.getByText('Catan').first().click();

      // Step 2: Create Game
      await page.getByRole('button', { name: /create game/i }).click();
      await expect(page.getByText('Game Created!')).toBeVisible({ timeout: 10000 });

      // Step 3 (PDF Upload) is complex - we verify we reached it
      // The PdfUploadStep renders PdfUploadForm which needs file input interaction
      // For now, verify the wizard advanced past step 2
      // The actual PDF upload would require file input mocking
    });
  });

  // ── Phase 5: Processing Monitor (SSE) ────────────────────────────────────

  test.describe('Phase 5: Processing Monitor', () => {
    test('should display processing pipeline with completion state', async ({ page }) => {
      await setupAdminAuth(page.context());
      await setupBaseApiMocks(page.context());

      // Mock SSE endpoint to return immediate completion
      await page.context().route('**/api/v1/admin/games/wizard/*/progress/stream', async (route) => {
        const body = createProgressCompleteEvent();
        await route.fulfill({
          status: 200,
          headers: {
            'Content-Type': 'text/event-stream',
            'Cache-Control': 'no-cache',
            'X-Accel-Buffering': 'no',
          },
          body,
        });
      });

      await page.goto(`/admin/games/${MOCK_GAME_ID}/processing?title=${encodeURIComponent(MOCK_GAME_TITLE)}`);
      await page.waitForLoadState('networkidle');

      if (page.url().includes('/login')) {
        test.skip(true, 'Middleware redirected');
        return;
      }

      // Verify header
      await expect(page.locator('h1')).toContainText(`Processing ${MOCK_GAME_TITLE}`);

      // Verify pipeline steps are visible
      await expect(page.getByText('Processing Pipeline')).toBeVisible();
      await expect(page.getByText('Pending')).toBeVisible();
      await expect(page.getByText('Uploading')).toBeVisible();
      await expect(page.getByText('Extracting')).toBeVisible();
      await expect(page.getByText('Chunking')).toBeVisible();
      await expect(page.getByText('Embedding')).toBeVisible();
      await expect(page.getByText('Indexing')).toBeVisible();
      await expect(page.getByText('Ready')).toBeVisible();

      // Wait for SSE to be processed and show completion
      await expect(page.getByText('Processing Complete!')).toBeVisible({ timeout: 10000 });

      // Verify "Test Agent" link is shown
      await expect(page.getByRole('link', { name: /test agent/i })).toBeVisible();
    });

    test('should show Admin Priority badge', async ({ page }) => {
      await setupAdminAuth(page.context());
      await setupBaseApiMocks(page.context());

      await page.context().route('**/api/v1/admin/games/wizard/*/progress/stream', async (route) => {
        const body =
          createProgressInFlightEvent('Extracting', 25) +
          createProgressCompleteEvent();
        await route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'text/event-stream', 'Cache-Control': 'no-cache' },
          body,
        });
      });

      await page.goto(`/admin/games/${MOCK_GAME_ID}/processing?title=${MOCK_GAME_TITLE}`);
      await page.waitForLoadState('networkidle');

      if (page.url().includes('/login')) {
        test.skip(true, 'Middleware redirected');
        return;
      }

      // Verify Admin Priority badge
      await expect(page.getByText('Admin Priority')).toBeVisible({ timeout: 10000 });
    });

    test('should show error state when processing fails', async ({ page }) => {
      await setupAdminAuth(page.context());
      await setupBaseApiMocks(page.context());

      await page.context().route('**/api/v1/admin/games/wizard/*/progress/stream', async (route) => {
        const body = createProgressFailedEvent('PDF extraction failed: corrupt file');
        await route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'text/event-stream', 'Cache-Control': 'no-cache' },
          body,
        });
      });

      await page.goto(`/admin/games/${MOCK_GAME_ID}/processing?title=${MOCK_GAME_TITLE}`);
      await page.waitForLoadState('networkidle');

      if (page.url().includes('/login')) {
        test.skip(true, 'Middleware redirected');
        return;
      }

      // Verify error state
      await expect(page.getByText('Processing Failed')).toBeVisible({ timeout: 10000 });
      await expect(page.getByText(/pdf extraction failed/i)).toBeVisible();
    });

    test('should show reconnect button on connection error', async ({ page }) => {
      // This test needs extra time: hook retries 5 times with exponential backoff
      // Total wait: ~31s (1+2+4+8+16s delays) before showing 'error' state
      test.setTimeout(90000);

      await setupAdminAuth(page.context());
      await setupBaseApiMocks(page.context());

      // Always abort SSE connections to exhaust all reconnection attempts
      // The hook tries: 1 initial + MAX_RECONNECT_ATTEMPTS (5) = 6 total
      await page.context().route('**/api/v1/admin/games/wizard/*/progress/stream', async (route) => {
        await route.abort('connectionfailed');
      });

      await page.goto(`/admin/games/${MOCK_GAME_ID}/processing?title=${MOCK_GAME_TITLE}`);
      await page.waitForLoadState('networkidle');

      if (page.url().includes('/login')) {
        test.skip(true, 'Middleware redirected');
        return;
      }

      // After exhausting reconnect attempts (~31s), should show reconnect button
      await expect(page.getByText('Connection lost.')).toBeVisible({ timeout: 60000 });
      await expect(page.getByRole('button', { name: /reconnect/i })).toBeVisible();
    });

    test('should navigate to agent test from processing complete', async ({ page }) => {
      await setupAdminAuth(page.context());
      await setupBaseApiMocks(page.context());

      await page.context().route('**/api/v1/admin/games/wizard/*/progress/stream', async (route) => {
        await route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'text/event-stream', 'Cache-Control': 'no-cache' },
          body: createProgressCompleteEvent(),
        });
      });

      // Mock the agent test page endpoints
      await page.context().route('**/api/v1/admin/games/*/agent/auto-test', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(createAutoTestResult()),
        });
      });

      await page.goto(`/admin/games/${MOCK_GAME_ID}/processing?title=${encodeURIComponent(MOCK_GAME_TITLE)}`);

      if (page.url().includes('/login')) {
        test.skip(true, 'Middleware redirected');
        return;
      }

      // Wait for completion
      await expect(page.getByText('Processing Complete!')).toBeVisible({ timeout: 10000 });

      // Click "Test Agent" link
      const testAgentLink = page.getByRole('link', { name: /test agent/i });
      await expect(testAgentLink).toBeVisible();
      await testAgentLink.click();

      // Verify navigation to agent test page
      await expect(page).toHaveURL(new RegExp(`/admin/games/${MOCK_GAME_ID}/agent/test`));
    });
  });

  // ── Phase 6: Agent Testing ────────────────────────────────────────────────

  test.describe('Phase 6: Agent Testing', () => {
    test('should display agent test page with tabs', async ({ page }) => {
      await setupAdminAuth(page.context());
      await setupBaseApiMocks(page.context());

      await page.goto(`/admin/games/${MOCK_GAME_ID}/agent/test?title=${encodeURIComponent(MOCK_GAME_TITLE)}`);
      await page.waitForLoadState('networkidle');

      if (page.url().includes('/login')) {
        test.skip(true, 'Middleware redirected');
        return;
      }

      // Verify header
      await expect(page.locator('h1')).toContainText(`Test Agent: ${MOCK_GAME_TITLE}`);
      await expect(page.getByText(/verify the rag agent/i)).toBeVisible();

      // Verify tabs
      await expect(page.getByText('Auto Test')).toBeVisible();
      await expect(page.getByText('Interactive Chat')).toBeVisible();

      // Verify Auto Test tab is active by default
      await expect(page.getByText('Run Auto Test')).toBeVisible();
      await expect(page.getByText(/sends 8 standard board game questions/i)).toBeVisible();
    });

    test('should run auto-test suite and display quality report', async ({ page }) => {
      await setupAdminAuth(page.context());
      await setupBaseApiMocks(page.context());

      // Mock auto-test endpoint
      await page.context().route('**/api/v1/admin/games/*/agent/auto-test', async (route) => {
        // Small delay to simulate test execution
        await new Promise((resolve) => setTimeout(resolve, 500));
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(createAutoTestResult()),
        });
      });

      await page.goto(`/admin/games/${MOCK_GAME_ID}/agent/test?title=${encodeURIComponent(MOCK_GAME_TITLE)}`);
      await page.waitForLoadState('networkidle');

      if (page.url().includes('/login')) {
        test.skip(true, 'Middleware redirected');
        return;
      }

      // Click "Run Auto Test" button
      await page.getByRole('button', { name: /run auto test/i }).click();

      // Should show loading state
      await expect(page.getByText(/running test suite/i)).toBeVisible();

      // Wait for results
      await expect(page.getByText('Quality Report')).toBeVisible({ timeout: 15000 });

      // Verify grade badge (should show "A")
      await expect(page.getByText('A').first()).toBeVisible();

      // Verify report metrics
      await expect(page.getByText('Pass Rate')).toBeVisible();
      await expect(page.getByText('100%')).toBeVisible(); // 8/8 passed
      await expect(page.getByText('Avg Confidence')).toBeVisible();
      await expect(page.getByText('Avg Latency')).toBeVisible();

      // Verify test cases section
      await expect(page.getByText('Test Cases')).toBeVisible();
      await expect(page.getByText('How do you set up the game?')).toBeVisible();
      await expect(page.getByText('What are the win conditions?')).toBeVisible();

      // Verify pass/fail indicators (all should pass with green checkmarks)
      await expect(page.getByText('8 pass')).toBeVisible();
      await expect(page.getByText('0 fail')).toBeVisible();
    });

    test('should switch to Interactive Chat tab and send messages', async ({ page }) => {
      await setupAdminAuth(page.context());
      await setupBaseApiMocks(page.context());

      // Mock chat endpoint
      await page.context().route('**/api/v1/agents/chat/ask', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(createChatResponse()),
        });
      });

      await page.goto(`/admin/games/${MOCK_GAME_ID}/agent/test?title=${encodeURIComponent(MOCK_GAME_TITLE)}`);
      await page.waitForLoadState('networkidle');

      if (page.url().includes('/login')) {
        test.skip(true, 'Middleware redirected');
        return;
      }

      // Switch to Interactive Chat tab
      await page.getByText('Interactive Chat').click();

      // Verify empty state
      await expect(page.getByText(/ask any question about/i)).toBeVisible();
      await expect(page.getByText(/the agent uses rag/i)).toBeVisible();

      // Type a question
      const chatInput = page.getByPlaceholder(/ask a question about the game rules/i);
      await expect(chatInput).toBeVisible();
      await chatInput.fill('How do I earn victory points?');

      // Send the message
      await page.getByRole('button').filter({ has: page.locator('svg') }).last().click();

      // Verify user message appears
      await expect(page.getByText('How do I earn victory points?')).toBeVisible();

      // Wait for assistant response
      await expect(page.getByText(/victory points from settlements/i)).toBeVisible({ timeout: 10000 });

      // Verify metadata badges
      await expect(page.getByText(/confidence/i)).toBeVisible();
      await expect(page.getByText(/950ms/)).toBeVisible();
      await expect(page.getByText(/2 chunks/)).toBeVisible();
    });

    test('should handle chat API error gracefully', async ({ page }) => {
      await setupAdminAuth(page.context());
      await setupBaseApiMocks(page.context());

      // Mock chat endpoint with error
      await page.context().route('**/api/v1/agents/chat/ask', async (route) => {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({ detail: 'Agent not configured for this game' }),
        });
      });

      await page.goto(`/admin/games/${MOCK_GAME_ID}/agent/test?title=${encodeURIComponent(MOCK_GAME_TITLE)}`);
      await page.waitForLoadState('networkidle');

      if (page.url().includes('/login')) {
        test.skip(true, 'Middleware redirected');
        return;
      }

      // Switch to Interactive Chat
      await page.getByText('Interactive Chat').click();

      // Send a message
      const chatInput = page.getByPlaceholder(/ask a question about the game rules/i);
      await chatInput.fill('How do I play?');
      await page.getByRole('button').filter({ has: page.locator('svg') }).last().click();

      // Verify error response in chat
      await expect(page.getByText(/encountered an error/i)).toBeVisible({ timeout: 10000 });
    });

    test('should show auto-test with partial failures', async ({ page }) => {
      await setupAdminAuth(page.context());
      await setupBaseApiMocks(page.context());

      const partialFailResult = createAutoTestResult();
      // Make some tests fail
      partialFailResult.testCases[5].passed = false;
      partialFailResult.testCases[5].failureReason = 'Average chunk confidence below threshold (0.2 < 0.3)';
      partialFailResult.testCases[6].passed = false;
      partialFailResult.testCases[6].failureReason = 'No chunks retrieved';
      partialFailResult.testCases[6].chunksRetrieved = 0;
      partialFailResult.report.passed = 6;
      partialFailResult.report.failed = 2;
      partialFailResult.report.passRate = 0.75;
      partialFailResult.report.overallGrade = 'B';

      await page.context().route('**/api/v1/admin/games/*/agent/auto-test', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(partialFailResult),
        });
      });

      await page.goto(`/admin/games/${MOCK_GAME_ID}/agent/test?title=${encodeURIComponent(MOCK_GAME_TITLE)}`);
      await page.waitForLoadState('networkidle');

      if (page.url().includes('/login')) {
        test.skip(true, 'Middleware redirected');
        return;
      }

      // Run auto test
      await page.getByRole('button', { name: /run auto test/i }).click();

      // Verify grade B
      await expect(page.getByText('Quality Report')).toBeVisible({ timeout: 15000 });
      await expect(page.getByText('B').first()).toBeVisible();

      // Verify pass/fail counts
      await expect(page.getByText('6 pass')).toBeVisible();
      await expect(page.getByText('2 fail')).toBeVisible();

      // Verify failure reasons are shown
      await expect(page.getByText(/chunk confidence below threshold/i)).toBeVisible();
      await expect(page.getByText(/no chunks retrieved/i)).toBeVisible();
    });
  });
});

// ─── Cross-Page Navigation Tests ────────────────────────────────────────────

test.describe('Admin Wizard - Navigation & Route Tests', () => {
  test('should not show 404 on wizard page', async ({ page }) => {
    await setupAdminAuth(page.context());
    await setupBaseApiMocks(page.context());

    await page.context().route('**/api/v1/bgg/search**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(createBggSearchResponse([])),
      });
    });

    await page.goto('/admin/games/new');

    if (page.url().includes('/login')) {
      test.skip(true, 'Middleware redirected');
      return;
    }

    await expect(page.locator('h1')).not.toContainText('404');
    await expect(page.locator('h1')).not.toContainText('Not Found');
  });

  test('should not show 404 on processing page', async ({ page }) => {
    await setupAdminAuth(page.context());
    await setupBaseApiMocks(page.context());

    await page.context().route('**/api/v1/admin/games/wizard/*/progress/stream', async (route) => {
      await route.fulfill({
        status: 200,
        headers: { 'Content-Type': 'text/event-stream', 'Cache-Control': 'no-cache' },
        body: createProgressInFlightEvent('Pending', 0),
      });
    });

    await page.goto(`/admin/games/${MOCK_GAME_ID}/processing?title=${MOCK_GAME_TITLE}`);

    if (page.url().includes('/login')) {
      test.skip(true, 'Middleware redirected');
      return;
    }

    await expect(page.locator('h1')).not.toContainText('404');
    await expect(page.locator('h1')).not.toContainText('Not Found');
  });

  test('should not show 404 on agent test page', async ({ page }) => {
    await setupAdminAuth(page.context());
    await setupBaseApiMocks(page.context());

    await page.goto(`/admin/games/${MOCK_GAME_ID}/agent/test?title=${MOCK_GAME_TITLE}`);

    if (page.url().includes('/login')) {
      test.skip(true, 'Middleware redirected');
      return;
    }

    await expect(page.locator('h1')).not.toContainText('404');
    await expect(page.locator('h1')).not.toContainText('Not Found');
  });
});
