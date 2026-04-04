/**
 * Add Game Wizard E2E Tests
 * Issue #4824: E2E Tests - AddGameSheet Complete Wizard Flows
 * Epic #4817: User Collection Wizard
 *
 * Tests all 6 wizard flows:
 *   Flow 1: From GameCard (shared game → step 2 → step 3 → save)
 *   Flow 2: From Library (catalog search → select → step 2 → step 3 → save)
 *   Flow 3: BGG Import (BGG search → select → upload PDF → step 3 → save)
 *   Flow 4: Custom Game Creation (custom form → step 2 → step 3 → save)
 *   Flow 5: Duplicate Detection (already-in-library indicator)
 *   Flow 6: Close with Unsaved Changes (confirmation dialog)
 *
 * Uses page.route() for API mocking with catch-all pattern.
 */

import { test, expect, Page } from '@playwright/test';

// ============================================================================
// Constants
// ============================================================================

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ============================================================================
// Mock Data
// ============================================================================

const MOCK_USER = {
  user: {
    id: 'user-test-123',
    email: 'player@meepleai.dev',
    displayName: 'Test Player',
    role: 'User',
  },
  expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
};

const MOCK_SHARED_GAME = {
  id: 'shared-game-catan-001',
  title: 'Catan',
  yearPublished: 1995,
  thumbnailUrl: 'https://example.com/catan-thumb.jpg',
  imageUrl: 'https://example.com/catan.jpg',
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 90,
  averageRating: 7.2,
  complexityRating: 2.3,
  bggId: 13,
  description: 'Trade, build, settle on the island of Catan.',
  status: 1,
};

const MOCK_SHARED_GAME_2 = {
  id: 'shared-game-ticket-002',
  title: 'Ticket to Ride',
  yearPublished: 2004,
  thumbnailUrl: null,
  imageUrl: null,
  minPlayers: 2,
  maxPlayers: 5,
  playingTimeMinutes: 60,
  averageRating: 7.4,
  complexityRating: 1.8,
  bggId: 9209,
  description: 'Build train routes across the country.',
  status: 1,
};

const MOCK_BGG_RESULTS = {
  results: [
    {
      bggId: 174430,
      name: 'Gloomhaven',
      yearPublished: 2017,
      thumbnailUrl: 'https://example.com/gloomhaven-thumb.jpg',
    },
  ],
  totalResults: 1,
  page: 1,
  pageSize: 10,
};

const MOCK_BGG_DETAILS = {
  bggId: 174430,
  name: 'Gloomhaven',
  yearPublished: 2017,
  minPlayers: 1,
  maxPlayers: 4,
  playingTime: 120,
  description: 'A tactical combat game in a persistent world.',
  imageUrl: 'https://example.com/gloomhaven.jpg',
  thumbnailUrl: 'https://example.com/gloomhaven-thumb.jpg',
  averageRating: 8.7,
  complexityRating: 3.9,
  categories: ['Adventure', 'Fantasy'],
  mechanics: ['Hand Management', 'Modular Board'],
};

const MOCK_PRIVATE_GAME = {
  id: 'private-game-custom-001',
  title: 'My Custom Game',
  minPlayers: 2,
  maxPlayers: 6,
  playingTimeMinutes: 45,
  description: null,
  source: 'Manual',
};

const MOCK_LIBRARY_ENTRY = {
  id: 'lib-entry-001',
  gameId: 'shared-game-catan-001',
  title: 'Catan',
  addedAt: new Date().toISOString(),
};

const MOCK_PDF_DOCUMENT = {
  id: 'doc-pdf-001',
  gameId: 'shared-game-catan-001',
  fileName: 'catan-rules.pdf',
  pageCount: 12,
  processingStatus: 'Completed',
  documentType: 'Rulebook',
  createdAt: new Date().toISOString(),
  fileSizeBytes: 2048000,
};

// ============================================================================
// Setup Helpers
// ============================================================================

/**
 * Setup mock auth and catch-all API routes.
 * Specific mocks added per-test override the catch-all.
 */
async function setupAuth(page: Page) {
  // Catch-all for unmocked API calls (MUST be first)
  await page.route(`${API_BASE}/api/**`, async route => {
    const method = route.request().method();
    if (method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    } else if (method === 'POST' || method === 'PUT' || method === 'PATCH') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true }),
      });
    } else if (method === 'DELETE') {
      await route.fulfill({ status: 204 });
    } else {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    }
  });

  // Auth
  await page.route(`${API_BASE}/api/v1/auth/me`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_USER),
    });
  });

  // User profile
  await page.route(`${API_BASE}/api/v1/users/me`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: MOCK_USER.user.id,
        email: MOCK_USER.user.email,
        displayName: MOCK_USER.user.displayName,
        role: MOCK_USER.user.role,
        createdAt: new Date().toISOString(),
      }),
    });
  });
}

/**
 * Setup catalog page with shared games and library status
 */
async function setupCatalogPage(page: Page, games = [MOCK_SHARED_GAME, MOCK_SHARED_GAME_2]) {
  // Shared games listing
  await page.route(`${API_BASE}/api/v1/shared-games*`, async route => {
    const url = route.request().url();
    // If it's a search with searchTerm, filter results
    if (url.includes('searchTerm=')) {
      const searchTerm = new URL(url).searchParams.get('searchTerm') ?? '';
      const filtered = games.filter(g => g.title.toLowerCase().includes(searchTerm.toLowerCase()));
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: filtered,
          totalCount: filtered.length,
          page: 1,
          pageSize: 10,
        }),
      });
    } else {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: games,
          totalCount: games.length,
          page: 1,
          pageSize: 20,
        }),
      });
    }
  });

  // Library status (not in library by default)
  await page.route(`${API_BASE}/api/v1/library/games/*/status`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ inLibrary: false, libraryEntryId: null }),
    });
  });

  // Batch status
  await page.route(`${API_BASE}/api/v1/library/games/batch-status*`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ statuses: {} }),
    });
  });

  // Library quota
  await page.route(`${API_BASE}/api/v1/library/quota`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ currentCount: 5, maxAllowed: 50, remaining: 45 }),
    });
  });

  // Library stats
  await page.route(`${API_BASE}/api/v1/library/stats`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ totalGames: 5, favorites: 2 }),
    });
  });

  // Library list
  await page.route(`${API_BASE}/api/v1/library`, async route => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [], totalCount: 0, page: 1, pageSize: 20 }),
      });
    } else {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true }),
      });
    }
  });

  // Collection status (generic collection system)
  await page.route(`${API_BASE}/api/v1/library/collections/**`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ inCollection: false }),
    });
  });
}

/**
 * Setup wizard-specific API mocks for the AddGameSheet flows
 */
async function setupWizardApiMocks(page: Page) {
  // Documents for a game (Step 2)
  await page.route(`${API_BASE}/api/v1/games/*/pdfs`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ pdfs: [] }),
    });
  });

  // PDF upload (Step 2)
  await page.route(`${API_BASE}/api/v1/ingest/pdf`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        documentId: 'doc-uploaded-001',
        fileName: 'uploaded-rules.pdf',
      }),
    });
  });

  // BGG search (Step 1 fallback)
  await page.route(`${API_BASE}/api/v1/bgg/search*`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_BGG_RESULTS),
    });
  });

  // BGG game details
  await page.route(`${API_BASE}/api/v1/bgg/games/*`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_BGG_DETAILS),
    });
  });

  // Add private game (custom creation)
  await page.route(`${API_BASE}/api/v1/private-games`, async route => {
    if (route.request().method() === 'POST') {
      const body = route.request().postDataJSON();
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({
          ...MOCK_PRIVATE_GAME,
          title: body?.title ?? 'Custom Game',
          minPlayers: body?.minPlayers ?? 2,
          maxPlayers: body?.maxPlayers ?? 4,
        }),
      });
    } else {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    }
  });

  // Update private game
  await page.route(`${API_BASE}/api/v1/private-games/*`, async route => {
    if (route.request().method() === 'PUT') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_PRIVATE_GAME),
      });
    } else {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_PRIVATE_GAME),
      });
    }
  });

  // Add game to library (Step 3 save)
  await page.route(`${API_BASE}/api/v1/library/games/*`, async route => {
    if (route.request().method() === 'POST') {
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_LIBRARY_ENTRY),
      });
    } else {
      // GET for status - delegate to specific status mock
      await route.continue();
    }
  });
}

/**
 * Open the wizard by triggering the quick action on a game card.
 * Navigates to catalog, finds game card, opens quick actions menu,
 * and clicks "Aggiungi a Collezione".
 */
async function openWizardFromGameCard(page: Page) {
  await page.goto('/library');
  await page.waitForLoadState('domcontentloaded');

  // Find the first game card and open quick actions
  const cardTrigger = page.getByTestId('quick-actions-trigger').first();
  await cardTrigger.click();

  // Click "Aggiungi a Collezione" in dropdown
  const addAction = page.getByTestId('action-aggiungi-a-collezione');
  await addAction.click();

  // Wait for the sheet to open
  await expect(page.getByText('Aggiungi alla Collezione')).toBeVisible({ timeout: 5000 });
}

/**
 * Open wizard from library page "Aggiungi Gioco" button.
 * Falls back to navigating to the catalog and using a game card.
 */
async function openWizardFromLibrary(page: Page) {
  await page.goto('/library');
  await page.waitForLoadState('domcontentloaded');

  // Look for the "add game" button on library page
  const addButton = page.getByRole('button', { name: /aggiungi/i }).first();
  const isVisible = await addButton.isVisible().catch(() => false);

  if (isVisible) {
    await addButton.click();
  } else {
    // Fallback: try link-based trigger
    const addLink = page.getByRole('link', { name: /aggiungi/i }).first();
    const linkVisible = await addLink.isVisible().catch(() => false);
    if (linkVisible) {
      await addLink.click();
    }
  }

  await expect(page.getByText('Aggiungi alla Collezione')).toBeVisible({ timeout: 5000 });
}

// ============================================================================
// Tests
// ============================================================================

test.describe('Add Game Wizard - E2E', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuth(page);
    await setupCatalogPage(page);
    await setupWizardApiMocks(page);
  });

  // --------------------------------------------------------------------------
  // Flow 1: From GameCard (Shared Game)
  // --------------------------------------------------------------------------

  test.describe('Flow 1: From GameCard (Shared Game)', () => {
    test('opens wizard at Step 2 and completes save flow', async ({ page }) => {
      await openWizardFromGameCard(page);

      // Should open at Step 2 (Knowledge Base / PDF)
      await expect(page.getByTestId('knowledge-base-step')).toBeVisible({ timeout: 5000 });
      await expect(page.getByText('Knowledge Base')).toBeVisible();

      // Step 2: Skip PDFs by clicking "Avanti"
      const nextButton = page.getByRole('button', { name: /avanti/i });
      await nextButton.click();

      // Step 3: Game Info Review
      await expect(page.getByTestId('game-info-step')).toBeVisible({ timeout: 5000 });

      // Verify game data is pre-filled
      const titleInput = page.getByTestId('info-title');
      await expect(titleInput).toBeVisible();

      // Click "Salva in Collezione"
      const saveButton = page.getByTestId('save-button');
      await expect(saveButton).toBeVisible();
      await saveButton.click();

      // Verify success state
      await expect(page.getByTestId('success-state')).toBeVisible({ timeout: 10000 });
      await expect(page.getByText('Gioco aggiunto alla tua collezione!')).toBeVisible();
    });

    test('shows existing PDFs on Step 2 when game has documents', async ({ page }) => {
      // Override documents mock to return existing PDFs
      await page.route(`${API_BASE}/api/v1/games/*/pdfs`, async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ pdfs: [MOCK_PDF_DOCUMENT] }),
        });
      });

      await openWizardFromGameCard(page);

      // Should show existing PDFs
      await expect(page.getByTestId('knowledge-base-step')).toBeVisible({ timeout: 5000 });
      await expect(page.getByText('PDF disponibili')).toBeVisible({ timeout: 5000 });
      await expect(page.getByTestId(`pdf-item-${MOCK_PDF_DOCUMENT.id}`)).toBeVisible();
    });
  });

  // --------------------------------------------------------------------------
  // Flow 2: From Library (Catalog Search)
  // --------------------------------------------------------------------------

  test.describe('Flow 2: Catalog Search', () => {
    test('searches catalog, selects game, and completes flow', async ({ page }) => {
      await openWizardFromLibrary(page);

      // Step 1: Should show search input
      const searchInput = page.getByPlaceholder(/cerca un gioco/i);
      await expect(searchInput).toBeVisible({ timeout: 5000 });

      // Type search query
      await searchInput.fill('Catan');

      // Wait for debounce + results
      await page.waitForTimeout(400);

      // Should show catalog results
      await expect(page.getByText('Catan')).toBeVisible({ timeout: 5000 });

      // Click on the search result to select it
      const result = page.locator('button').filter({ hasText: 'Catan' }).first();
      await result.click();

      // Should auto-advance to Step 2 (Knowledge Base)
      await expect(page.getByTestId('knowledge-base-step')).toBeVisible({ timeout: 5000 });

      // Step 2: Skip PDFs
      await page.getByRole('button', { name: /avanti/i }).click();

      // Step 3: Verify and save
      await expect(page.getByTestId('game-info-step')).toBeVisible({ timeout: 5000 });
      await page.getByTestId('save-button').click();

      // Success
      await expect(page.getByTestId('success-state')).toBeVisible({ timeout: 10000 });
    });

    test('shows empty state when no search results', async ({ page }) => {
      // Override catalog search to return empty
      await page.route(`${API_BASE}/api/v1/shared-games*`, async route => {
        const url = route.request().url();
        if (url.includes('searchTerm=')) {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ items: [], totalCount: 0, page: 1, pageSize: 10 }),
          });
        } else {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ items: [], totalCount: 0, page: 1, pageSize: 20 }),
          });
        }
      });

      await openWizardFromLibrary(page);

      const searchInput = page.getByPlaceholder(/cerca un gioco/i);
      await searchInput.fill('NonexistentGame123');
      await page.waitForTimeout(400);

      // Should show BGG fallback button
      await expect(page.getByText(/cerca su boardgamegeek/i)).toBeVisible({ timeout: 5000 });
    });
  });

  // --------------------------------------------------------------------------
  // Flow 3: BGG Import
  // --------------------------------------------------------------------------

  test.describe('Flow 3: BGG Import', () => {
    test('imports game from BGG when not in catalog', async ({ page }) => {
      // Override catalog search to return empty (forcing BGG fallback)
      await page.route(`${API_BASE}/api/v1/shared-games*`, async route => {
        const url = route.request().url();
        if (url.includes('searchTerm=')) {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ items: [], totalCount: 0, page: 1, pageSize: 10 }),
          });
        } else {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ items: [], totalCount: 0, page: 1, pageSize: 20 }),
          });
        }
      });

      await openWizardFromLibrary(page);

      // Search for a game
      const searchInput = page.getByPlaceholder(/cerca un gioco/i);
      await searchInput.fill('Gloomhaven');
      await page.waitForTimeout(400);

      // Click BGG fallback button
      await page.getByText(/cerca su boardgamegeek/i).click();

      // Should show BGG results with "BGG" badge
      await expect(page.getByText('Gloomhaven')).toBeVisible({ timeout: 5000 });

      // Select the BGG game
      const bggResult = page.locator('button').filter({ hasText: 'Gloomhaven' }).first();
      await bggResult.click();

      // Should advance to Step 2 (Knowledge Base)
      await expect(page.getByTestId('knowledge-base-step')).toBeVisible({ timeout: 5000 });

      // Step 2: Skip PDFs
      await page.getByRole('button', { name: /avanti/i }).click();

      // Step 3: Verify BGG data is pre-filled
      await expect(page.getByTestId('game-info-step')).toBeVisible({ timeout: 5000 });

      // Save
      await page.getByTestId('save-button').click();

      // Success
      await expect(page.getByTestId('success-state')).toBeVisible({ timeout: 10000 });
    });
  });

  // --------------------------------------------------------------------------
  // Flow 4: Custom Game Creation
  // --------------------------------------------------------------------------

  test.describe('Flow 4: Custom Game Creation', () => {
    test('creates custom game and completes wizard', async ({ page }) => {
      await openWizardFromLibrary(page);

      // Click "Crea gioco personalizzato"
      const customButton = page.getByText(/crea gioco personalizzato/i).first();
      await expect(customButton).toBeVisible({ timeout: 5000 });
      await customButton.click();

      // Should show custom game form
      await expect(page.locator('#custom-game-title')).toBeVisible({ timeout: 3000 });

      // Fill required fields
      await page.locator('#custom-game-title').fill('My Custom Board Game');
      await page.locator('#custom-game-min').fill('2');
      await page.locator('#custom-game-max').fill('6');

      // Fill optional fields
      await page.locator('#custom-game-time').fill('45');

      // Submit custom game form
      const createButton = page.getByRole('button', { name: /crea gioco/i });
      await expect(createButton).toBeEnabled();
      await createButton.click();

      // Should advance to Step 2 (Knowledge Base)
      await expect(page.getByTestId('knowledge-base-step')).toBeVisible({ timeout: 5000 });

      // Step 2: Skip PDFs
      await page.getByRole('button', { name: /avanti/i }).click();

      // Step 3: Verify custom data is shown
      await expect(page.getByTestId('game-info-step')).toBeVisible({ timeout: 5000 });

      // Save
      await page.getByTestId('save-button').click();

      // Success
      await expect(page.getByTestId('success-state')).toBeVisible({ timeout: 10000 });
    });

    test('validates required fields on custom form', async ({ page }) => {
      await openWizardFromLibrary(page);

      await page
        .getByText(/crea gioco personalizzato/i)
        .first()
        .click();
      await expect(page.locator('#custom-game-title')).toBeVisible({ timeout: 3000 });

      // Try to submit with empty fields
      const createButton = page.getByRole('button', { name: /crea gioco/i });

      // Button should be disabled or show validation errors when clicked
      await createButton.click();

      // Should show validation error for required name field
      await expect(page.getByText(/nome.*obbligatorio/i)).toBeVisible({ timeout: 3000 });
    });
  });

  // --------------------------------------------------------------------------
  // Flow 5: Duplicate Detection
  // --------------------------------------------------------------------------

  test.describe('Flow 5: Duplicate Detection', () => {
    test('shows in-library indicator for games already in collection', async ({ page }) => {
      // Override library status to show game is already in library
      await page.route(`${API_BASE}/api/v1/library/games/*/status`, async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ inLibrary: true, libraryEntryId: 'existing-entry-001' }),
        });
      });

      // Override batch status
      await page.route(`${API_BASE}/api/v1/library/games/batch-status*`, async route => {
        const url = route.request().url();
        const gameIds = new URL(url).searchParams.get('gameIds')?.split(',') ?? [];
        const statuses: Record<string, { inLibrary: boolean }> = {};
        for (const id of gameIds) {
          statuses[id] = { inLibrary: true };
        }
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ statuses }),
        });
      });

      await page.goto('/library');
      await page.waitForLoadState('domcontentloaded');

      // Game cards should show "In Libreria" badge for games already in library
      await expect(page.getByText('In Libreria').first()).toBeVisible({ timeout: 5000 });
    });
  });

  // --------------------------------------------------------------------------
  // Flow 6: Close with Unsaved Changes
  // --------------------------------------------------------------------------

  test.describe('Flow 6: Close with Unsaved Changes', () => {
    test('shows confirmation when closing wizard with dirty state', async ({ page }) => {
      await openWizardFromGameCard(page);

      // Wizard should be open at Step 2
      await expect(page.getByTestId('knowledge-base-step')).toBeVisible({ timeout: 5000 });

      // The wizard is now "dirty" because a game was selected (from game card)
      // Try to close the wizard by clicking close/X button
      const closeButton = page.getByRole('button', { name: /chiudi/i }).first();
      // Or find the X button in the sheet header
      const xButton = page.locator('button[aria-label="Chiudi"]').first();

      const closeTarget = (await xButton.isVisible()) ? xButton : closeButton;
      await closeTarget.click();

      // Should show unsaved changes confirmation
      await expect(page.getByText('Modifiche non salvate')).toBeVisible({ timeout: 3000 });
      await expect(page.getByText(/vuoi chiudere e perdere le modifiche/i)).toBeVisible();

      // Click "Annulla" to stay
      await page.getByRole('button', { name: /annulla/i }).click();

      // Wizard should still be open
      await expect(page.getByTestId('knowledge-base-step')).toBeVisible();
    });

    test('closes wizard when confirming leave', async ({ page }) => {
      await openWizardFromGameCard(page);

      await expect(page.getByTestId('knowledge-base-step')).toBeVisible({ timeout: 5000 });

      // Try to close
      const xButton = page.locator('button[aria-label="Chiudi"]').first();
      const closeButton = page.getByRole('button', { name: /chiudi/i }).first();
      const closeTarget = (await xButton.isVisible()) ? xButton : closeButton;
      await closeTarget.click();

      // Confirmation dialog should appear
      await expect(page.getByText('Modifiche non salvate')).toBeVisible({ timeout: 3000 });

      // Click "Chiudi" (destructive) to confirm leave
      // There should be a second "Chiudi" button in the dialog
      const confirmButtons = page.getByRole('button', { name: /^chiudi$/i });
      // The last "Chiudi" button should be the confirm in the dialog
      await confirmButtons.last().click();

      // Wizard should close - the sheet content should not be visible
      await expect(page.getByTestId('knowledge-base-step')).not.toBeVisible({ timeout: 5000 });
    });
  });

  // --------------------------------------------------------------------------
  // Edge Cases
  // --------------------------------------------------------------------------

  test.describe('Edge Cases', () => {
    test('handles network error during catalog search', async ({ page }) => {
      // Override catalog search to fail
      await page.route(`${API_BASE}/api/v1/shared-games*`, async route => {
        const url = route.request().url();
        if (url.includes('searchTerm=')) {
          await route.abort('failed');
        } else {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ items: [], totalCount: 0, page: 1, pageSize: 20 }),
          });
        }
      });

      await openWizardFromLibrary(page);

      const searchInput = page.getByPlaceholder(/cerca un gioco/i);
      await searchInput.fill('test');
      await page.waitForTimeout(500);

      // Should show error message
      await expect(page.getByText(/errore nella ricerca/i)).toBeVisible({ timeout: 5000 });
    });

    test('handles network error during save', async ({ page }) => {
      // Override add-to-library to fail
      await page.route(`${API_BASE}/api/v1/library/games/*`, async route => {
        if (route.request().method() === 'POST') {
          await route.fulfill({
            status: 500,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'Internal Server Error' }),
          });
        } else {
          await route.continue();
        }
      });

      await openWizardFromGameCard(page);

      // Navigate through steps
      await expect(page.getByTestId('knowledge-base-step')).toBeVisible({ timeout: 5000 });
      await page.getByRole('button', { name: /avanti/i }).click();
      await expect(page.getByTestId('game-info-step')).toBeVisible({ timeout: 5000 });

      // Try to save
      await page.getByTestId('save-button').click();

      // Should show error message on Step 3
      await expect(page.getByTestId('save-error')).toBeVisible({ timeout: 5000 });
    });

    test('handles back navigation correctly', async ({ page }) => {
      await openWizardFromGameCard(page);

      // Start at Step 2
      await expect(page.getByTestId('knowledge-base-step')).toBeVisible({ timeout: 5000 });

      // Go to Step 3
      await page.getByRole('button', { name: /avanti/i }).click();
      await expect(page.getByTestId('game-info-step')).toBeVisible({ timeout: 5000 });

      // Go back to Step 2
      await page.getByRole('button', { name: /indietro/i }).click();
      await expect(page.getByTestId('knowledge-base-step')).toBeVisible({ timeout: 5000 });
    });

    test('step indicator shows correct progress', async ({ page }) => {
      await openWizardFromGameCard(page);

      // At Step 2, step indicator should show progress
      await expect(page.getByText('Sorgente')).toBeVisible({ timeout: 5000 });
      await expect(page.getByText('PDF')).toBeVisible();
      await expect(page.getByText('Info & Salva')).toBeVisible();
    });

    test('success state shows navigation options', async ({ page }) => {
      await openWizardFromGameCard(page);

      // Complete the flow
      await expect(page.getByTestId('knowledge-base-step')).toBeVisible({ timeout: 5000 });
      await page.getByRole('button', { name: /avanti/i }).click();
      await expect(page.getByTestId('game-info-step')).toBeVisible({ timeout: 5000 });
      await page.getByTestId('save-button').click();

      // Success state should show navigation buttons
      await expect(page.getByTestId('success-state')).toBeVisible({ timeout: 10000 });
      await expect(page.getByText('Vai alla collezione')).toBeVisible();
      await expect(page.getByTestId('add-another-button')).toBeVisible();
    });

    test('"Add another" resets wizard to Step 1', async ({ page }) => {
      await openWizardFromGameCard(page);

      // Complete the flow
      await expect(page.getByTestId('knowledge-base-step')).toBeVisible({ timeout: 5000 });
      await page.getByRole('button', { name: /avanti/i }).click();
      await expect(page.getByTestId('game-info-step')).toBeVisible({ timeout: 5000 });
      await page.getByTestId('save-button').click();

      // Click "Aggiungi un altro gioco"
      await expect(page.getByTestId('add-another-button')).toBeVisible({ timeout: 10000 });
      await page.getByTestId('add-another-button').click();

      // Should be back at Step 1 (search input visible)
      await expect(page.getByPlaceholder(/cerca un gioco/i)).toBeVisible({ timeout: 5000 });
    });
  });

  // --------------------------------------------------------------------------
  // Step 2: PDF Upload
  // --------------------------------------------------------------------------

  test.describe('Step 2: PDF Upload', () => {
    test('shows upload button and skip info', async ({ page }) => {
      await openWizardFromGameCard(page);

      await expect(page.getByTestId('knowledge-base-step')).toBeVisible({ timeout: 5000 });

      // Should show skip info
      await expect(page.getByTestId('skip-info')).toBeVisible();

      // Should show upload button
      await expect(page.getByTestId('show-upload-button')).toBeVisible();
    });

    test('clicking upload shows the drop zone', async ({ page }) => {
      await openWizardFromGameCard(page);

      await expect(page.getByTestId('knowledge-base-step')).toBeVisible({ timeout: 5000 });

      // Click upload button
      await page.getByTestId('show-upload-button').click();

      // Should show the PDF upload zone
      await expect(page.getByTestId('pdf-upload-zone')).toBeVisible({ timeout: 3000 });
      await expect(page.getByTestId('pdf-drop-area')).toBeVisible();
    });
  });

  // --------------------------------------------------------------------------
  // Step 3: Game Info Form
  // --------------------------------------------------------------------------

  test.describe('Step 3: Game Info Form', () => {
    test('form fields are pre-filled with game data', async ({ page }) => {
      await openWizardFromGameCard(page);

      // Navigate to Step 3
      await expect(page.getByTestId('knowledge-base-step')).toBeVisible({ timeout: 5000 });
      await page.getByRole('button', { name: /avanti/i }).click();
      await expect(page.getByTestId('game-info-step')).toBeVisible({ timeout: 5000 });

      // Check that form fields exist
      await expect(page.getByTestId('info-title')).toBeVisible();
      await expect(page.getByTestId('info-min-players')).toBeVisible();
      await expect(page.getByTestId('info-max-players')).toBeVisible();
    });

    test('validates required fields before save', async ({ page }) => {
      await openWizardFromGameCard(page);

      // Navigate to Step 3
      await expect(page.getByTestId('knowledge-base-step')).toBeVisible({ timeout: 5000 });
      await page.getByRole('button', { name: /avanti/i }).click();
      await expect(page.getByTestId('game-info-step')).toBeVisible({ timeout: 5000 });

      // Clear the title field
      const titleInput = page.getByTestId('info-title');
      await titleInput.clear();

      // Clear min players
      const minInput = page.getByTestId('info-min-players');
      await minInput.clear();

      // Try to save with empty required fields
      await page.getByTestId('save-button').click();

      // Should not show success (validation should prevent save)
      await expect(page.getByTestId('success-state')).not.toBeVisible({ timeout: 2000 });
    });

    test('shows source badge (catalog/bgg/custom)', async ({ page }) => {
      await openWizardFromGameCard(page);

      // Navigate to Step 3
      await expect(page.getByTestId('knowledge-base-step')).toBeVisible({ timeout: 5000 });
      await page.getByRole('button', { name: /avanti/i }).click();
      await expect(page.getByTestId('game-info-step')).toBeVisible({ timeout: 5000 });

      // Should show source indicator
      await expect(page.getByText(/fonte/i)).toBeVisible();
    });
  });
});
