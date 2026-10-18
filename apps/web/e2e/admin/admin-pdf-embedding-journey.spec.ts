/**
 * E2E Test: Admin PDF Embedding Journey — Full Pipeline
 *
 * Tests the complete admin flow:
 * 1. Create game via BGG wizard
 * 2. Upload PDF rulebook
 * 3. Monitor embedding queue in admin dashboard
 * 4. Test RAG agent with citations
 *
 * All API calls are mocked for CI reliability (~15s).
 * See: docs/specs/us-admin-pdf-embedding-journey.md
 */

import { test, expect, type Page } from '@playwright/test';

import { seedMockRoleCookies } from '../_helpers/seedAuthSession';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ========================================
// Test Data
// ========================================

const GAME_ID = 'shared-game-catan-123';
const DOCUMENT_ID = 'doc-catan-pdf-789';
const BGG_ID = 13;

const CATAN_BGG_RESULT = {
  bggId: BGG_ID,
  name: 'Catan',
  yearPublished: 1995,
  type: 'boardgame',
  thumbnailUrl: 'https://example.com/catan-thumb.jpg',
};

const CATAN_GAME = {
  id: GAME_ID,
  title: 'Catan',
  bggId: BGG_ID,
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  playingTime: 90,
  description: 'Trade, build, settle.',
  thumbnailUrl: 'https://example.com/catan-thumb.jpg',
  imageUrl: 'https://example.com/catan.jpg',
  createdAt: new Date().toISOString(),
};

// ========================================
// Mock Helpers
// ========================================

async function mockAdminAuth(page: Page) {
  const userResponse = {
    user: {
      id: 'admin-test-id',
      email: 'admin@meepleai.dev',
      displayName: 'Test Admin',
      role: 'Admin',
    },
    expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  };

  // Catch-all for unmocked API calls
  await page.route(`${API_BASE}/api/**`, async route => {
    const method = route.request().method();
    if (method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    } else {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true }),
      });
    }
  });

  await page.route(`${API_BASE}/api/v1/auth/me`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(userResponse),
    });
  });

  await page.route(`${API_BASE}/api/v1/users/me`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: userResponse.user.id,
        email: userResponse.user.email,
        displayName: userResponse.user.displayName,
        role: userResponse.user.role,
        createdAt: new Date().toISOString(),
      }),
    });
  });
}

async function mockBggSearch(page: Page) {
  await page.route(`${API_BASE}/api/v1/bgg/search**`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        results: [
          CATAN_BGG_RESULT,
          { bggId: 14, name: 'Catan: Seafarers', yearPublished: 1997, type: 'boardgameexpansion' },
        ],
        totalResults: 2,
      }),
    });
  });
}

async function mockGameCreation(page: Page) {
  await page.route(`${API_BASE}/api/v1/admin/shared-games`, async route => {
    if (route.request().method() === 'POST') {
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify(CATAN_GAME),
      });
    } else {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([CATAN_GAME]),
      });
    }
  });

  // Mock individual game fetch
  await page.route(`${API_BASE}/api/v1/admin/shared-games/${GAME_ID}`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(CATAN_GAME),
    });
  });
}

async function mockPdfUpload(page: Page) {
  await page.route(`${API_BASE}/api/v1/ingest/pdf`, async route => {
    await route.fulfill({
      status: 201,
      contentType: 'application/json',
      body: JSON.stringify({
        id: DOCUMENT_ID,
        fileName: 'catan-regolamento.pdf',
        status: 'Uploaded',
        gameId: GAME_ID,
        createdAt: new Date().toISOString(),
      }),
    });
  });
}

async function mockQueueEndpoints(page: Page) {
  let callCount = 0;

  // Enqueue for processing
  await page.route(`${API_BASE}/api/v1/admin/queue/enqueue`, async route => {
    await route.fulfill({
      status: 202,
      contentType: 'application/json',
      body: JSON.stringify({
        jobId: 'job-catan-001',
        documentId: DOCUMENT_ID,
        status: 'Queued',
      }),
    });
  });

  // Queue list — transitions state on successive calls
  await page.route(`${API_BASE}/api/v1/admin/queue`, async route => {
    if (route.request().method() !== 'GET') {
      await route.continue();
      return;
    }
    callCount++;
    const status = callCount <= 1 ? 'Queued' : callCount <= 2 ? 'Processing' : 'Completed';
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        jobs: [
          {
            id: 'job-catan-001',
            documentId: DOCUMENT_ID,
            fileName: 'catan-regolamento.pdf',
            gameTitle: 'Catan',
            status,
            currentStep: status === 'Processing' ? 'Embedding' : undefined,
            createdAt: new Date().toISOString(),
            completedAt: status === 'Completed' ? new Date().toISOString() : undefined,
          },
        ],
        total: 1,
      }),
    });
  });

  // SSE stream for real-time updates
  await page.route(`${API_BASE}/api/v1/admin/queue/stream`, async route => {
    const sseEvents = [
      `data: ${JSON.stringify({ type: 'status_change', jobId: 'job-catan-001', status: 'Queued', fileName: 'catan-regolamento.pdf' })}\n\n`,
      `data: ${JSON.stringify({ type: 'status_change', jobId: 'job-catan-001', status: 'Processing', currentStep: 'Extracting', fileName: 'catan-regolamento.pdf' })}\n\n`,
      `data: ${JSON.stringify({ type: 'status_change', jobId: 'job-catan-001', status: 'Processing', currentStep: 'Embedding', fileName: 'catan-regolamento.pdf' })}\n\n`,
      `data: ${JSON.stringify({ type: 'status_change', jobId: 'job-catan-001', status: 'Completed', fileName: 'catan-regolamento.pdf' })}\n\n`,
    ];

    await route.fulfill({
      status: 200,
      headers: {
        'Content-Type': 'text/event-stream',
        'Cache-Control': 'no-cache',
        Connection: 'keep-alive',
      },
      body: sseEvents.join(''),
    });
  });
}

// Issue #4138: mockAgentEndpoints stood here, routing /agents/{id}, /{id}/status and
// /{id}/chat. The first two served the retired agent page; the third mocked a route
// that NEVER existed on the backend - the same non-route #4139 found the chat hook
// talking to. Nothing in this journey calls agent endpoints any more.

// ========================================
// Tests
// ========================================

test.describe('Admin PDF Embedding Journey', () => {
  test.beforeEach(async ({ page }) => {
    await seedMockRoleCookies(page, 'Admin');
    await mockAdminAuth(page);
  });

  test('Scenario 1: Create game via BGG wizard search', async ({ page }) => {
    await mockBggSearch(page);
    await mockGameCreation(page);

    // Navigate to new game wizard
    await page.goto('/admin/games/new');
    await page.waitForLoadState('networkidle');

    // Step 1: Search BGG
    const searchInput = page.getByPlaceholder(/Search BoardGameGeek/i);
    await expect(searchInput).toBeVisible({ timeout: 10000 });
    await searchInput.fill('Catan');

    // Wait for search results to appear
    await expect(page.getByText('Catan').first()).toBeVisible({ timeout: 5000 });

    // Select Catan from results (click the game card, not the expansion)
    const catanResult = page.getByText('Catan').first();
    await catanResult.click();

    // Step 2: Game Details — verify selected game info is shown
    await expect(page.getByText(/Selected from BoardGameGeek/i)).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('1995')).toBeVisible();

    // Click "Create Game"
    const createButton = page.getByRole('button', { name: /Create Game/i });
    await expect(createButton).toBeVisible();
    await createButton.click();

    // Wait for game creation success
    await expect(page.getByText(/Game Created/i)).toBeVisible({ timeout: 5000 });
  });

  test('Scenario 2: Upload PDF rulebook', async ({ page }) => {
    await mockBggSearch(page);
    await mockGameCreation(page);
    await mockPdfUpload(page);

    // Navigate and go through wizard to PDF upload step
    await page.goto('/admin/games/new');
    await page.waitForLoadState('networkidle');

    // Step 1: Search and select
    const searchInput = page.getByPlaceholder(/Search BoardGameGeek/i);
    await expect(searchInput).toBeVisible({ timeout: 10000 });
    await searchInput.fill('Catan');
    await expect(page.getByText('Catan').first()).toBeVisible({ timeout: 5000 });
    await page.getByText('Catan').first().click();

    // Step 2: Create game
    await expect(page.getByRole('button', { name: /Create Game/i })).toBeVisible({ timeout: 5000 });
    await page.getByRole('button', { name: /Create Game/i }).click();
    await expect(page.getByText(/Game Created/i)).toBeVisible({ timeout: 5000 });

    // Step 3: Upload PDF — should auto-advance or have a "Next" button
    await expect(page.getByText(/Upload Rulebook PDF/i)).toBeVisible({ timeout: 5000 });

    // Upload a test PDF file
    const fileInput = page.locator('input[type="file"]');
    if (await fileInput.isVisible()) {
      // Create a minimal PDF buffer for testing
      const pdfContent = Buffer.from('%PDF-1.4 test content');
      await fileInput.setInputFiles({
        name: 'catan-regolamento.pdf',
        mimeType: 'application/pdf',
        buffer: pdfContent,
      });

      // Click upload button
      const uploadButton = page.getByRole('button', { name: /Upload PDF/i });
      if (await uploadButton.isVisible()) {
        await uploadButton.click();
      }

      // Verify upload success
      await expect(page.getByText(/PDF uploaded successfully/i)).toBeVisible({ timeout: 5000 });
    }
  });

  test('Scenario 3: Monitor embedding queue in admin dashboard', async ({ page }) => {
    await mockQueueEndpoints(page);

    // Navigate to queue dashboard
    await page.goto('/admin/knowledge-base/queue');
    await page.waitForLoadState('networkidle');

    // Verify queue page loads with the job
    await expect(page.getByText(/Processing Queue/i)).toBeVisible({ timeout: 10000 });

    // Verify the job appears with the PDF filename
    await expect(page.getByText('catan-regolamento.pdf')).toBeVisible({ timeout: 5000 });

    // Verify status is shown (Queued, Processing, or Completed depending on timing)
    const statusIndicator = page.getByText(/Queued|Processing|Completed/i).first();
    await expect(statusIndicator).toBeVisible({ timeout: 5000 });

    // Verify connection status indicator is present (Live or Polling)
    await expect(page.getByText(/Live|Polling/i).first()).toBeVisible({ timeout: 5000 });
  });

  // Issue #4138: "Scenario 4: Test RAG agent with citations" stood here. It drove
  // `/agents/{id}` and the `agent-info-tab-chat` tab, both deleted with the
  // user-facing agents section (#4141). What it actually asserted - a grounded
  // answer carrying a page citation - is worth keeping, but it belongs on the
  // game-scoped chat (`/library/{gameId}?tab=aiChat`); re-pointing the selectors
  // here would have produced a spec that cannot pass, dressed up as one that can.

  test('Full journey: BGG wizard → PDF upload → queue', async ({ page }) => {
    // Setup all mocks
    await mockBggSearch(page);
    await mockGameCreation(page);
    await mockPdfUpload(page);
    await mockQueueEndpoints(page);

    // === Step 1: Navigate to wizard and search BGG ===
    await page.goto('/admin/games/new');
    await page.waitForLoadState('networkidle');

    const searchInput = page.getByPlaceholder(/Search BoardGameGeek/i);
    await expect(searchInput).toBeVisible({ timeout: 10000 });
    await searchInput.fill('Catan');
    await expect(page.getByText('Catan').first()).toBeVisible({ timeout: 5000 });
    await page.getByText('Catan').first().click();

    // === Step 2: Create game ===
    await expect(page.getByRole('button', { name: /Create Game/i })).toBeVisible({ timeout: 5000 });
    await page.getByRole('button', { name: /Create Game/i }).click();
    await expect(page.getByText(/Game Created/i)).toBeVisible({ timeout: 5000 });

    // === Step 3: Upload PDF ===
    const uploadHeading = page.getByText(/Upload Rulebook PDF/i);
    if (await uploadHeading.isVisible({ timeout: 3000 })) {
      const fileInput = page.locator('input[type="file"]');
      if (await fileInput.isVisible()) {
        await fileInput.setInputFiles({
          name: 'catan-regolamento.pdf',
          mimeType: 'application/pdf',
          buffer: Buffer.from('%PDF-1.4 test content'),
        });

        const uploadBtn = page.getByRole('button', { name: /Upload PDF/i });
        if (await uploadBtn.isVisible({ timeout: 2000 })) {
          await uploadBtn.click();
          await expect(page.getByText(/PDF uploaded successfully/i)).toBeVisible({ timeout: 5000 });
        }
      }
    }

    // === Step 4: Check queue dashboard ===
    await page.goto('/admin/knowledge-base/queue');
    await page.waitForLoadState('networkidle');

    await expect(page.getByText(/Processing Queue/i)).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('catan-regolamento.pdf')).toBeVisible({ timeout: 5000 });

    // Issue #4138: a "Step 5: Test agent with RAG" stood here, navigating to
    // `/agents/{AGENT_ID}`. Same deleted page as the retired Scenario 4 above. The
    // journey this test owns - BGG wizard → PDF upload → queue - is intact and
    // still asserted; the chat leg moves with the rest of the grounded-answer
    // coverage to the game-scoped surface.
  });
});
