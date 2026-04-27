/**
 * E2E — Mechanic Extractor: load-existing-analysis flow (ADR-051 Sprint 2 / Task 31).
 *
 * Closes spec-panel gap #3 by exercising the three real entry points an admin
 * uses to land on a previously-generated MechanicAnalysis:
 *
 *  1. **Deep-link** — navigate directly to
 *     `/admin/.../analyses?analysisId=<uuid>` (gap #1 wiring).
 *  2. **Load-by-id input** — paste a UUID into the "Load existing analysis"
 *     fallback card and click Load.
 *  3. **Discovery list** — click a row in the recent-analyses table (gap #2).
 *
 * In all three cases we assert:
 *   - `GET /admin/mechanic-analyses/{id}/status` is called (analysis hydrates).
 *   - `GET /admin/mechanic-analyses/{id}/claims` is called.
 *   - `<ClaimsSection>` renders and the claim text is visible.
 *   - The URL ends up with `?analysisId=<uuid>` (URL <-> state two-way bind).
 *
 * Backend endpoints are fully mocked at the `page.context()` level so the spec
 * is hermetic and runs against the Next.js webServer without touching auth,
 * Postgres or the real .NET API. Auth is faked via {@link setupMockAuth}
 * which already installs a catch-all `/api/**` route — we register more
 * specific routes BEFORE navigating so they take precedence.
 *
 * Feature flag: requires `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED=true` at
 * server start (forwarded by `playwright.config.ts` webServer env). When off
 * the analyses route 404s via `notFound()` in the page component.
 */

import { test, expect, type Page, type Route } from '@playwright/test';

const ANALYSES_PATH = '/admin/knowledge-base/mechanic-extractor/analyses';

// Valid UUID v4 placeholders. Zod's `.uuid()` (used in the auth schema for
// the admin user id) rejects all-hex sequences like `cccccccc-cccc-cccc-cccc-cccccccccccc`
// because the third group must start with the version digit (1-8) and the
// fourth group must start with 8/9/a/b. Earlier iterations of this spec used
// memorable but invalid placeholders and silently failed `RequireRole` due to
// `CurrentUserResponseSchema` validation failure on the `auth/me` mock.
const ANALYSIS_ID = '11111111-1111-4111-8111-111111111111';
const SHARED_GAME_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const PDF_DOCUMENT_ID = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
const ADMIN_USER_ID = 'cccccccc-cccc-4ccc-8ccc-cccccccccccc';

const CLAIM_ID = 'dddddddd-dddd-4ddd-8ddd-dddddddddddd';
const CLAIM_TEXT =
  'Players score 1 point per completed road tile when the road is finished during the scoring phase.';

const STATUS_PUBLISHED = 2; // MechanicAnalysisStatus.Published
const SECTION_MECHANICS = 1; // MECHANIC_SECTION_LABELS[1] = 'Mechanics'
const RUN_STATUS_SUCCEEDED = 0;
const CLAIM_STATUS_PENDING = 0; // MechanicClaimStatus.Pending

/**
 * In-memory state holder so we can mutate the analysis between tests if
 * needed. The status mock returns whatever's currently in `state.current`.
 */
function buildStatusDto() {
  return {
    id: ANALYSIS_ID,
    sharedGameId: SHARED_GAME_ID,
    pdfDocumentId: PDF_DOCUMENT_ID,
    promptVersion: 'v1.0.0',
    status: STATUS_PUBLISHED,
    rejectionReason: null,
    createdBy: ADMIN_USER_ID,
    createdAt: '2026-04-25T10:00:00Z',
    reviewedBy: ADMIN_USER_ID,
    reviewedAt: '2026-04-25T11:00:00Z',
    provider: 'deepseek',
    modelUsed: 'deepseek-chat',
    totalTokensUsed: 4200,
    estimatedCostUsd: 0.42,
    costCapUsd: 1.0,
    costCapOverrideApplied: false,
    costCapOverrideAt: null,
    costCapOverrideBy: null,
    costCapOverrideReason: null,
    isSuppressed: false,
    suppressedAt: null,
    suppressedBy: null,
    suppressionReason: null,
    claimsCount: 1,
    sectionRuns: [
      {
        section: SECTION_MECHANICS,
        runOrder: 1,
        provider: 'deepseek',
        modelUsed: 'deepseek-chat',
        promptTokens: 2000,
        completionTokens: 2200,
        totalTokens: 4200,
        estimatedCostUsd: 0.42,
        latencyMs: 5400,
        status: RUN_STATUS_SUCCEEDED,
        errorMessage: null,
        startedAt: '2026-04-25T10:00:00Z',
        completedAt: '2026-04-25T10:00:05Z',
      },
    ],
  };
}

function buildListResponse() {
  return {
    items: [
      {
        id: ANALYSIS_ID,
        sharedGameId: SHARED_GAME_ID,
        gameTitle: 'Carcassonne',
        pdfDocumentId: PDF_DOCUMENT_ID,
        promptVersion: 'v1.0.0',
        status: STATUS_PUBLISHED,
        claimsCount: 1,
        totalTokensUsed: 4200,
        estimatedCostUsd: 0.42,
        certificationStatus: 1,
        isSuppressed: false,
        createdAt: '2026-04-25T10:00:00Z',
      },
    ],
    page: 1,
    pageSize: 20,
    totalCount: 1,
  };
}

function buildClaimsResponse() {
  return [
    {
      id: CLAIM_ID,
      analysisId: ANALYSIS_ID,
      section: SECTION_MECHANICS,
      text: CLAIM_TEXT,
      displayOrder: 0,
      status: CLAIM_STATUS_PENDING,
      reviewedBy: null,
      reviewedAt: null,
      rejectionNote: null,
      citations: [
        {
          id: 'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee',
          pdfPage: 4,
          quote: 'A completed road scores 1 point per tile during the scoring phase.',
          displayOrder: 0,
        },
      ],
    },
  ];
}

/**
 * Builds the auth/me payload an admin user needs to satisfy `RequireRole` in
 * `app/admin/(dashboard)/layout.tsx`. The schema mirrors `CurrentUserResponseSchema`
 * in `apps/web/src/lib/api/schemas/auth.schemas.ts:60`, which wraps the user
 * data in a `{ user: AuthUserSchema }` envelope. Required fields per
 * `AuthUserSchema`: id (uuid), email (email), role (min 1 char). Optional/
 * defaulted: displayName (nullable), onboardingCompleted (default false),
 * onboardingSkipped (default false). Extra props are stripped silently by Zod.
 */
function buildAuthMeDto() {
  return {
    user: {
      id: ADMIN_USER_ID,
      email: 'admin@meepleai.dev',
      role: 'Admin',
      displayName: 'Mock Admin',
      onboardingCompleted: true,
      onboardingSkipped: false,
    },
  };
}

/**
 * Registers context-level mocks for every endpoint the page (and its admin
 * layout's `RequireRole` guard) touches.
 *
 * IMPORTANT: routes use `**` globs (no host prefix) so they match BOTH:
 *  - direct hits to the backend at `http://localhost:8080/api/v1/...`
 *  - browser-side relative requests proxied through Next.js
 *    (`http://localhost:3000/api/v1/...`)
 *
 * The page's `httpClient` issues relative URLs in the browser → they go
 * through the Next.js proxy at `localhost:3000`, NOT to `localhost:8080`.
 * Absolute-URL mocks (against `API_BASE`) silently miss those requests, which
 * is why earlier iterations of this spec hung on the "Verifica autorizzazioni…"
 * spinner: `RequireRole` polled `/api/v1/auth/me` and never got a response.
 */
async function mockAnalysisRoutes(page: Page) {
  const calls = {
    authMe: 0,
    list: 0,
    status: 0,
    claims: 0,
  };

  // Auth — RequireRole in the admin (dashboard) layout calls this on mount.
  // Without a host-agnostic mock it 401s and the layout redirects to /login.
  // Regex (not glob) because Playwright's `**` glob anchored to scheme is
  // unreliable across mixed absolute/relative request URLs.
  await page.context().route(/\/api\/v1\/auth\/me(\?.*)?$/, async (route: Route) => {
    if (route.request().method() === 'GET') {
      calls.authMe += 1;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(buildAuthMeDto()),
      });
      return;
    }
    await route.continue();
  });

  // Session status — polled by the global session-watcher hook. Returns the
  // shape required by `SessionStatusResponseSchema` in
  // `apps/web/src/lib/api/schemas/auth.schemas.ts:91`. Without this mock the
  // endpoint 401s and triggers the auto-logout / redirect path, which can
  // unmount the admin shell mid-render.
  await page.context().route(/\/api\/v1\/auth\/session\/status(\?.*)?$/, async (route: Route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          expiresAt: '2026-12-31T23:59:59Z',
          lastSeenAt: '2026-04-27T12:00:00Z',
          remainingMinutes: 60,
        }),
      });
      return;
    }
    await route.continue();
  });

  // List endpoint (discovery card) — match with optional `?page=...&pageSize=...`
  // and only the bare collection path, NOT any `/{id}/...` subroutes.
  await page
    .context()
    .route(/\/api\/v1\/admin\/mechanic-analyses(\?[^/]*)?$/, async (route: Route) => {
      if (route.request().method() === 'GET') {
        calls.list += 1;
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(buildListResponse()),
        });
        return;
      }
      await route.continue();
    });

  // Status endpoint — polled by React Query every 2s while pipeline is
  // running. Once status=Published the page stops polling, so a single mock
  // fulfilment is fine (route stays installed, will serve repeats).
  await page
    .context()
    .route(
      new RegExp(`/api/v1/admin/mechanic-analyses/${ANALYSIS_ID}/status(\\?.*)?$`),
      async (route: Route) => {
        if (route.request().method() === 'GET') {
          calls.status += 1;
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(buildStatusDto()),
          });
          return;
        }
        await route.continue();
      }
    );

  // Claims endpoint — fetched by ClaimsSection once the analysis is loaded.
  await page
    .context()
    .route(
      new RegExp(`/api/v1/admin/mechanic-analyses/${ANALYSIS_ID}/claims(\\?.*)?$`),
      async (route: Route) => {
        if (route.request().method() === 'GET') {
          calls.claims += 1;
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(buildClaimsResponse()),
          });
          return;
        }
        await route.continue();
      }
    );

  return calls;
}

/**
 * Inject the session + role cookies the Next.js proxy middleware checks
 * before letting `/admin/**` render. Without these, the middleware redirects
 * to `/login` even when `PLAYWRIGHT_AUTH_BYPASS=true`, because the bypass
 * branch in `proxy.ts:334` requires a present `meepleai_session` cookie value
 * (it only skips backend validation, not the cookie-existence check).
 */
async function setAdminSessionCookies(page: Page) {
  await page.context().addCookies([
    {
      name: 'meepleai_session',
      value: 'mock-admin-session',
      domain: 'localhost',
      path: '/',
      httpOnly: true,
      secure: false,
      sameSite: 'Lax',
    },
    {
      name: 'meepleai_user_role',
      value: 'admin',
      domain: 'localhost',
      path: '/',
      httpOnly: false,
      secure: false,
      sameSite: 'Lax',
    },
  ]);
}

test.describe('Admin · Mechanic Extractor · Load existing analysis', () => {
  test('deep-link entry: ?analysisId=<uuid> hydrates status + claims', async ({ page }) => {
    await setAdminSessionCookies(page);
    const calls = await mockAnalysisRoutes(page);

    // Direct deep-link entry — gap #1: state initializes lazily from the URL.
    await page.goto(`${ANALYSES_PATH}?analysisId=${ANALYSIS_ID}`);

    // Visual assertions first — they auto-wait up to 5s, which lets React Query
    // fire the status + claims fetches (deferred past the initial paint via
    // `enabled: !!analysisId`) before we inspect the call counters. Ordering
    // synchronous `expect(calls.status)` BEFORE the visual wait races against
    // the React Query render cycle: `waitForLoadState('networkidle')` resolves
    // on a 500ms idle window after auth/me + session/status, which can occur
    // before the analysis-status fetch even kicks off.
    const claimsSection = page.getByTestId('claims-section');
    await expect(claimsSection).toBeVisible();

    const claimText = page.getByTestId(`claim-text-${CLAIM_ID}`);
    await expect(claimText).toBeVisible();
    await expect(claimText).toContainText(CLAIM_TEXT.substring(0, 40));

    // Now poll the counters — by this point the mocks have definitely fired,
    // but the increments may not yet be flushed to the closure if the
    // `route.fulfill()` microtask is still running.
    await expect.poll(() => calls.status, { timeout: 5000 }).toBeGreaterThanOrEqual(1);
    await expect.poll(() => calls.claims, { timeout: 5000 }).toBeGreaterThanOrEqual(1);

    // URL still carries the analysisId param after hydration (no rewrite).
    expect(page.url()).toContain(`analysisId=${ANALYSIS_ID}`);
  });

  test('Load-by-id input: typing a UUID and clicking Load updates URL + renders claims', async ({
    page,
  }) => {
    await setAdminSessionCookies(page);
    const calls = await mockAnalysisRoutes(page);

    // Land on the page with NO query param — the input is the entry point.
    await page.goto(ANALYSES_PATH);
    await page.waitForLoadState('networkidle');

    // No analysis selected yet → ClaimsSection should not be in the DOM.
    await expect(page.getByTestId('claims-section')).toHaveCount(0);

    // Paste UUID + click Load.
    const idInput = page.getByTestId('load-analysis-id-input');
    await idInput.fill(ANALYSIS_ID);
    await page.getByTestId('load-analysis-id-button').click();

    // ClaimsSection mounts first (visible auto-waits up to 5s, which gives
    // both the URL-sync effect and the React Query fetches time to settle).
    await expect(page.getByTestId('claims-section')).toBeVisible();
    await expect(page.getByTestId(`claim-text-${CLAIM_ID}`)).toContainText(
      CLAIM_TEXT.substring(0, 40)
    );

    // URL gains `?analysisId=<uuid>` via gap #1 sync effect.
    await expect.poll(() => page.url(), { timeout: 5000 }).toContain(`analysisId=${ANALYSIS_ID}`);

    await expect.poll(() => calls.status, { timeout: 5000 }).toBeGreaterThanOrEqual(1);
    await expect.poll(() => calls.claims, { timeout: 5000 }).toBeGreaterThanOrEqual(1);
  });

  test('Discovery list: clicking a row updates URL and loads the analysis', async ({ page }) => {
    await setAdminSessionCookies(page);
    const calls = await mockAnalysisRoutes(page);

    await page.goto(ANALYSES_PATH);
    await page.waitForLoadState('networkidle');

    // List card hits the index endpoint on mount.
    await expect.poll(() => calls.list).toBeGreaterThanOrEqual(1);

    // Click the row corresponding to our analysis (gap #2 wiring).
    const row = page.getByTestId(`mechanic-analyses-list-row-${ANALYSIS_ID}`);
    await expect(row).toBeVisible();
    await row.click();

    // Detail view (status + claims) materialises (visual auto-waits up to 5s).
    await expect(page.getByTestId('claims-section')).toBeVisible();
    await expect(page.getByTestId(`claim-text-${CLAIM_ID}`)).toContainText(
      CLAIM_TEXT.substring(0, 40)
    );

    // URL gains the analysisId query param (gap #1 sync).
    await expect.poll(() => page.url(), { timeout: 5000 }).toContain(`analysisId=${ANALYSIS_ID}`);

    await expect.poll(() => calls.status, { timeout: 5000 }).toBeGreaterThanOrEqual(1);
    await expect.poll(() => calls.claims, { timeout: 5000 }).toBeGreaterThanOrEqual(1);
  });
});
