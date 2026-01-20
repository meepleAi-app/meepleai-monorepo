/**
 * Game Management Flow - Visual Documentation (Editor Role)
 *
 * Captures visual documentation for game management flows:
 * - Create game in catalog
 * - Edit game details
 * - Delete game (soft delete)
 * - BGG import
 *
 * @see docs/08-user-flows/editor-role/01-game-management.md
 */

import { test } from '../../fixtures/chromatic';
import { AuthHelper, USER_FIXTURES } from '../../pages';
import {
  ScreenshotHelper,
  EDITOR_FLOWS,
  disableAnimations,
  waitForStableState,
  ANNOTATION_COLORS,
} from '../fixtures/screenshot-helpers';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Mock shared games data
const MOCK_SHARED_GAMES = [
  {
    id: 'shared-1',
    title: 'Ticket to Ride',
    publisher: 'Days of Wonder',
    yearPublished: 2004,
    status: 'published',
    createdBy: 'editor-test-1',
    createdAt: '2026-01-15T10:00:00Z',
  },
  {
    id: 'shared-2',
    title: 'Catan',
    publisher: 'Kosmos',
    yearPublished: 1995,
    status: 'draft',
    createdBy: 'editor-test-1',
    createdAt: '2026-01-18T14:00:00Z',
  },
];

test.describe('Game Management Flow - Visual Documentation (Editor)', () => {
  let helper: ScreenshotHelper;
  let authHelper: AuthHelper;

  test.beforeEach(async ({ page }) => {
    helper = new ScreenshotHelper({
      outputDir: EDITOR_FLOWS.gameManagement.outputDir,
      flow: EDITOR_FLOWS.gameManagement.name,
      role: EDITOR_FLOWS.gameManagement.role,
    });
    authHelper = new AuthHelper(page);
    await disableAnimations(page);

    // Setup editor session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.editor);

    // Mock shared games endpoints
    await page.route(`${API_BASE}/api/v1/admin/shared-games*`, async route => {
      const url = route.request().url();
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({ id: 'new-shared-game', title: 'New Game', status: 'draft' }),
        });
      } else if (url.includes('/shared-games/shared-1')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_SHARED_GAMES[0]),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ items: MOCK_SHARED_GAMES, total: 2 }),
        });
      }
    });

    // Mock BGG endpoints
    await page.route(`${API_BASE}/api/v1/bgg/search*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          results: [
            { bggId: 266192, name: 'Wingspan', yearPublished: 2019, thumbnailUrl: 'https://example.com/thumb.jpg' },
          ],
        }),
      });
    });
  });

  test('create game - new entry in catalog', async ({ page }) => {
    // Step 1: Navigate to admin games
    await page.goto('/admin/games');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Games Management',
      description: 'Editor view of shared game catalog',
      annotations: [
        { selector: 'button:has-text("New Game"), button:has-text("Create"), [data-testid="create-game"]', label: 'Create Game', color: ANNOTATION_COLORS.success },
      ],
      nextAction: 'Create new game',
    });

    // Step 2: Click create
    const createBtn = page.locator('button:has-text("New Game"), button:has-text("Create"), [data-testid="create-game"]').first();
    if (await createBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await createBtn.click();
      await waitForStableState(page);

      // Step 3: Game form
      const gameForm = page.locator('form, [data-testid="game-form"], [role="dialog"]').first();
      if (await gameForm.isVisible({ timeout: 2000 }).catch(() => false)) {
        await helper.capture(page, {
          step: 2,
          title: 'Game Creation Form',
          description: 'Form to enter new game details',
          annotations: [
            { selector: 'input[name="title"], [data-testid="game-title"]', label: 'Title', color: ANNOTATION_COLORS.primary },
            { selector: 'input[name="publisher"]', label: 'Publisher', color: ANNOTATION_COLORS.primary },
          ],
          previousAction: 'Open form',
          nextAction: 'Fill details',
        });

        // Fill form
        const titleInput = page.locator('input[name="title"], [data-testid="game-title"]').first();
        if (await titleInput.isVisible({ timeout: 1000 }).catch(() => false)) {
          await titleInput.fill('Wingspan');
          await waitForStableState(page);

          await helper.capture(page, {
            step: 3,
            title: 'Form Filled',
            description: 'Game details entered and ready to save',
            annotations: [
              { selector: 'input[name="title"]', label: 'Title Filled', color: ANNOTATION_COLORS.success },
              { selector: 'button[type="submit"], button:has-text("Save")', label: 'Save', color: ANNOTATION_COLORS.primary },
            ],
            previousAction: 'Fill form',
            nextAction: 'Save game',
          });
        }
      }
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ Create game captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('edit game - update details', async ({ page }) => {
    // Step 1: Navigate to game edit
    await page.goto('/admin/games/shared-1/edit');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Edit Game',
      description: 'Edit existing game details',
      annotations: [
        { selector: 'input[name="title"], [data-testid="game-title"]', label: 'Title', color: ANNOTATION_COLORS.primary },
      ],
      nextAction: 'Update details',
    });

    // Step 2: Make changes
    const titleInput = page.locator('input[name="title"], [data-testid="game-title"]').first();
    if (await titleInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      await titleInput.clear();
      await titleInput.fill('Ticket to Ride: Europe');
      await waitForStableState(page);

      await helper.capture(page, {
        step: 2,
        title: 'Details Updated',
        description: 'Game information modified',
        annotations: [
          { selector: 'input[name="title"]', label: 'Updated', color: ANNOTATION_COLORS.warning },
          { selector: 'button[type="submit"], button:has-text("Save")', label: 'Save Changes', color: ANNOTATION_COLORS.success },
        ],
        previousAction: 'Modify title',
        nextAction: 'Save changes',
      });
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Edit game captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('delete game - soft delete flow', async ({ page }) => {
    // Step 1: Navigate to game
    await page.goto('/admin/games/shared-2');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Game to Delete',
      description: 'View game details before deletion',
      annotations: [
        { selector: 'button:has-text("Delete"), [data-testid="delete-game"]', label: 'Delete', color: ANNOTATION_COLORS.error },
      ],
      nextAction: 'Click delete',
    });

    // Step 2: Delete confirmation
    const deleteBtn = page.locator('button:has-text("Delete"), [data-testid="delete-game"]').first();
    if (await deleteBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await deleteBtn.click();
      await waitForStableState(page);

      const confirmDialog = page.locator('[role="alertdialog"], [role="dialog"], .confirm-dialog').first();
      if (await confirmDialog.isVisible({ timeout: 2000 }).catch(() => false)) {
        await helper.capture(page, {
          step: 2,
          title: 'Confirm Deletion',
          description: 'Confirm soft-delete of the game (recoverable)',
          annotations: [
            { selector: 'button:has-text("Confirm"), button:has-text("Delete")', label: 'Confirm Delete', color: ANNOTATION_COLORS.error },
            { selector: 'button:has-text("Cancel")', label: 'Cancel', color: ANNOTATION_COLORS.neutral },
          ],
          previousAction: 'Click delete',
          nextAction: 'Confirm deletion',
        });
      }
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Delete game captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('import from BGG - BoardGameGeek integration', async ({ page }) => {
    // Step 1: Navigate to import
    await page.goto('/admin/games/import');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'BGG Import',
      description: 'Import game data from BoardGameGeek',
      annotations: [
        { selector: 'input[type="search"], input[placeholder*="Search"]', label: 'BGG Search', color: ANNOTATION_COLORS.primary },
      ],
      nextAction: 'Search BGG',
    });

    // Step 2: Search BGG
    const searchInput = page.locator('input[type="search"], input[placeholder*="Search"], input[placeholder*="BGG"]').first();
    if (await searchInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      await searchInput.fill('Wingspan');
      await waitForStableState(page);

      const searchBtn = page.locator('button:has-text("Search"), button[type="submit"]').first();
      if (await searchBtn.isVisible({ timeout: 1000 }).catch(() => false)) {
        await searchBtn.click();
        await page.waitForTimeout(500);
        await waitForStableState(page);

        await helper.capture(page, {
          step: 2,
          title: 'BGG Search Results',
          description: 'Select game from BGG search results to import',
          annotations: [
            { selector: 'text=Wingspan', label: 'Result', color: ANNOTATION_COLORS.success },
            { selector: 'button:has-text("Import"), button:has-text("Add")', label: 'Import', color: ANNOTATION_COLORS.primary },
          ],
          previousAction: 'Search BGG',
          nextAction: 'Select to import',
        });
      }
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ BGG import captured: ${helper.getCapturedSteps().length} screenshots`);
  });
});
