/**
 * E2E — Admin cover-gap → cover-editor deep-link (Issue #3611).
 *
 * Regression coverage for the fix described in #3611: the cover-gap admin view's
 * "Assegna cover" CTA used to point at `/shared-games?highlight=<id>`, a query
 * param nobody read — clicking it landed on the full catalog grid with no way to
 * actually edit the cover. The fix changed the CTA to `/shared-games/{id}?cover=edit`,
 * which the public detail page (`page-client.tsx`) reads via `useSearchParams()` to
 * open `AdminCoverEditAffordance`'s dialog on mount (`defaultOpen`), then strips the
 * param on close (`onDialogClose` → `router.replace`) so a back/refresh doesn't
 * silently reopen it.
 *
 * This flow crosses `page-client.tsx`, which this project treats as too
 * dependency-heavy for a meaningful Vitest test (see
 * `docs/for-claude/...shared-games-page-client-test-fragile` lesson) — an E2E is
 * the right level.
 *
 * ---
 * SSR caveat (why this file starts its own tiny HTTP stub in `beforeAll`):
 *
 * `/shared-games/[id]/page.tsx` is an async **Server Component** — its
 * `getSharedGameDetail()` call runs inside the Next.js dev-server process itself,
 * using an *absolute* URL (`getApiBase()` resolves server-side to `API_BASE_URL` /
 * `NEXT_PUBLIC_API_BASE`, falling back to `http://localhost:8080`). That fetch never
 * touches the browser's network stack, so Playwright's `page.route()` /
 * `page.context().route()` — which intercept only requests the *browser* issues —
 * cannot mock it. Verified empirically: without anything listening on :8080, a full
 * navigation to `/shared-games/{id}` renders the route's `error.tsx` boundary
 * (`data-state="error"`), not the real page.
 *
 * Every OTHER call in this flow (`/api/v1/auth/me`, the cover-gap list, the
 * cover-candidates picker fetch, and any client-side re-fetch of the detail) is a
 * normal browser-side fetch and IS mocked via `page.context().route()`, per the
 * project's usual convention (see `mechanic-card-public.spec.ts`). Only the one
 * SSR-only call is served by the local stub below — it is not "the backend": no
 * .NET, no DB, just two static JSON responses so the Server Component's fetch
 * resolves instead of ECONNREFUSED-ing.
 */
import { createServer, type Server } from 'node:http';

import { test, expect, type Route } from '@playwright/test';

import { mockAuthEndpoints, seedMockRoleCookies } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

// Deterministic UUIDv4-shaped id (encodes #3611 in the last group), mirrors the
// convention used by `visual-test-fixture.ts`'s VISUAL_TEST_FIXTURE_ID.
const GAME_ID = 'a0000000-0000-4000-8000-000000003611';
const GAME_TITLE = 'Root';

// Matches `getApiBase()`'s server-side fallback when neither `API_BASE_URL` nor
// `NEXT_PUBLIC_API_BASE` is set (true for this worktree — no `.env.local`).
const SSR_STUB_PORT = 8080;

const SHARED_GAME_DETAIL = {
  id: GAME_ID,
  bggId: null,
  title: GAME_TITLE,
  yearPublished: 2018,
  description: 'Fixture game with no cover, used to exercise the #3611 deep-link.',
  minPlayers: 2,
  maxPlayers: 4,
  playingTimeMinutes: 60,
  minAge: 10,
  complexityRating: null,
  averageRating: null,
  imageUrl: '',
  thumbnailUrl: '',
  coverUrl: null,
  rules: null,
  status: 'Published',
  createdBy: 'b0000000-0000-4000-8000-000000000001',
  modifiedBy: null,
  createdAt: '2026-01-01T00:00:00.000Z',
  modifiedAt: null,
  faqs: [],
  erratas: [],
  designers: [],
  publishers: [],
  categories: [],
  mechanics: [],
  toolkits: [],
  agents: [],
  kbs: [],
  toolkitsCount: 0,
  agentsCount: 0,
  kbsCount: 0,
  contributorsCount: 0,
  hasKnowledgeBase: false,
  isTopRated: false,
  isNew: false,
  translations: [],
  coverLicense: null,
  coverAttribution: null,
  coverSourceUrl: null,
};

const COVER_GAP_ROW = {
  gameId: GAME_ID,
  title: GAME_TITLE,
  bggId: null,
  cause: 'no_source',
  pdfFileName: null,
  pdfSizeBytes: null,
  errorCategory: null,
};

let ssrStub: Server | null = null;

test.beforeAll(async () => {
  ssrStub = createServer((req, res) => {
    const url = req.url ?? '';
    res.setHeader('Content-Type', 'application/json');

    if (url.startsWith('/api/v1/shared-games/top-contributors')) {
      res.writeHead(200);
      res.end('[]');
      return;
    }
    if (url === `/api/v1/shared-games/${GAME_ID}`) {
      res.writeHead(200);
      res.end(JSON.stringify(SHARED_GAME_DETAIL));
      return;
    }
    res.writeHead(404);
    res.end(JSON.stringify({ error: 'not-found' }));
  });

  await new Promise<void>((resolve, reject) => {
    ssrStub?.once('error', reject);
    ssrStub?.listen(SSR_STUB_PORT, () => resolve());
  });
});

test.afterAll(async () => {
  await new Promise<void>(resolve => {
    if (!ssrStub) {
      resolve();
      return;
    }
    ssrStub.close(() => resolve());
  });
});

async function mockCoverGapList(page: import('@playwright/test').Page): Promise<void> {
  await page
    .context()
    .route(/\/api\/v1\/admin\/shared-games\/cover-gap(\?.*)?$/, async (route: Route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [COVER_GAP_ROW], total: 1, page: 1, pageSize: 100 }),
      });
    });
}

async function mockCoverCandidates(page: import('@playwright/test').Page): Promise<void> {
  await page
    .context()
    .route(
      /\/api\/v1\/admin\/shared-games\/[^/]+\/cover-candidates(\?.*)?$/,
      async (route: Route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            gameId: GAME_ID,
            candidates: [],
            assignments: { card: null, hero: null, social: null },
          }),
        });
      }
    );
}

/** Defensive client-side mocks for the detail + top-contributors endpoints — covers
 * any post-hydration re-fetch (staleTime is 60s so none is expected, but the app
 * still exercises the relative `/api/v1/shared-games/*` proxy path client-side). */
async function mockSharedGameDetailClientSide(
  page: import('@playwright/test').Page
): Promise<void> {
  await page
    .context()
    .route(new RegExp(`/api/v1/shared-games/${GAME_ID}(\\?.*)?$`), async (route: Route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(SHARED_GAME_DETAIL),
      });
    });
  await page
    .context()
    .route(/\/api\/v1\/shared-games\/top-contributors(\?.*)?$/, async (route: Route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
    });
}

test.describe('Admin cover-gap → cover-editor deep-link (#3611)', () => {
  // Serial: all 4 tests share the single SSR stub server started in `beforeAll`
  // (one `http.createServer` bound to :8080 for the whole file). Running this
  // file's tests across multiple parallel workers would start that listener
  // more than once and EADDRINUSE.
  test.describe.configure({ mode: 'serial' });

  test('1. the cover-gap row exposes an "Assegna cover" link to /shared-games/{id}?cover=edit', async ({
    page,
  }) => {
    await seedMockRoleCookies(page, 'Admin');
    await seedCookieConsent(page);
    await mockAuthEndpoints(page, { role: 'admin' });
    await mockCoverGapList(page);

    await page.goto('/admin/shared-games/cover-gap', { waitUntil: 'domcontentloaded' });

    const link = page.getByRole('link', { name: /assegna cover/i });
    await expect(link).toBeVisible();
    await expect(link).toHaveAttribute('href', `/shared-games/${GAME_ID}?cover=edit`);
  });

  test('2. clicking it lands on the game page with the cover dialog already open', async ({
    page,
  }) => {
    await seedMockRoleCookies(page, 'Admin');
    await seedCookieConsent(page);
    await mockAuthEndpoints(page, { role: 'admin' });
    await mockCoverGapList(page);
    await mockCoverCandidates(page);
    await mockSharedGameDetailClientSide(page);

    await page.goto('/admin/shared-games/cover-gap', { waitUntil: 'domcontentloaded' });

    // Single click, no hover, no extra interaction — the dialog must already be
    // open on arrival (defaultOpen), not require the hover-revealed pencil icon.
    await page.getByRole('link', { name: /assegna cover/i }).click();

    await expect(page).toHaveURL(new RegExp(`/shared-games/${GAME_ID}\\?cover=edit$`));
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    // Exact match: "Copertina — Root" (dialog title). A loose /copertina/i regex
    // also matches the "Aggiungi copertina da URL" manual-upload section heading.
    await expect(dialog.getByRole('heading', { name: `Copertina — ${GAME_TITLE}` })).toBeVisible();
  });

  test('3. closing the dialog strips ?cover=edit from the URL', async ({ page }) => {
    await seedMockRoleCookies(page, 'Admin');
    await seedCookieConsent(page);
    await mockAuthEndpoints(page, { role: 'admin' });
    await mockCoverGapList(page);
    await mockCoverCandidates(page);
    await mockSharedGameDetailClientSide(page);

    await page.goto('/admin/shared-games/cover-gap', { waitUntil: 'domcontentloaded' });
    await page.getByRole('link', { name: /assegna cover/i }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(page).toHaveURL(new RegExp(`\\?cover=edit$`));

    await page.getByRole('button', { name: 'Close dialog' }).click();

    await expect(dialog).toBeHidden();
    // Exact match (no query string) — a stray `?cover=edit` left in the URL is
    // exactly the bug this assertion guards against (stale deep-link surviving
    // close, re-opening the dialog on back/refresh).
    await expect(page).toHaveURL(`http://localhost:3000/shared-games/${GAME_ID}`);
  });

  test('4. a non-admin visiting the deep-link directly sees neither the dialog nor the edit affordance', async ({
    page,
  }) => {
    await seedMockRoleCookies(page, 'User');
    await seedCookieConsent(page);
    await mockAuthEndpoints(page, { role: 'user' });
    await mockSharedGameDetailClientSide(page);

    await page.goto(`/shared-games/${GAME_ID}?cover=edit`, { waitUntil: 'domcontentloaded' });

    // The page itself must render for real (not an error/not-found boundary) so
    // this is a genuine check of the role gate, not an accidental early-out.
    await expect(page.getByRole('heading', { name: GAME_TITLE, level: 1 })).toBeVisible();

    await expect(page.getByRole('dialog')).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Modifica sorgente copertina' })).toHaveCount(0);
  });
});
