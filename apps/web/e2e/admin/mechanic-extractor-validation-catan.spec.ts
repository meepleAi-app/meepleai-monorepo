/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 1 / Task 41)
 *
 * Smoke E2E for the Catan validation journey:
 *   golden review page → Evaluate (compute metrics) → dashboard
 *
 * Asserts the data-testid landmarks shipped earlier in this sprint:
 *   - golden-claims-list      (GoldenClaimsList.tsx)
 *   - golden-version-hash     (GoldenVersionHashBadge.tsx)
 *   - evaluate-metrics-button (EvaluateButton.tsx)
 *   - mechanic-metrics-card   (MetricsCard.tsx)
 *   - dashboard-table-row     (DashboardTable.tsx)
 *
 * The validation surface is gated by the build-time feature flag
 * `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED`. When the flag is not enabled
 * in the dev webServer, the page renders a Next.js notFound() — in that
 * case this smoke test skips at runtime so the file remains green during
 * `playwright test --list` (Task 42 will wire the flag in CI).
 *
 * Mocking-only: follows the convention established by sibling admin specs
 * (admin-overview, admin-feature-flags) — `page.context().route()` for
 * `/api/v1/...`, no real backend required.
 */

import { test, expect, type Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Stable identifiers used to drive the mocked routes.
const CATAN_SHARED_GAME_ID = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1';
const CATAN_PDF_DOCUMENT_ID = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2';
const CATAN_ANALYSIS_ID = 'cccccccc-cccc-cccc-cccc-ccccccccccc3';
const CATAN_METRICS_ID = 'dddddddd-dddd-dddd-dddd-ddddddddddd4';
const GOLDEN_CLAIM_ID = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee5';
const GOLDEN_VERSION_HASH = 'cafef00dc0ffeebeef0123456789abcdef012345';

const ADMIN_BASE = `${API_BASE}/api/v1/admin/mechanic-extractor`;

const MOCK_GOLDEN_FOR_GAME = {
  sharedGameId: CATAN_SHARED_GAME_ID,
  versionHash: GOLDEN_VERSION_HASH,
  claims: [
    {
      id: GOLDEN_CLAIM_ID,
      sharedGameId: CATAN_SHARED_GAME_ID,
      section: 'Mechanics',
      statement: 'Players collect resources by rolling dice matching adjacent hex numbers.',
      expectedPage: 4,
      sourceQuote: 'Each player whose settlement borders that number receives one resource.',
      bggTags: ['Dice Rolling', 'Trading'],
      isActive: true,
      createdAt: new Date('2026-04-20T10:00:00Z').toISOString(),
      updatedAt: new Date('2026-04-20T10:00:00Z').toISOString(),
      createdByUserId: 'admin-1',
      updatedByUserId: null,
    },
  ],
} as const;

const MOCK_LATEST_METRICS = [
  {
    id: CATAN_METRICS_ID,
    mechanicAnalysisId: CATAN_ANALYSIS_ID,
    sharedGameId: CATAN_SHARED_GAME_ID,
    goldenVersionHash: GOLDEN_VERSION_HASH,
    coveragePct: 0.92,
    pageAccuracyPct: 0.88,
    bggMatchPct: 0.81,
    overallScore: 0.87,
    certificationStatus: 'Certified',
    computedAt: new Date('2026-04-21T12:00:00Z').toISOString(),
  },
];

const MOCK_DASHBOARD_ROWS = [
  {
    sharedGameId: CATAN_SHARED_GAME_ID,
    name: 'Catan',
    status: 'Certified',
    overallScore: 0.87,
    lastComputedAt: new Date('2026-04-21T12:00:00Z').toISOString(),
  },
];

const MOCK_CALCULATE_METRICS_RESPONSE = {
  metricsId: CATAN_METRICS_ID,
  certificationStatus: 'Certified',
  overallScore: 0.87,
  goldenVersionHash: GOLDEN_VERSION_HASH,
  computedAt: new Date('2026-04-21T12:00:00Z').toISOString(),
};

const MOCK_DRAFT = {
  mechanicAnalysisId: CATAN_ANALYSIS_ID,
  sharedGameId: CATAN_SHARED_GAME_ID,
  pdfDocumentId: CATAN_PDF_DOCUMENT_ID,
  gameTitle: 'Catan',
  status: 'Published',
  summaryDraft: 'Catan is a resource-management board game for 3-4 players.',
  mechanicsDraft: JSON.stringify(['Dice Rolling', 'Trading', 'Network Building']),
  victoryDraft: JSON.stringify({
    primary: 'Be the first to reach 10 victory points.',
    isPointBased: true,
    targetPoints: 10,
  }),
  resourcesDraft: JSON.stringify([
    { name: 'Wood', type: 'Card', usage: 'Build roads/settlements', isLimited: false },
  ]),
  phasesDraft: JSON.stringify([
    { order: 1, name: 'Roll', description: 'Roll dice to produce resources.' },
  ]),
  questionsDraft: JSON.stringify(['Can I trade with the bank at any time?']),
  totalTokensUsed: 4321,
  estimatedCostUsd: 0.0123,
};

const MOCK_SHARED_GAME = {
  id: CATAN_SHARED_GAME_ID,
  bggId: 13,
  title: 'Catan',
  publisher: 'KOSMOS',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  averageRating: 7.1,
  imageUrl: null,
  description: 'A classic German-style board game.',
  status: 'Published',
};

async function mockAdminAuth(page: Page) {
  await page.context().route(`${API_BASE}/api/v1/auth/me`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'admin-1',
          email: 'admin@meepleai.dev',
          displayName: 'Admin',
          role: 'Admin',
        },
        expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      }),
    })
  );
}

async function mockMechanicValidationApi(page: Page) {
  // Golden set for Catan
  await page.context().route(`${ADMIN_BASE}/golden/${CATAN_SHARED_GAME_ID}`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_GOLDEN_FOR_GAME),
    })
  );

  // Trend (latest metrics) — `?take=1` query param tolerated by glob `**`
  await page.context().route(`${ADMIN_BASE}/dashboard/${CATAN_SHARED_GAME_ID}/trend**`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_LATEST_METRICS),
    })
  );

  // Compute metrics (Evaluate button POST)
  await page.context().route(`${ADMIN_BASE}/analyses/${CATAN_ANALYSIS_ID}/metrics`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_CALCULATE_METRICS_RESPONSE),
    })
  );

  // Validation dashboard (per-game rows)
  await page.context().route(`${ADMIN_BASE}/dashboard`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_DASHBOARD_ROWS),
    })
  );

  // SharedGame metadata used by the golden page header
  await page.context().route(`${API_BASE}/api/v1/shared-games/${CATAN_SHARED_GAME_ID}`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_SHARED_GAME),
    })
  );

  // Mechanic draft (review page)
  await page.context().route(`${API_BASE}/api/v1/admin/mechanic-extractor/draft**`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_DRAFT),
    })
  );
}

/**
 * Skip the test gracefully if the Mechanic Validation feature flag is not
 * enabled in the dev webServer build. Task 42 will wire the flag in CI.
 */
async function skipIfFeatureFlagDisabled(page: Page, url: string) {
  const response = await page.goto(url, { waitUntil: 'domcontentloaded' });
  const status = response?.status() ?? 0;
  if (status === 404) {
    test.skip(true, 'NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED is not set in this build');
  }
}

test.describe('ME-VAL — Catan AI Comprehension smoke', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);
    await mockMechanicValidationApi(page);
  });

  test('walks golden review → evaluate → dashboard for Catan', async ({ page }) => {
    // 1) Navigate to the golden review page for Catan.
    await skipIfFeatureFlagDisabled(
      page,
      `/admin/knowledge-base/mechanic-extractor/golden/${CATAN_SHARED_GAME_ID}`
    );

    const goldenList = page.locator('[data-testid="golden-claims-list"]').first();
    await expect(goldenList).toBeVisible({ timeout: 15_000 });

    // The mocked golden set has 1 active claim — the list should not render
    // the "no claims yet" empty state, so we look for the section heading
    // emitted by the populated branch.
    await expect(goldenList.getByRole('heading', { name: /Mechanics/i })).toBeVisible({
      timeout: 10_000,
    });

    const versionHash = page.locator('[data-testid="golden-version-hash"]').first();
    await expect(versionHash).toBeVisible({ timeout: 10_000 });
    const hashText = (await versionHash.innerText()).trim();
    expect(hashText.length).toBeGreaterThan(0);
    expect(hashText).not.toBe('—');

    // 2) Trigger Evaluate on the review page (ValidationSection is feature-flagged
    //    inside the review page and only mounts when sharedGameId is in the URL).
    await page.goto(
      `/admin/knowledge-base/mechanic-extractor/review?sharedGameId=${CATAN_SHARED_GAME_ID}&pdfDocumentId=${CATAN_PDF_DOCUMENT_ID}`,
      { waitUntil: 'domcontentloaded' }
    );

    const evaluateButton = page.locator('[data-testid="evaluate-metrics-button"]').first();
    await expect(evaluateButton).toBeVisible({ timeout: 15_000 });
    await expect(evaluateButton).toBeEnabled({ timeout: 5_000 });
    await evaluateButton.click();

    const metricsCard = page.locator('[data-testid="mechanic-metrics-card"]').first();
    await expect(metricsCard).toBeVisible({ timeout: 15_000 });
    const metricsText = (await metricsCard.innerText()).trim();
    expect(metricsText.length).toBeGreaterThan(0);
    // The card surface owns the certification badge + score pills, so any
    // non-empty body proves the panel rendered with snapshot data.
    await expect(
      metricsCard.locator('[data-testid="metrics-certification-badge"]').first()
    ).toBeVisible({ timeout: 5_000 });

    // 3) Navigate to the validation dashboard and assert a Catan row.
    await page.goto('/admin/knowledge-base/mechanic-extractor/dashboard', {
      waitUntil: 'domcontentloaded',
    });

    const dashboardRow = page
      .locator('[data-testid="dashboard-table-row"]', { hasText: 'Catan' })
      .first();
    await expect(dashboardRow).toBeVisible({ timeout: 15_000 });
  });
});
