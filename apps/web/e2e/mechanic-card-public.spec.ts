/**
 * E2E — public, login-gated mechanic card (ME-M1.6, Issue #528).
 *
 * `/games/[id]/card` is an `(authenticated)` route gated by proxy.ts. Under
 * PLAYWRIGHT_AUTH_BYPASS we must seed the session + role cookies
 * (`seedMockRoleCookies`) so the proxy resolves the user instead of 307→/login,
 * plus mock `/auth/me` + `/session/status` so AuthProvider doesn't hang on the
 * "Verifica autorizzazioni…" spinner (real backend is unreachable in this run).
 *
 * The card endpoint `GET /api/v1/games/{gameId}/card` is mocked via
 * `page.context().route(...)` (NOT `page.route` — project convention: the
 * context route matches with/without the query string and before redirects).
 *
 * Two scenarios:
 *   1. 200 → assert the section labels + a `[p.N]` citation badge render.
 *   2. 404 → assert the not-found UI (Next's notFound() boundary).
 */
import { test, expect, type Route } from '@playwright/test';

import { mockAuthEndpoints, seedMockRoleCookies } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

// Deterministic sharedGameId used as the route param.
const GAME_ID = '00000000-0000-4000-8000-000000000528';

const SAMPLE_CARD = {
  cardId: '11111111-1111-4111-8111-111111111111',
  sharedGameId: GAME_ID,
  title: 'Catan — Mechanics',
  version: 3,
  publishedAt: '2026-05-01T00:00:00Z',
  gameName: 'Catan',
  publisher: 'Kosmos',
  language: 'en',
  sourceAnalysisId: '99999999-9999-4999-8999-999999999999',
  publicationYear: 1995,
  documentName: 'Catan Rulebook',
  sections: [
    {
      section: 'Summary',
      claims: [
        {
          id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
          claim: 'Players collect resources to build settlements and cities.',
          citations: [
            {
              pdfId: 'ffffffff-ffff-4fff-8fff-ffffffffffff',
              pdfPage: 2,
              quote: 'gather resources',
            },
          ],
        },
      ],
    },
    {
      section: 'Faq',
      claims: [
        {
          id: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
          claim: 'You may only trade on your own turn.',
          citations: [
            {
              pdfId: 'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee',
              pdfPage: 12,
              quote: 'only on your own turn',
            },
          ],
        },
      ],
    },
  ],
};

async function seedAuth(page: import('@playwright/test').Page): Promise<void> {
  await seedMockRoleCookies(page, 'User');
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
}

test.describe('Public mechanic card — /games/[id]/card', () => {
  test('renders sections + citation badge for a published card (200)', async ({ page }) => {
    await seedAuth(page);

    await page.context().route(/\/api\/v1\/games\/[^/]+\/card(\?.*)?$/, async (route: Route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(SAMPLE_CARD),
      });
    });

    await page.goto(`/games/${GAME_ID}/card`, { waitUntil: 'domcontentloaded' });

    // Card root + title
    await expect(page.locator('[data-slot="mechanic-card"]')).toBeVisible();
    await expect(page.getByTestId('mechanic-card-title')).toHaveText('Catan');

    // Section labels present (Faq → FAQ)
    await expect(page.getByRole('heading', { name: 'Summary' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'FAQ' })).toBeVisible();

    // A claim from each section
    await expect(
      page.getByText('Players collect resources to build settlements and cities.')
    ).toBeVisible();

    // A [p.N] citation badge
    await expect(
      page.getByTestId('mechanic-card-citation-eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee-12')
    ).toHaveText('p.12');
  });

  test('renders the not-found UI when the card is gone (404)', async ({ page }) => {
    await seedAuth(page);

    await page.context().route(/\/api\/v1\/games\/[^/]+\/card(\?.*)?$/, async (route: Route) => {
      await route.fulfill({
        status: 404,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'NOT_FOUND' }),
      });
    });

    await page.goto(`/games/${GAME_ID}/card`, { waitUntil: 'domcontentloaded' });

    // The card content must NOT render; Next's notFound() boundary takes over.
    await expect(page.getByTestId('mechanic-card-title')).toHaveCount(0);
    // The default Next not-found surface shows a 404 signal.
    await expect(page.getByText(/not found|404|non trovat/i).first()).toBeVisible();
  });

  // ─── Per-claim feedback (ME-M3.1, #533) ───────────────────────────────────────
  test('👍 posts a positive vote and shows the thanks state; 👎 posts an error report', async ({
    page,
  }) => {
    await seedAuth(page);

    const CLAIM_UP = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
    const CLAIM_DOWN = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';

    await page.context().route(/\/api\/v1\/games\/[^/]+\/card(\?.*)?$/, async (route: Route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(SAMPLE_CARD),
      });
    });

    // Capture the feedback POST bodies. 201 = created.
    const feedbackBodies: unknown[] = [];
    await page
      .context()
      .route(/\/api\/v1\/mechanic-cards\/[^/]+\/feedback(\?.*)?$/, async (route: Route) => {
        feedbackBodies.push(route.request().postDataJSON());
        await route.fulfill({ status: 201, contentType: 'application/json', body: '{}' });
      });

    await page.goto(`/games/${GAME_ID}/card`, { waitUntil: 'domcontentloaded' });
    await expect(page.getByTestId('mechanic-card-title')).toHaveText('Catan');

    // 👍 on the first claim → immediate positive vote.
    await page.getByTestId(`mechanic-card-thumb-up-${CLAIM_UP}`).click();
    await expect(page.getByTestId(`mechanic-card-feedback-thanks-${CLAIM_UP}`)).toBeVisible();
    await expect(page.getByTestId(`mechanic-card-thumb-up-${CLAIM_UP}`)).toHaveAttribute(
      'aria-pressed',
      'true'
    );
    expect(feedbackBodies.at(-1)).toMatchObject({
      claimId: CLAIM_UP,
      isPositive: true,
      errorType: null,
    });

    // 👎 on the second claim → open the modal, fill it, submit.
    await page.getByTestId(`mechanic-card-thumb-down-${CLAIM_DOWN}`).click();
    await expect(page.getByTestId('mechanic-card-report-dialog')).toBeVisible();
    await page
      .getByTestId('mechanic-card-report-description')
      .fill('This contradicts the trading rules.');
    await page.getByTestId('mechanic-card-report-submit').click();

    // Modal closes + the claim shows the "report received" thanks state.
    await expect(page.getByTestId('mechanic-card-report-dialog')).toHaveCount(0);
    await expect(page.getByTestId(`mechanic-card-feedback-thanks-${CLAIM_DOWN}`)).toBeVisible();
    expect(feedbackBodies.at(-1)).toMatchObject({
      claimId: CLAIM_DOWN,
      isPositive: false,
      errorType: 'factual',
      description: 'This contradicts the trading rules.',
    });
  });
});
