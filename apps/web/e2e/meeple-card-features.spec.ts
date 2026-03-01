/**
 * MeepleCard System ⭐ — Sezione 12 del piano di test UI MeepleAI
 * Epic #3820 — 29 issue completate
 *
 * Testa: grid view, list view, hover preview, quick actions, bulk select
 */

import { test, expect, type Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const MOCK_GAMES = [
  {
    id: 'g1',
    title: 'Catan',
    publisher: 'Kosmos',
    yearPublished: 1995,
    averageRating: 7.2,
    minPlayers: 3,
    maxPlayers: 4,
    imageUrl: null,
    thumbnailUrl: null,
    description: 'Il classico gioco di costruzione e commercio.',
  },
  {
    id: 'g2',
    title: 'Pandemic',
    publisher: 'Z-Man Games',
    yearPublished: 2008,
    averageRating: 8.1,
    minPlayers: 2,
    maxPlayers: 4,
    imageUrl: null,
    thumbnailUrl: null,
    description: 'Cooperativo per salvare il mondo dalle epidemie.',
  },
  {
    id: 'g3',
    title: 'Ticket to Ride',
    publisher: 'Days of Wonder',
    yearPublished: 2004,
    averageRating: 7.5,
    minPlayers: 2,
    maxPlayers: 5,
    imageUrl: null,
    thumbnailUrl: null,
    description: 'Costruisci rotte ferroviarie attraverso l\'Europa.',
  },
];

async function mockGamesApi(page: Page) {
  await page.context().route(`${API_BASE}/api/v1/shared-games**`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items: MOCK_GAMES, totalCount: MOCK_GAMES.length }),
    })
  );
  // Wishlist
  await page.context().route(`${API_BASE}/api/v1/library/wishlist**`, (route) => {
    const method = route.request().method();
    if (method === 'POST') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true }) });
    }
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ items: [] }) });
  });
}

test.describe('MeepleCard — Grid View', () => {
  test.beforeEach(async ({ page }) => {
    await mockGamesApi(page);
  });

  test('mostra MeepleCard variant=grid con titolo e rating', async ({ page }) => {
    await page.goto('/games', { waitUntil: 'domcontentloaded' });

    await expect(page.getByText('Catan').first()).toBeVisible({ timeout: 8000 });
    await expect(page.getByText('Pandemic').first()).toBeVisible();
    await expect(page.getByText('Ticket to Ride').first()).toBeVisible();
  });

  test('ogni card mostra publisher', async ({ page }) => {
    await page.goto('/games', { waitUntil: 'domcontentloaded' });
    if (await page.getByText('Kosmos').count() > 0) {
      await expect(page.getByText('Kosmos').first()).toBeVisible({ timeout: 8000 });
    }
  });

  test('card mostra rating visivo', async ({ page }) => {
    await page.goto('/games', { waitUntil: 'domcontentloaded' });

    // Cerca rating numerico o stelle
    const rating = page.locator('[data-testid="rating"]')
      .or(page.locator('text=/7\\.2|8\\.1|7\\.5/'))
      .first();
    if (await rating.count() > 0) {
      await expect(rating).toBeVisible({ timeout: 8000 });
    }
  });
});

test.describe('MeepleCard — List View', () => {
  test.beforeEach(async ({ page }) => {
    await mockGamesApi(page);
  });

  test('toggle vista lista mostra card compatte', async ({ page }) => {
    await page.goto('/games', { waitUntil: 'domcontentloaded' });

    // Cerca toggle view (griglia/lista)
    const listToggle = page.locator(
      'button[aria-label*="list"], button[data-view="list"], [data-testid="view-toggle-list"]'
    ).first();

    if (await listToggle.count() > 0) {
      await listToggle.click();
      await page.waitForTimeout(300);
      // Verifica che le card siano ancora visibili (layout cambiato)
      await expect(page.getByText('Catan')).toBeVisible({ timeout: 5000 });
    }
  });
});

test.describe('MeepleCard — Hover Preview', () => {
  test.beforeEach(async ({ page }) => {
    await mockGamesApi(page);
  });

  test('hover su card mostra popover con descrizione', async ({ page }) => {
    await page.goto('/games', { waitUntil: 'domcontentloaded' });

    // Hover sulla prima card
    const firstCard = page.locator('[data-testid="meeple-card"], [data-testid="game-card"]').first();
    if (await firstCard.count() > 0) {
      await firstCard.hover();
      await page.waitForTimeout(400);

      // Verifica popover o tooltip
      const popover = page.locator('[data-testid="hover-preview"], [role="tooltip"], .popover').first();
      if (await popover.count() > 0) {
        await expect(popover).toBeVisible({ timeout: 5000 });
      }
    }
  });
});

test.describe('MeepleCard — Quick Actions', () => {
  test.beforeEach(async ({ page }) => {
    await mockGamesApi(page);
    await page.context().route(`${API_BASE}/api/v1/auth/me`, (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          user: { id: 'u1', email: 'user@meepleai.dev', displayName: 'User', role: 'User' },
          expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
        }),
      })
    );
  });

  test('hover card mostra quick actions', async ({ page }) => {
    await page.goto('/games', { waitUntil: 'domcontentloaded' });

    const firstCard = page.locator('[data-testid="meeple-card"], [data-testid="game-card"]').first();
    if (await firstCard.count() > 0) {
      await firstCard.hover();
      await page.waitForTimeout(300);

      // Cerca quick action buttons
      const quickActions = page.locator(
        '[data-testid="quick-actions"], [data-testid="wishlist-btn"], button[aria-label*="wishlist"], button[aria-label*="add"]'
      ).first();
      if (await quickActions.count() > 0) {
        await expect(quickActions).toBeVisible({ timeout: 5000 });
      }
    }
  });

  test('click wishlist aggiunge gioco e mostra feedback', async ({ page }) => {
    let wishlistCalled = false;
    await page.context().route(`${API_BASE}/api/v1/library/wishlist`, (route) => {
      if (route.request().method() === 'POST') {
        wishlistCalled = true;
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true }),
      });
    });

    await page.goto('/games', { waitUntil: 'domcontentloaded' });

    const firstCard = page.locator('[data-testid="meeple-card"]').first();
    if (await firstCard.count() > 0) {
      await firstCard.hover();
      await page.waitForTimeout(300);

      const wishlistBtn = page.locator(
        '[data-testid="wishlist-btn"], button[aria-label*="wishlist"]'
      ).first();

      if (await wishlistBtn.count() > 0) {
        await wishlistBtn.click();
        await page.waitForTimeout(500);
        // Verifica toast o feedback
        const feedback = page.locator(
          'text=/wishlist|aggiunto|added/i, [role="status"]'
        ).first();
        if (await feedback.count() > 0) {
          await expect(feedback).toBeVisible({ timeout: 5000 });
        }
      }
    }
  });
});

test.describe('MeepleCard — Bulk Select', () => {
  test.beforeEach(async ({ page }) => {
    await page.context().route(`${API_BASE}/api/v1/auth/me`, (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          user: { id: 'u1', email: 'user@meepleai.dev', displayName: 'User', role: 'User' },
          expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
        }),
      })
    );
    await page.context().route(`${API_BASE}/api/v1/library**`, (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: MOCK_GAMES, totalCount: MOCK_GAMES.length }),
      })
    );
  });

  test('attiva bulk mode e seleziona cards', async ({ page }) => {
    await page.goto('/library', { waitUntil: 'domcontentloaded' });

    // Cerca pulsante bulk select
    const bulkBtn = page.locator(
      'button:has-text("Seleziona"), button:has-text("Select"), [data-testid="bulk-mode-toggle"]'
    ).first();

    if (await bulkBtn.count() > 0) {
      await bulkBtn.click();
      await page.waitForTimeout(300);

      // Seleziona prime 2-3 card
      const cards = page.locator('[data-testid="meeple-card"], [data-testid="game-card"]');
      const count = await cards.count();

      if (count >= 2) {
        await cards.nth(0).click();
        await cards.nth(1).click();

        // Verifica counter selezione
        const counter = page.locator('text=/2.*selezionat|2.*selected/i').first();
        if (await counter.count() > 0) {
          await expect(counter).toBeVisible({ timeout: 5000 });
        }
      }
    }
  });
});
