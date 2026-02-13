/**
 * SharedGame Manual Creation E2E Tests (Issue #4231)
 *
 * Test Coverage:
 * - Scenario 1: Happy Path - Complete Manual Creation
 * - Scenario 2: Form Validation - Required Fields
 * - Scenario 3: Duplicate Detection - Warning System
 * - Scenario 4: Navigation & Cancel - Return to List
 * - Scenario 5: Edit After Creation - Modify Saved Game
 *
 * Configuration (via playwright.config.ts):
 * - Screenshots: Enabled on failure (trace: 'on-first-retry')
 * - Cleanup: Global teardown handles memory monitoring
 * - Timeout: 60s per test, 10s per action, 60s per navigation
 * - Retries: 2 retries in CI to prevent flaky tests
 * - Performance Target: <5 minutes total execution
 *
 * @see apps/web/src/app/(authenticated)/admin/shared-games/new
 * @see apps/web/src/components/admin/shared-games/GameForm.tsx
 * @see apps/web/playwright.config.ts
 */

import { test, expect, type Page } from '@playwright/test';

// ========== Test Data ==========

const TEST_USER_ADMIN = {
  email: 'admin@test.com',
  password: 'Admin123!',
  role: 'Admin',
};

// ========== Test Fixtures & Helpers ==========

/**
 * Login as admin user (real authentication)
 */
async function loginAsAdmin(page: Page) {
  await page.goto('/login');
  await page.getByLabel(/email/i).fill(TEST_USER_ADMIN.email);
  await page.getByLabel(/password/i).fill(TEST_USER_ADMIN.password);
  await page.getByRole('button', { name: /sign in/i }).click();
  await page.waitForURL('/admin/**');
}

/**
 * Test game data factory
 */
function createTestGameData(overrides: Partial<TestGameData> = {}): TestGameData {
  const timestamp = Date.now();
  return {
    title: `Test Game ${timestamp}`,
    yearPublished: 2024,
    description: 'A test board game for E2E testing purposes',
    minPlayers: 2,
    maxPlayers: 4,
    playingTimeMinutes: 60,
    minAge: 12,
    imageUrl: 'https://via.placeholder.com/300x300',
    thumbnailUrl: 'https://via.placeholder.com/150x150',
    ...overrides,
  };
}

interface TestGameData {
  title: string;
  yearPublished: number;
  description: string;
  minPlayers: number;
  maxPlayers: number;
  playingTimeMinutes: number;
  minAge: number;
  imageUrl: string;
  thumbnailUrl: string;
}

/**
 * Fill game form with provided data
 */
async function fillGameForm(page: Page, data: TestGameData) {
  await page.getByLabel(/titolo/i).fill(data.title);
  await page.getByLabel(/anno di pubblicazione/i).fill(data.yearPublished.toString());
  await page.getByLabel(/descrizione/i).fill(data.description);
  await page.getByLabel(/giocatori minimi/i).fill(data.minPlayers.toString());
  await page.getByLabel(/giocatori massimi/i).fill(data.maxPlayers.toString());
  await page.getByLabel(/durata.*minuti/i).fill(data.playingTimeMinutes.toString());
  await page.getByLabel(/età minima/i).fill(data.minAge.toString());
  await page.getByLabel(/url immagine/i).fill(data.imageUrl);
  await page.getByLabel(/url thumbnail/i).fill(data.thumbnailUrl);
}

/**
 * Submit game form and wait for navigation
 */
async function submitGameForm(page: Page) {
  const submitButton = page.getByRole('button', { name: /salva|crea|submit/i });
  await submitButton.click();
}

/**
 * Click cancel button and wait for navigation
 */
async function cancelGameForm(page: Page) {
  const cancelButton = page.getByRole('button', { name: /annulla|cancel/i });
  await cancelButton.click();
}

// ========== Test Suite ==========

test.describe('SharedGame Manual Creation Flow', () => {
  test.beforeEach(async ({ page }) => {
    // Login as admin user
    await loginAsAdmin(page);

    // Navigate to new game page
    await page.goto('/admin/shared-games/new');

    // Wait for page to load
    await expect(page.getByRole('heading', { name: /nuovo gioco/i })).toBeVisible();
  });

  // ========== Scenario 1: Happy Path ==========

  test('Admin creates SharedGame manually - complete flow', async ({ page }) => {
    // Arrange: Prepare test data
    const testGame = createTestGameData();

    // Act: Fill and submit form
    await fillGameForm(page, testGame);
    await submitGameForm(page);

    // Assert: Verify redirect to detail page
    await expect(page).toHaveURL(/\/admin\/shared-games\/[a-f0-9-]{36}/);

    // Assert: Verify game details displayed
    await expect(page.getByRole('heading', { name: testGame.title })).toBeVisible();
    await expect(page.getByText(testGame.description)).toBeVisible();
    await expect(page.getByText(`${testGame.minPlayers}-${testGame.maxPlayers} giocatori`)).toBeVisible();
    await expect(page.getByText(`${testGame.playingTimeMinutes} min`)).toBeVisible();
    await expect(page.getByText(`${testGame.minAge}+`)).toBeVisible();

    // Assert: Verify status is Draft
    await expect(page.getByText(/bozza|draft/i)).toBeVisible();
  });

  // ========== Scenario 2: Form Validation ==========

  test('Form validates required fields correctly', async ({ page }) => {
    // Act: Submit empty form
    await submitGameForm(page);

    // Assert: Verify validation errors displayed for required fields
    await expect(page.getByText(/titolo.*obbligatorio/i)).toBeVisible();
    await expect(page.getByText(/descrizione.*obbligatoria/i)).toBeVisible();
    // Note: Year, players, time, age have default values or different validation messages

    // Act: Fill only required fields
    const minimalGame = createTestGameData();
    await fillGameForm(page, minimalGame);

    // Act: Submit form
    await submitGameForm(page);

    // Assert: Verify submission succeeds (redirect to detail page)
    await expect(page).toHaveURL(/\/admin\/shared-games\/[a-f0-9-]{36}/);
  });

  // ========== Scenario 3: Duplicate Detection ==========

  test('Duplicate warning prevents conflicts', async ({ page, context: _context }) => {
    // Arrange: Create first game
    const gameName = `Duplicate Test ${Date.now()}`;
    const firstGame = createTestGameData({ title: gameName });

    await fillGameForm(page, firstGame);
    await submitGameForm(page);

    // Wait for creation to complete
    await expect(page).toHaveURL(/\/admin\/shared-games\/[a-f0-9-]{36}/);

    // Arrange: Navigate back to new game page
    await page.goto('/admin/shared-games/new');

    // Act: Try to create another game with same title
    const duplicateGame = createTestGameData({ title: gameName });
    await fillGameForm(page, duplicateGame);
    await submitGameForm(page);

    // Assert: Verify duplicate warning shown
    // Note: Exact implementation depends on UI - adjust selector as needed
    await expect(page.getByText(/duplicato|già esiste|duplicate/i)).toBeVisible({ timeout: 5000 });

    // Assert: Verify user can proceed or cancel
    const proceedButton = page.getByRole('button', { name: /procedi|continua|yes/i });
    const cancelButton = page.getByRole('button', { name: /annulla|no/i });

    await expect(proceedButton.or(cancelButton)).toBeVisible();
  });

  // ========== Scenario 4: Navigation & Cancel ==========

  test('User can cancel and return to list', async ({ page }) => {
    // Arrange: Fill partial data
    const partialGame = createTestGameData();
    await page.getByLabel(/titolo/i).fill(partialGame.title);
    await page.getByLabel(/anno di pubblicazione/i).fill(partialGame.yearPublished.toString());

    // Act: Click cancel
    await cancelGameForm(page);

    // Assert: Verify returned to shared games list
    await expect(page).toHaveURL('/admin/shared-games');

    // Assert: Verify no game created with partial title
    // Note: This assumes the list page shows game titles
    const gameList = page.locator('[data-testid="shared-games-list"]').or(page.locator('table'));
    await expect(gameList.getByText(partialGame.title)).not.toBeVisible();
  });

  // ========== Scenario 5: Edit After Creation ==========

  test('Admin can edit created game', async ({ page }) => {
    // Arrange: Create a game
    const originalGame = createTestGameData();
    await fillGameForm(page, originalGame);
    await submitGameForm(page);

    // Wait for redirect to detail page
    await expect(page).toHaveURL(/\/admin\/shared-games\/[a-f0-9-]{36}/);

    // Act: Click edit button
    const editButton = page.getByRole('button', { name: /modifica|edit/i }).or(page.getByRole('link', { name: /modifica|edit/i }));
    await editButton.click();

    // Wait for navigation to edit page (might be same URL or /edit route)
    await expect(page.getByLabel(/titolo/i)).toBeVisible();

    // Act: Modify fields
    const updatedTitle = `${originalGame.title} - Updated`;
    const updatedDescription = `${originalGame.description} - Modified for testing`;

    await page.getByLabel(/titolo/i).fill(updatedTitle);
    await page.getByLabel(/descrizione/i).fill(updatedDescription);

    // Act: Save changes
    const saveButton = page.getByRole('button', { name: /salva|save/i });
    await saveButton.click();

    // Assert: Verify redirect back to detail page
    await expect(page).toHaveURL(/\/admin\/shared-games\/[a-f0-9-]{36}/);

    // Assert: Verify changes persisted
    await expect(page.getByRole('heading', { name: updatedTitle })).toBeVisible();
    await expect(page.getByText(updatedDescription)).toBeVisible();
  });
});
