import { test, expect } from '@playwright/test';

const API_BASE = process.env.SMOKE_API_BASE ?? 'http://localhost:8080';

test.describe('SMOKE — /shared-games/[id] real backend (public, no auth)', () => {
  test('GET /api/v1/shared-games/top-contributors returns array', async ({ request }) => {
    const res = await request.get(`${API_BASE}/api/v1/shared-games/top-contributors?limit=8`);
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body)).toBe(true);
  });

  test('frontend /shared-games/{seeded-id} renders hero or notFound (seed-aware)', async ({
    page,
  }) => {
    const SEEDED_ID = process.env.SMOKE_SHARED_GAME_ID ?? '';
    test.skip(!SEEDED_ID, 'SMOKE_SHARED_GAME_ID not seeded');
    await page.goto(`/shared-games/${SEEDED_ID}`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector(
      '[data-slot="shared-game-hero"], [data-slot="shared-game-not-found"]',
      {
        timeout: 30_000,
      }
    );
  });
});
