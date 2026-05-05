import { test, expect } from '@playwright/test';

const API_BASE = process.env.SMOKE_API_BASE ?? 'http://localhost:8080';

test.describe('SMOKE — /invites/[token] real backend (public, no auth)', () => {
  test('GET /api/v1/game-nights/invitations/{invalid-token} returns 404', async ({ request }) => {
    const res = await request.get(`${API_BASE}/api/v1/game-nights/invitations/invalid-token-12345`);
    expect(res.status()).toBe(404);
  });

  test('frontend /invites/invalid-token-12345 renders not-found shell', async ({ page }) => {
    await page.goto('/invites/invalid-token-12345', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-slot="invite-not-found"], [data-slot="invite-expired"]', {
      timeout: 30_000,
    });
  });
});
