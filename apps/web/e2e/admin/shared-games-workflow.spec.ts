/**
 * ISSUE #2426: SharedGameCatalog Admin Workflow E2E Tests
 *
 * Tests complete game lifecycle: Create → Edit → Publish → Archive → Delete
 *
 * Prerequisites:
 * - Backend running: cd apps/api/src/Api && dotnet run
 * - Migration applied: dotnet ef database update
 * - Admin user exists (auto-seeded in dev)
 *
 * Run: pnpm test:e2e apps/web/e2e/admin/shared-games-workflow.spec.ts
 */

import { test, expect } from '@playwright/test';

test.describe('SharedGameCatalog Admin Workflow', () => {
  test.beforeEach(async ({ page }) => {
    // Login as admin (auto-seeded: admin@meepleai.dev / Admin123!)
    await page.goto('/login');
    await page.fill('input[type="email"]', 'admin@meepleai.dev');
    await page.fill('input[type="password"]', 'Admin123!');
    await page.click('button[type="submit"]');

    // Wait for redirect to dashboard
    await page.waitForURL('/board-game-ai');
    await expect(page.locator('text=Dashboard')).toBeVisible();
  });

  test('should complete full game lifecycle: Create → Edit → Publish → Archive → Delete', async ({
    page,
  }) => {
    const testGameTitle = `E2E Test Game ${Date.now()}`;

    // ============================================================
    // Step 1: Navigate to Shared Games Admin
    // ============================================================
    await page.goto('/admin/shared-games');
    await expect(page.locator('h1:has-text("Shared Games Catalog")')).toBeVisible();

    // ============================================================
    // Step 2: CREATE - Create new game
    // ============================================================
    await page.click('button:has-text("Nuovo Gioco")');

    // Wait for form
    await expect(page.locator('input[name="title"]')).toBeVisible();

    // Fill form
    await page.fill('input[name="title"]', testGameTitle);
    await page.fill('input[name="yearPublished"]', '2024');
    await page.fill('textarea[name="description"]', 'E2E test game for automated testing');
    await page.fill('input[name="minPlayers"]', '2');
    await page.fill('input[name="maxPlayers"]', '4');
    await page.fill('input[name="playingTimeMinutes"]', '60');
    await page.fill('input[name="minAge"]', '10');
    await page.fill('input[name="imageUrl"]', 'https://picsum.photos/400/300');
    await page.fill('input[name="thumbnailUrl"]', 'https://picsum.photos/200/150');

    // Submit
    await page.click('button[type="submit"]:has-text("Crea")');

    // Verify success toast
    await expect(page.locator('text=Gioco creato con successo')).toBeVisible();

    // Verify game appears in list with Draft status
    await expect(page.locator(`text=${testGameTitle}`)).toBeVisible();
    await expect(
      page.locator(`[data-testid="game-row"]:has-text("${testGameTitle}") >> text=Draft`)
    ).toBeVisible();

    // ============================================================
    // Step 3: EDIT - Update game details
    // ============================================================
    await page.click(
      `[data-testid="game-row"]:has-text("${testGameTitle}") >> button:has-text("Modifica")`
    );

    // Wait for edit form
    await expect(page.locator('input[name="title"]')).toHaveValue(testGameTitle);

    // Update description
    const updatedDescription = 'Updated description for E2E testing';
    await page.fill('textarea[name="description"]', updatedDescription);

    // Submit
    await page.click('button[type="submit"]:has-text("Salva")');

    // Verify success
    await expect(page.locator('text=Gioco aggiornato con successo')).toBeVisible();

    // ============================================================
    // Step 4: PUBLISH - Draft → Published
    // ============================================================
    await page.click(
      `[data-testid="game-row"]:has-text("${testGameTitle}") >> button[aria-label="Azioni"]`
    );
    await page.click('button:has-text("Pubblica")');

    // Confirm dialog
    await page.click('button:has-text("Conferma")');

    // Verify success
    await expect(page.locator('text=Gioco pubblicato con successo')).toBeVisible();

    // Verify status changed to Published
    await expect(
      page.locator(`[data-testid="game-row"]:has-text("${testGameTitle}") >> text=Published`)
    ).toBeVisible();

    // ============================================================
    // Step 5: ARCHIVE - Published → Archived
    // ============================================================
    await page.click(
      `[data-testid="game-row"]:has-text("${testGameTitle}") >> button[aria-label="Azioni"]`
    );
    await page.click('button:has-text("Archivia")');

    // Confirm dialog
    await page.click('button:has-text("Conferma")');

    // Verify success
    await expect(page.locator('text=Gioco archiviato con successo')).toBeVisible();

    // Verify status changed to Archived
    await expect(
      page.locator(`[data-testid="game-row"]:has-text("${testGameTitle}") >> text=Archived`)
    ).toBeVisible();

    // ============================================================
    // Step 6: REQUEST DELETE - Two-step workflow
    // ============================================================
    await page.click(
      `[data-testid="game-row"]:has-text("${testGameTitle}") >> button[aria-label="Azioni"]`
    );
    await page.click('button:has-text("Richiedi Eliminazione")');

    // Fill reason
    await page.fill('textarea[name="reason"]', 'E2E test cleanup');
    await page.click('button:has-text("Invia Richiesta")');

    // Verify success
    await expect(page.locator('text=Richiesta di eliminazione inviata')).toBeVisible();

    // ============================================================
    // Step 7: APPROVE DELETE - Admin approves deletion
    // ============================================================
    await page.goto('/admin/shared-games/pending-deletes');
    await expect(page.locator('h1:has-text("Richieste di Eliminazione")')).toBeVisible();

    // Find pending request for test game
    await expect(page.locator(`text=${testGameTitle}`)).toBeVisible();

    // Click approve
    await page.click(
      `[data-testid="delete-request"]:has-text("${testGameTitle}") >> button:has-text("Approva")`
    );

    // Confirm approval
    await page.click('button:has-text("Conferma Eliminazione")');

    // Verify success
    await expect(page.locator('text=Gioco eliminato con successo')).toBeVisible();

    // ============================================================
    // Step 8: VERIFY - Game no longer visible
    // ============================================================
    await page.goto('/admin/shared-games');

    // Game should not appear in list (soft deleted)
    await expect(page.locator(`text=${testGameTitle}`)).not.toBeVisible();

    // Verify can still search archived/deleted games with filter
    // (Optional - depends on UI implementation)
  });

  test('should allow adding FAQ to published game', async ({ page }) => {
    // Navigate to a published game (assumes at least one exists)
    await page.goto('/admin/shared-games');

    // Click first published game
    await page.click(
      '[data-testid="game-row"]:has-text("Published"):first >> button:has-text("Visualizza")'
    );

    // Navigate to FAQ tab
    await page.click('button[role="tab"]:has-text("FAQ")');

    // Add FAQ
    await page.click('button:has-text("Aggiungi FAQ")');
    await page.fill('input[name="question"]', 'Can I play with 5 players?');
    await page.fill(
      'textarea[name="answer"]',
      'Yes, with the expansion you can play up to 5 players.'
    );
    await page.click('button:has-text("Salva")');

    // Verify FAQ appears
    await expect(page.locator('text=Can I play with 5 players?')).toBeVisible();
  });

  test('should allow adding errata to published game', async ({ page }) => {
    await page.goto('/admin/shared-games');

    // Click first published game
    await page.click(
      '[data-testid="game-row"]:has-text("Published"):first >> button:has-text("Visualizza")'
    );

    // Navigate to Errata tab
    await page.click('button[role="tab"]:has-text("Errata")');

    // Add errata
    await page.click('button:has-text("Aggiungi Errata")');
    await page.fill('textarea[name="description"]', 'Card X should say "draw 2" not "draw 3"');
    await page.fill('input[name="pageReference"]', 'p. 15');
    await page.click('button:has-text("Salva")');

    // Verify errata appears
    await expect(page.locator('text=Card X should say "draw 2"')).toBeVisible();
  });
});
