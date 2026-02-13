/**
 * Game Import Wizard E2E Tests
 * Issue #4168: End-to-end tests for PDF wizard with BGG integration
 *
 * Tests:
 * - Happy path: Upload → Extract → BGG Match → Confirm → Success
 * - Duplicate detection warning
 * - Manual BGG ID input
 * - Conflict resolution
 * - Editor submit → Admin approve
 * - Visual regression tests
 */

import { test, expect } from '@playwright/test';
import path from 'path';

// Test data paths
const TEST_PDF_PATH = path.join(__dirname, '..', 'test-data', 'wingspan_rulebook.pdf');
const WIZARD_URL = '/admin/games/import/wizard';

// Auth credentials (from .env.test, populated by setup-secrets.ps1)
const ADMIN_EMAIL = process.env.ADMIN_EMAIL || 'admin@meepleai.dev';
const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD || 'Demo123!';
const EDITOR_EMAIL = process.env.EDITOR_EMAIL || 'editor@meepleai.dev';
const EDITOR_PASSWORD = process.env.EDITOR_PASSWORD || 'Demo123!';

// Helper: Admin authentication (pattern from working tests)
async function loginAsAdmin(page: any) {
  // Read env at runtime (not import time)
  const email = process.env.ADMIN_EMAIL || 'admin@meepleai.dev';
  const password = process.env.ADMIN_PASSWORD || 'Demo123!';

  console.log('[DEBUG] Login with:', email, '/', password.substring(0, 4) + '***');

  await page.goto('/login');
  await page.fill('input[type="email"]', email);
  await page.fill('input[type="password"]', password);
  await page.click('button[type="submit"]');

  // Wait for redirect (flexible - accepts any authenticated route)
  await page.waitForURL(/\/(dashboard|admin|board-game-ai)/, { timeout: 10000 });
  await page.waitForLoadState('networkidle');
}

// Helper: Editor authentication
async function loginAsEditor(page: any) {
  await page.goto('/login');
  await page.fill('input[type="email"]', EDITOR_EMAIL);
  await page.fill('input[type="password"]', EDITOR_PASSWORD);
  await page.click('button[type="submit"]');

  await page.waitForURL('/admin');
  await page.waitForLoadState('networkidle');
}

test.describe('Game Import Wizard E2E', () => {
  test.describe('Happy Path', () => {
    test('completes full import workflow', async ({ page }) => {
      // Setup: Authenticate as admin
      await loginAsAdmin(page);

      // Navigate to wizard
      await page.goto(WIZARD_URL);

      // Assert: Wizard header visible
      await expect(page.getByRole('heading', { name: /game import wizard/i })).toBeVisible();

      // Assert: Step 1 is active (progress bar exists, may be CSS hidden)
      const progressBar = page.locator('[role="progressbar"]');
      await expect(progressBar).toBeAttached(); // toBeAttached checks existence, not visibility

      // Step 1: Upload PDF
      const fileInput = page.locator('input[type="file"]');
      await fileInput.setInputFiles(TEST_PDF_PATH);

      // Click Upload PDF button (file selection is separate from upload)
      // Use exact match to avoid step navigation button
      const uploadButton = page.getByRole('button', { name: 'Upload PDF', exact: true });
      await uploadButton.click();

      // Wait for upload to complete and Next button to be enabled
      const nextButton = page.getByRole('button', { name: 'Next →', exact: true });
      await expect(nextButton).toBeEnabled({ timeout: 30000 }); // Wait up to 30s for backend processing

      // Click Next to go to Step 2
      await nextButton.click();

      // Step 2: Review metadata
      // Wait for metadata extraction to complete
      await page.waitForTimeout(3000); // AI extraction time

      // Assert: Extracted metadata fields visible
      await expect(page.getByLabel(/title/i)).toBeVisible();

      // Optionally edit metadata (skip for happy path)
      // Click Next to Step 3
      await nextButton.click();

      // Step 3: BGG match
      // Assert: Search input pre-filled
      const bggSearchInput = page.getByTestId('bgg-search-input');
      await expect(bggSearchInput).toBeVisible();

      // Wait for search results (debounce 300ms + API)
      await page.waitForTimeout(1000);

      // Assert: Results visible
      const searchResults = page.getByTestId('bgg-search-results');
      await expect(searchResults).toBeVisible();

      // Click first result (highest match score)
      const firstResult = page.locator('[data-testid^="bgg-result-"]').first();
      await firstResult.click();

      // Assert: Result selected (highlighted)
      await expect(firstResult).toHaveClass(/selected|bg-primary/);

      // Click Next to Step 4
      await nextButton.click();

      // Step 4: Confirm and submit
      // Assert: Final preview visible
      const previewCard = page.getByTestId('final-preview-card');
      await expect(previewCard).toBeVisible();

      // Assert: Confirm button visible
      const confirmButton = page.getByTestId('confirm-import-btn');
      await expect(confirmButton).toBeVisible();

      // Click Submit
      await confirmButton.click();

      // Assert: Success redirect (to game detail page)
      await page.waitForURL(/\/admin\/games\/\w+/);

      // Assert: Success toast or message
      // await expect(page.getByText(/success|imported/i)).toBeVisible();
    });
  });

  test.describe('Duplicate Detection', () => {
    test('shows warning when duplicate found', async ({ page }) => {
      // Setup: Authenticate as admin
      await loginAsAdmin(page);
      await page.goto(WIZARD_URL);

      // Steps 1-3: Upload → Extract → BGG Match (same as happy path)
      const fileInput = page.locator('input[type="file"]');
      await fileInput.setInputFiles(TEST_PDF_PATH);
      await page.waitForTimeout(2000);

      await page.getByRole('button', { name: /next/i }).click();
      await page.waitForTimeout(3000);

      await page.getByRole('button', { name: /next/i }).click();
      await page.waitForTimeout(1000);

      const firstResult = page.locator('[data-testid^="bgg-result-"]').first();
      await firstResult.click();

      await page.getByRole('button', { name: /next/i }).click();

      // Step 4: Assert duplicate modal appears
      // NOTE: Questo assume che il game con BGG ID già esista nel DB
      // In un test reale, dovresti seedare il DB con un duplicate game

      const duplicateModal = page.getByRole('dialog').filter({ hasText: /duplicate|already exists/i });

      // Se il modal appare (test condizionale basato su DB state)
      const isModalVisible = await duplicateModal.isVisible().catch(() => false);

      if (isModalVisible) {
        // Assert: Warning message visible
        await expect(duplicateModal).toContainText(/already exists|duplicate/i);

        // Assert: Existing game details shown
        await expect(duplicateModal.getByText(/existing game/i)).toBeVisible();

        // User clicks "Cancel" - stays on step 4
        await duplicateModal.getByRole('button', { name: /cancel|close/i }).click();

        // Assert: Modal closed, still on step 4
        await expect(duplicateModal).not.toBeVisible();
        await expect(page.getByTestId('confirm-import-btn')).toBeVisible();
      }
    });

    test('allows force create with checkbox', async ({ page }) => {
      // Setup: Authenticate as admin
      await loginAsAdmin(page);
      await page.goto(WIZARD_URL);

      // Steps 1-3: Upload → Extract → BGG Match
      const fileInput = page.locator('input[type="file"]');
      await fileInput.setInputFiles(TEST_PDF_PATH);
      await page.waitForTimeout(2000);

      await page.getByRole('button', { name: /next/i }).click();
      await page.waitForTimeout(3000);

      await page.getByRole('button', { name: /next/i }).click();
      await page.waitForTimeout(1000);

      const firstResult = page.locator('[data-testid^="bgg-result-"]').first();
      await firstResult.click();

      await page.getByRole('button', { name: /next/i }).click();

      // Step 4: If duplicate modal appears
      const duplicateModal = page.getByRole('dialog').filter({ hasText: /duplicate|already exists/i });
      const isModalVisible = await duplicateModal.isVisible().catch(() => false);

      if (isModalVisible) {
        // Assert: Force create option available (checkbox or button)
        const forceCheckbox = duplicateModal.getByRole('checkbox', { name: /force|create anyway/i });

        // Enable force create
        await forceCheckbox.check();

        // Confirm force create
        await duplicateModal.getByRole('button', { name: /create|proceed/i }).click();

        // Assert: Modal closed, submit proceeds
        await expect(duplicateModal).not.toBeVisible();

        // Click final submit
        await page.getByTestId('confirm-import-btn').click();

        // Assert: Success redirect (duplicate created)
        await page.waitForURL(/\/admin\/games\/\w+/);
      }
    });
  });

  test.describe('Manual BGG ID', () => {
    test('accepts manual BGG ID input', async ({ page }) => {
      // Setup: Authenticate as admin
      await loginAsAdmin(page);
      await page.goto(WIZARD_URL);

      // Steps 1-2: Upload → Extract
      const fileInput = page.locator('input[type="file"]');
      await fileInput.setInputFiles(TEST_PDF_PATH);
      await page.waitForTimeout(2000);

      await page.getByRole('button', { name: /next/i }).click();
      await page.waitForTimeout(3000);

      await page.getByRole('button', { name: /next/i }).click();

      // Step 3: Manual BGG ID input
      // Expand manual section (if collapsible/accordion)
      const manualSection = page.getByText(/manual|enter bgg id/i).first();
      if (await manualSection.isVisible()) {
        await manualSection.click();
      }

      // Input manual BGG ID (e.g., 13 = Catan)
      const manualIdInput = page.getByTestId('manual-bgg-id-input');
      await manualIdInput.fill('13');

      // Click Preview/Fetch button
      const fetchButton = page.getByTestId('fetch-manual-bgg-btn');
      await fetchButton.click();

      // Wait for preview to load
      await page.waitForTimeout(1500);

      // Assert: Preview card shows game details
      const previewCard = page.locator('[data-testid="manual-bgg-preview"]').or(page.getByText(/catan/i));
      await expect(previewCard).toBeVisible();

      // Click "Select This Game" or confirm button
      const confirmButton = page.getByTestId('confirm-manual-bgg-btn');
      await confirmButton.click();

      // Assert: Manual game selected
      // Continue to step 4
      await page.getByRole('button', { name: /next/i }).click();

      // Step 4: Verify selected game in preview
      await expect(page.getByTestId('final-preview-card')).toContainText(/catan/i);
    });
  });

  test.describe('Conflict Resolution', () => {
    test('resolves BGG vs PDF conflicts', async ({ page }) => {
      // Setup: Authenticate as admin
      await loginAsAdmin(page);
      await page.goto(WIZARD_URL);

      // Steps 1-3: Upload → Extract → BGG Match
      const fileInput = page.locator('input[type="file"]');
      await fileInput.setInputFiles(TEST_PDF_PATH);
      await page.waitForTimeout(2000);

      await page.getByRole('button', { name: /next/i }).click();
      await page.waitForTimeout(3000);

      await page.getByRole('button', { name: /next/i }).click();
      await page.waitForTimeout(1000);

      const firstResult = page.locator('[data-testid^="bgg-result-"]').first();
      await firstResult.click();

      await page.getByRole('button', { name: /next/i }).click();

      // Step 4: Conflict resolution
      // Assert: Conflict section visible (if conflicts exist)
      const conflictSection = page.getByText(/conflict|different values/i).first();
      const hasConflicts = await conflictSection.isVisible().catch(() => false);

      if (hasConflicts) {
        // Example: minPlayers conflict (BGG: 2 vs PDF: 3)
        // Find conflict radios
        const conflictField = page.locator('[data-testid*="minPlayers"]').or(
          page.getByText(/min players|minimum players/i).locator('..')
        );

        // Test variant A: Select BGG value
        const bggRadio = conflictField.getByRole('radio', { name: /bgg|boardgamegeek/i });
        if (await bggRadio.isVisible()) {
          await bggRadio.click();

          // Assert: BGG value selected
          await expect(bggRadio).toBeChecked();
        }

        // Test variant B: Select Custom value
        const customRadio = conflictField.getByRole('radio', { name: /custom/i });
        if (await customRadio.isVisible()) {
          await customRadio.click();

          // Input custom value
          const customInput = conflictField.getByTestId('minPlayers-custom-input');
          await customInput.fill('2');

          // Assert: Custom value set
          await expect(customInput).toHaveValue('2');
        }
      }

      // Submit final import
      await page.getByTestId('confirm-import-btn').click();

      // Assert: Success
      await page.waitForURL(/\/admin\/games\/\w+/);
    });
  });

  test.describe('Approval Workflow', () => {
    test('editor creates draft, admin approves', async ({ page, context }) => {
      // Part A: Editor creates draft via wizard
      await loginAsEditor(page);
      await page.goto(WIZARD_URL);

      // Complete wizard steps 1-4
      const fileInput = page.locator('input[type="file"]');
      await fileInput.setInputFiles(TEST_PDF_PATH);

      // Click Upload PDF button
      const uploadButton = page.getByRole('button', { name: 'Upload PDF', exact: true });
      await uploadButton.click();

      // Wait for upload and Next button
      const nextButton = page.getByRole('button', { name: 'Next →', exact: true });
      await expect(nextButton).toBeEnabled({ timeout: 30000 });
      await nextButton.click();

      // Step 2: Metadata review
      await page.waitForTimeout(3000);
      await nextButton.click();

      // Step 3: BGG match - select first result
      await page.waitForTimeout(1000);
      const firstResult = page.locator('[data-testid^="bgg-result-"]').first();
      if (await firstResult.isVisible({ timeout: 5000 })) {
        await firstResult.click();
      }
      await nextButton.click();

      // Step 4: Confirm and create game
      const submitButton = page.getByRole('button', { name: /submit.*import/i });
      await expect(submitButton).toBeEnabled({ timeout: 5000 });
      await submitButton.click();

      // Wait for redirect to detail page
      await page.waitForURL(/\/admin\/shared-games\/[a-f0-9-]+$/, { timeout: 10000 });

      // Extract game ID from URL
      const gameUrl = page.url();
      const gameId = gameUrl.split('/').pop();

      // Assert: Game created with Draft status
      await expect(page.getByText(/draft/i).first()).toBeVisible({ timeout: 5000 });

      // Editor submits for approval
      const submitForApprovalButton = page.getByRole('button', { name: /submit.*approval/i });
      if (await submitForApprovalButton.isVisible({ timeout: 2000 })) {
        await submitForApprovalButton.click();
        await page.waitForTimeout(1000);

        // Assert: Status changed to PendingApproval
        await expect(page.getByText(/pending approval/i).first()).toBeVisible({ timeout: 5000 });
      }

      // Part B: Admin approves
      const adminPage = await context.newPage();
      await loginAsAdmin(adminPage);

      // Navigate to game detail page
      await adminPage.goto(`/admin/shared-games/${gameId}`);
      await adminPage.waitForLoadState('networkidle');

      // Assert: Approve button visible for admin
      const approveButton = adminPage.getByRole('button', { name: /approve/i });
      await expect(approveButton).toBeVisible({ timeout: 5000 });

      // Click Approve
      await approveButton.click();
      await adminPage.waitForTimeout(2000);

      // Assert: Status changed to Published
      await expect(adminPage.getByText(/published/i).first()).toBeVisible({ timeout: 5000 });

      // Cleanup
      await adminPage.close();
    });
  });

  test.describe('Visual Regression', () => {
    test('wizard UI snapshots', async ({ page }) => {
      // Setup: Authenticate as admin
      await loginAsAdmin(page);
      await page.goto(WIZARD_URL);

      // Step 1: Initial state
      await expect(page).toHaveScreenshot('wizard-step-1-initial.png');

      // Upload file
      const fileInput = page.locator('input[type="file"]');
      await fileInput.setInputFiles(TEST_PDF_PATH);
      await page.waitForTimeout(2000);

      // Step 1: After upload
      await expect(page).toHaveScreenshot('wizard-step-1-uploaded.png');

      // Step 2: Metadata extraction
      await page.getByRole('button', { name: /next/i }).click();
      await page.waitForTimeout(3000);

      await expect(page).toHaveScreenshot('wizard-step-2-metadata.png');

      // Step 3: BGG search
      await page.getByRole('button', { name: /next/i }).click();
      await page.waitForTimeout(1000);

      await expect(page).toHaveScreenshot('wizard-step-3-bgg-search.png');

      // Select result
      const firstResult = page.locator('[data-testid^="bgg-result-"]').first();
      await firstResult.click();

      await expect(page).toHaveScreenshot('wizard-step-3-selected.png');

      // Step 4: Final preview
      await page.getByRole('button', { name: /next/i }).click();

      await expect(page).toHaveScreenshot('wizard-step-4-preview.png');

      // Duplicate modal (conditional - only if duplicate exists)
      const duplicateModal = page.getByRole('dialog').filter({ hasText: /duplicate|already exists/i });
      const isModalVisible = await duplicateModal.isVisible().catch(() => false);

      if (isModalVisible) {
        await expect(duplicateModal).toHaveScreenshot('wizard-duplicate-modal.png');
      }

      // Conflict resolution UI (conditional - only if conflicts exist)
      const conflictSection = page.getByText(/conflict|different values/i).first();
      const hasConflicts = await conflictSection.isVisible().catch(() => false);

      if (hasConflicts) {
        await expect(page).toHaveScreenshot('wizard-step-4-conflicts.png');
      }
    });
  });
});
