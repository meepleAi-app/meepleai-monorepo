/**
 * Library Management Flow - Visual Documentation
 *
 * Captures visual documentation for library management flows:
 * - Add game to library
 * - Remove game from library
 * - Organize collections
 * - View library with quota info
 *
 * @see docs/08-user-flows/user-role/03-library-management.md
 */

import { test } from '../../fixtures/chromatic';
import { AuthHelper, USER_FIXTURES } from '../../pages';
import {
  ScreenshotHelper,
  USER_FLOWS,
  disableAnimations,
  waitForStableState,
  ANNOTATION_COLORS,
} from '../fixtures/screenshot-helpers';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Mock library data
const MOCK_LIBRARY = [
  {
    id: 'lib-1',
    gameId: 'game-1',
    game: {
      id: 'game-1',
      title: 'Ticket to Ride',
      publisher: 'Days of Wonder',
      yearPublished: 2004,
      thumbnailUrl: 'https://example.com/thumb1.jpg',
    },
    addedAt: '2026-01-15T10:00:00Z',
    playCount: 5,
    lastPlayedAt: '2026-01-18T20:00:00Z',
  },
  {
    id: 'lib-2',
    gameId: 'game-2',
    game: {
      id: 'game-2',
      title: 'Catan',
      publisher: 'Kosmos',
      yearPublished: 1995,
      thumbnailUrl: 'https://example.com/thumb2.jpg',
    },
    addedAt: '2026-01-10T10:00:00Z',
    playCount: 12,
    lastPlayedAt: '2026-01-19T18:00:00Z',
  },
];

test.describe('Library Management Flow - Visual Documentation', () => {
  let helper: ScreenshotHelper;
  let authHelper: AuthHelper;

  test.beforeEach(async ({ page }) => {
    helper = new ScreenshotHelper({
      outputDir: USER_FLOWS.libraryManagement.outputDir,
      flow: USER_FLOWS.libraryManagement.name,
      role: USER_FLOWS.libraryManagement.role,
    });
    authHelper = new AuthHelper(page);
    await disableAnimations(page);

    // Setup authenticated session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // Mock library endpoint
    await page.route(`${API_BASE}/api/v1/library**`, async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_LIBRARY),
        });
      } else if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({ id: 'new-lib-entry', gameId: 'game-3' }),
        });
      } else if (route.request().method() === 'DELETE') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ success: true }),
        });
      } else {
        await route.continue();
      }
    });

    // Mock quota endpoint
    await page.route(`${API_BASE}/api/v1/users/me/upload-quota`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          daily: { used: 3, limit: 5 },
          weekly: { used: 10, limit: 20 },
          libraryCount: 2,
          libraryLimit: 5,
        }),
      });
    });
  });

  test('view library - game collection', async ({ page }) => {
    // Step 1: Navigate to library
    await page.goto('/library');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'My Game Library',
      description: 'Personal game library showing owned games',
      annotations: [
        { selector: 'h1, [data-testid="library-heading"]', label: 'Library', color: ANNOTATION_COLORS.primary },
      ],
      nextAction: 'Browse library',
    });

    // Step 2: Library cards
    const libraryCard = page.locator('[data-testid="library-card"], .library-item, article').first();
    if (await libraryCard.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Library Games',
        description: 'Games in library with play statistics',
        annotations: [
          { selector: '[data-testid="library-card"], .library-item, article', label: 'Game Entry', color: ANNOTATION_COLORS.info },
        ],
        previousAction: 'View library',
        nextAction: 'Manage game',
      });
    }

    // Step 3: Quota display (if visible)
    const quotaDisplay = page.locator('[data-testid="quota"], text=/\\d+.*\\/.*\\d+/, .quota-info').first();
    if (await quotaDisplay.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 3,
        title: 'Library Quota',
        description: 'Current library usage and limits based on tier',
        annotations: [
          { selector: '[data-testid="quota"], .quota-info', label: 'Quota Info', color: ANNOTATION_COLORS.warning },
        ],
        previousAction: 'View quota',
        nextAction: 'Check limits',
      });
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ Library view captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('add game to library', async ({ page }) => {
    // Mock games endpoint for catalog
    await page.route(`${API_BASE}/api/v1/games*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'game-3', title: 'Wingspan', publisher: 'Stonemaier Games', yearPublished: 2019 },
        ]),
      });
    });

    // Step 1: Navigate to games catalog
    await page.goto('/games');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Game Catalog',
      description: 'Browse catalog to find games to add',
      nextAction: 'Find game to add',
    });

    // Step 2: Click add button
    const addButton = page.locator('button:has-text("Add to Library"), button:has-text("Aggiungi"), [data-testid="add-to-library"]').first();
    if (await addButton.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Add to Library Button',
        description: 'Click to add game to personal library',
        annotations: [
          { selector: 'button:has-text("Add to Library"), button:has-text("Aggiungi")', label: 'Add Button', color: ANNOTATION_COLORS.success },
        ],
        previousAction: 'Find game',
        nextAction: 'Click add',
      });
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Add to library captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('remove game from library', async ({ page }) => {
    // Step 1: Navigate to library
    await page.goto('/library');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Library for Removal',
      description: 'View library to remove a game',
      nextAction: 'Select game to remove',
    });

    // Step 2: Find remove button
    const removeButton = page.locator('button:has-text("Remove"), button:has-text("Rimuovi"), [data-testid="remove-game"]').first();
    if (await removeButton.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Remove Game Option',
        description: 'Remove game from library',
        annotations: [
          { selector: 'button:has-text("Remove"), button:has-text("Rimuovi")', label: 'Remove', color: ANNOTATION_COLORS.error },
        ],
        previousAction: 'Find game',
        nextAction: 'Confirm removal',
      });
    }

    // Step 3: Confirmation dialog (if exists)
    const confirmDialog = page.locator('[role="dialog"], [data-testid="confirm-dialog"], .modal').first();
    if (await confirmDialog.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 3,
        title: 'Confirm Removal',
        description: 'Confirm game removal from library',
        annotations: [
          { selector: '[role="dialog"], .modal', label: 'Confirm Dialog', color: ANNOTATION_COLORS.warning },
        ],
        previousAction: 'Click remove',
        nextAction: 'Confirm or cancel',
      });
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ Remove from library captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('upload custom PDF to library game', async ({ page }) => {
    // Mock PDF upload
    await page.route(`${API_BASE}/api/v1/ingest/pdf`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ documentId: 'new-pdf-id' }),
      });
    });

    // Step 1: Navigate to game in library
    await page.goto('/games/game-1');
    await waitForStableState(page);

    // Step 2: Go to Rules tab
    const rulesTab = page.locator('button:has-text("Rules"), [role="tab"]:has-text("Rules")').first();
    if (await rulesTab.isVisible({ timeout: 2000 }).catch(() => false)) {
      await rulesTab.click();
      await waitForStableState(page);

      await helper.capture(page, {
        step: 1,
        title: 'Rules Tab',
        description: 'Navigate to Rules section to upload PDF',
        annotations: [
          { selector: 'button:has-text("Upload"), [data-testid="upload-pdf"]', label: 'Upload PDF', color: ANNOTATION_COLORS.primary },
        ],
        nextAction: 'Click upload',
      });
    }

    // Step 3: Upload modal/form
    const uploadButton = page.locator('button:has-text("Upload"), [data-testid="upload-pdf"]').first();
    if (await uploadButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await uploadButton.click();
      await waitForStableState(page);

      const uploadForm = page.locator('[role="dialog"], .upload-form, form').first();
      if (await uploadForm.isVisible({ timeout: 2000 }).catch(() => false)) {
        await helper.capture(page, {
          step: 2,
          title: 'PDF Upload Form',
          description: 'Upload a custom PDF rulebook',
          annotations: [
            { selector: 'input[type="file"]', label: 'File Input', color: ANNOTATION_COLORS.primary },
          ],
          previousAction: 'Open upload form',
          nextAction: 'Select PDF file',
        });
      }
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ PDF upload captured: ${helper.getCapturedSteps().length} screenshots`);
  });
});
