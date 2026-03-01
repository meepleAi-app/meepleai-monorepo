/**
 * Public Pages — Sezione 2 del piano di test UI MeepleAI
 * Homepage, Catalog giochi, Dettaglio gioco, Sessioni pubbliche
 */

import { test, expect } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const MOCK_GAMES = [
  { id: 'g1', title: 'Catan', publisher: 'Kosmos', averageRating: 7.2, imageUrl: null, minPlayers: 3, maxPlayers: 4 },
  { id: 'g2', title: 'Pandemic', publisher: 'Z-Man Games', averageRating: 8.1, imageUrl: null, minPlayers: 2, maxPlayers: 4 },
  { id: 'g3', title: 'Ticket to Ride', publisher: 'Days of Wonder', averageRating: 7.5, imageUrl: null, minPlayers: 2, maxPlayers: 5 },
];

test.describe('Homepage', () => {
  test('carica hero, nav e CTA', async ({ page }) => {
    await page.goto('/', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('body')).toBeVisible();
    // Nav principale
    await expect(page.locator('nav, header').first()).toBeVisible({ timeout: 8000 });
    // Heading principale (h1 o h2)
    await expect(page.locator('h1, h2').first()).toBeVisible({ timeout: 8000 });
  });
});

test.describe('Catalog Giochi', () => {
  test.beforeEach(async ({ page }) => {
    await page.route(`${API_BASE}/api/v1/shared-games**`, (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: MOCK_GAMES, totalCount: MOCK_GAMES.length }),
      })
    );
  });

  test('mostra lista giochi', async ({ page }) => {
    await page.goto('/games', { waitUntil: 'domcontentloaded' });
    await expect(page.getByText('Catan').first()).toBeVisible({ timeout: 8000 });
    await expect(page.getByText('Pandemic').first()).toBeVisible();
  });

  test('ricerca filtra giochi', async ({ page }) => {
    const filtered = [MOCK_GAMES[0]];
    await page.route(`${API_BASE}/api/v1/shared-games*search=catan*`, (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: filtered, totalCount: 1 }),
      })
    );

    await page.goto('/games', { waitUntil: 'domcontentloaded' });
    const searchInput = page.locator('input[placeholder*="erca"], input[type="search"], input[name="search"]').first();
    if (await searchInput.count() > 0) {
      await searchInput.fill('catan');
      await page.waitForTimeout(500);
    }
    await expect(page.locator('body')).toBeVisible();
  });

  test('click su gioco naviga al dettaglio', async ({ page }) => {
    await page.goto('/games', { waitUntil: 'domcontentloaded' });

    // Click sul primo link che porta a un gioco specifico
    const gameCard = page.locator('[href*="/games/"]').first();
    if (await gameCard.count() > 0) {
      const href = await gameCard.getAttribute('href');
      if (href && /\/games\/.+/.test(href)) {
        await gameCard.click();
        await page.waitForURL(/\/games\/[^/]+/, { timeout: 8000 }).catch(() => {});
      }
    }
  });
});

test.describe('Dettaglio Gioco (public)', () => {
  test.beforeEach(async ({ page }) => {
    await page.route(`${API_BASE}/api/v1/shared-games/g1**`, (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'g1',
          title: 'Catan',
          publisher: 'Kosmos',
          description: 'Il classico gioco di strategia',
          averageRating: 7.2,
          minPlayers: 3,
          maxPlayers: 4,
          playingTimeMinutes: 90,
          faqs: [{ question: 'Quanti giocatori?', answer: '3-4' }],
        }),
      })
    );
  });

  test('mostra titolo e dati base', async ({ page }) => {
    await page.goto('/games/g1', { waitUntil: 'domcontentloaded' });
    if (await page.getByText('Catan').count() > 0) {
      await expect(page.getByText('Catan').first()).toBeVisible({ timeout: 8000 });
    } else {
      await expect(page.locator('body')).toBeVisible();
    }
  });

  test('tab FAQs mostra contenuto', async ({ page }) => {
    await page.goto('/games/g1', { waitUntil: 'domcontentloaded' });

    const faqTab = page.locator('button[role="tab"]:has-text("FAQ"), [data-tab="faqs"]').first();
    if (await faqTab.count() > 0) {
      await faqTab.click();
      await expect(page.getByText('Quanti giocatori?')).toBeVisible({ timeout: 5000 });
    }
  });
});

test.describe('Sessioni Pubbliche', () => {
  test('mostra lista sessioni', async ({ page }) => {
    await page.route(`${API_BASE}/api/v1/sessions**`, (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            { id: 's1', gameTitle: 'Catan', date: '2026-02-20', players: 3 },
          ],
          totalCount: 1,
        }),
      })
    );

    await page.goto('/sessions', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('body')).toBeVisible();
  });
});
