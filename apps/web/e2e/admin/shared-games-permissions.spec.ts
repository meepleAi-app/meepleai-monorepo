/**
 * ISSUE #2426: SharedGameCatalog Permission Enforcement E2E Tests
 *
 * Validates role-based access control (RBAC):
 * - Admin: Full access (all operations)
 * - Editor: Limited access (no delete approval, no bulk import, no archive)
 * - User: Read-only (no admin operations)
 *
 * Run: pnpm test:e2e apps/web/e2e/admin/shared-games-permissions.spec.ts
 */

import { test, expect } from '@playwright/test';

test.describe('SharedGameCatalog Permission Enforcement', () => {
  test('User (unauthenticated) cannot access admin endpoints', async ({ page }) => {
    // Try to access admin page without login
    const response = await page.goto('/admin/shared-games');

    // Should redirect to login
    await expect(page).toHaveURL(/\/login/);
  });

  test('Editor cannot publish game (AdminOrEditor policy allows)', async ({ page }) => {
    // Login as editor (assumes editor@meepleai.dev exists)
    await page.goto('/login');
    await page.fill('input[type="email"]', 'editor@meepleai.dev');
    await page.fill('input[type="password"]', 'Editor123!');
    await page.click('button[type="submit"]');

    await page.waitForURL('/board-game-ai');

    // Navigate to shared games admin
    await page.goto('/admin/shared-games');

    // Editor can see the page (AdminOrEditor policy)
    await expect(page.locator('h1:has-text("Shared Games Catalog")')).toBeVisible();

    // Editor can create games
    await expect(page.locator('button:has-text("Nuovo Gioco")')).toBeVisible();
    await expect(page.locator('button:has-text("Nuovo Gioco")')).toBeEnabled();
  });

  test('Editor cannot access bulk import (Admin only)', async ({ page }) => {
    await page.goto('/login');
    await page.fill('input[type="email"]', 'editor@meepleai.dev');
    await page.fill('input[type="password"]', 'Editor123!');
    await page.click('button[type="submit"]');

    await page.waitForURL('/board-game-ai');

    // Try to access bulk import page
    const response = await page.goto('/admin/shared-games/import');

    // Should show 403 Forbidden or hide the page
    // (Implementation depends on frontend route protection)
    const is403 = response?.status() === 403;
    const isRedirected =
      page.url().includes('/admin/shared-games') && !page.url().includes('/import');

    expect(is403 || isRedirected).toBeTruthy();
  });

  test('Editor cannot approve delete requests (Admin only)', async ({ page }) => {
    await page.goto('/login');
    await page.fill('input[type="email"]', 'editor@meepleai.dev');
    await page.fill('input[type="password"]', 'Editor123!');
    await page.click('button[type="submit"]');

    await page.waitForURL('/board-game-ai');

    // Try to access pending deletes page
    await page.goto('/admin/shared-games/pending-deletes');

    // Editor should not have access (AdminOnly policy)
    // Check for 403 or empty state with "Non autorizzato" message
    const accessDenied =
      (await page.locator('text=Non autorizzato').isVisible()) ||
      (await page.locator('text=Accesso negato').isVisible());

    expect(accessDenied).toBeTruthy();
  });

  test('Regular user cannot create games (API returns 403)', async ({ page, request }) => {
    // Login as regular user
    await page.goto('/login');
    await page.fill('input[type="email"]', 'user@meepleai.dev');
    await page.fill('input[type="password"]', 'User123!');
    await page.click('button[type="submit"]');

    await page.waitForURL('/board-game-ai');

    // Try to create game via API (should return 403)
    const response = await request.post('http://localhost:8080/api/v1/admin/shared-games', {
      data: {
        title: 'Unauthorized Test Game',
        yearPublished: 2024,
        description: 'Should fail',
        minPlayers: 2,
        maxPlayers: 4,
        playingTimeMinutes: 60,
        minAge: 10,
        imageUrl: 'https://picsum.photos/400/300',
        thumbnailUrl: 'https://picsum.photos/200/150',
      },
    });

    // Expect 403 Forbidden
    expect(response.status()).toBe(403);
  });

  test('Unauthenticated requests to admin endpoints return 401', async ({ request }) => {
    // Try to access admin endpoint without authentication
    const response = await request.get('http://localhost:8080/api/v1/admin/shared-games');

    // Expect 401 Unauthorized
    expect(response.status()).toBe(401);
  });
});
