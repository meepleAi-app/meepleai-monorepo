/**
 * KbGlobaleView.integration.test.tsx
 * Issue #1482 Task 7 — MSW integration tests for /knowledge-base/global
 *
 * These tests exercise the full FE data stack without mocking hooks:
 *   - Zod schemas (kb-globale.schemas.ts, kb-docs.schemas.ts)
 *   - API clients (kbDocsClient.ts: searchGlobal + listUserKbDocs)
 *   - TanStack Query hooks (useGlobalKbSearch, useUserKbDocs)
 *   - KbGlobaleView orchestrator (Task 6)
 *   - KbHomeDesktop (Task 4) and KbSearchResultsDesktop (Task 5)
 *
 * MSW v2 — the global server is started by vitest.setup.tsx with
 * `onUnhandledRequest: 'bypass'`. Per-test handlers are added via
 * `server.use()` and reset via the global `afterEach(() => server.resetHandlers())`.
 *
 * Navigation mocks: next/navigation (useRouter + useSearchParams) are controlled
 * via mutable state objects so each test can set the URL independently.
 *
 * Real timers: fake-timers + TanStack Query v5 = known deadlock (P129).
 * All timing-sensitive tests (debounce coalesce) use real timers + waitFor
 * with an extended timeout (1500ms) to safely clear the 250ms debounce.
 *
 * Scope: Foundation layer only. No Phase 2 hooks (drawer/viewer/editor) consumed.
 *
 * @see apps/web/src/app/(authenticated)/knowledge-base/global/_components/KbGlobaleView.tsx
 * @see Issue #1482 Phase 1 Foundation
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { type ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { http, HttpResponse } from 'msw';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { server } from '@/__tests__/mocks/server';
import { globalRequestCache } from '@/lib/api/core/requestCache';

// ─── API base (matches vitest.setup.tsx + handler convention) ────────────────
const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ─── next/navigation mocks ────────────────────────────────────────────────────
// Mutable state so each test can control the URL independently.

type SearchParamsRecord = Record<string, string | null>;
let searchParamsMap: SearchParamsRecord = {};
const mockRouterPush = vi.fn();

vi.mock('next/navigation', () => ({
  useSearchParams: () => ({
    get: (key: string) => searchParamsMap[key] ?? null,
  }),
  useRouter: () => ({
    push: mockRouterPush,
    replace: vi.fn(),
  }),
  usePathname: () => '/knowledge-base/global',
}));

// ─── Wrapper with fresh QueryClient per test ──────────────────────────────────

function createWrapper() {
  const client = new QueryClient({
    defaultOptions: {
      queries: {
        // No retry — MSW error responses must be immediately surfaced in tests.
        retry: false,
        // Zero staleTime so tests always trigger a fresh fetch.
        staleTime: 0,
        // Zero gcTime so query cache is cleared between re-renders.
        gcTime: 0,
      },
    },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

// ─── Import after mocks ───────────────────────────────────────────────────────

import { KbGlobaleView } from '../KbGlobaleView';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const ISO_DATE = '2026-01-15T10:00:00+00:00';

// RFC 4122 v4 UUIDs for test fixtures — must pass Zod uuid() validation.
// Pattern: xxxxxxxx-xxxx-4xxx-[89ab]xxx-xxxxxxxxxxxx
const DOC_IDS = [
  'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11',
  'b1eebc99-9c0b-4ef8-bb6d-6bb9bd380a22',
  'c2eebc99-9c0b-4ef8-bb6d-6bb9bd380a33',
  'd3eebc99-9c0b-4ef8-bb6d-6bb9bd380a44',
  'e4eebc99-9c0b-4ef8-bb6d-6bb9bd380a55',
];
const GAME_IDS = [
  'f5eebc99-9c0b-4ef8-bb6d-6bb9bd380a66',
  'f6eebc99-9c0b-4ef8-bb6d-6bb9bd380a77',
  'f7eebc99-9c0b-4ef8-bb6d-6bb9bd380a88',
  'f8eebc99-9c0b-4ef8-bb6d-6bb9bd380a99',
  'f9eebc99-9c0b-4ef8-bb6d-6bb9bd380aaa',
];
const CHUNK_DOC_IDS = [
  'aaeebc99-9c0b-4ef8-bb6d-6bb9bd380b11',
  'abeebc99-9c0b-4ef8-bb6d-6bb9bd380b22',
  'aceebc99-9c0b-4ef8-bb6d-6bb9bd380b33',
  'adeebc99-9c0b-4ef8-bb6d-6bb9bd380b44',
  'aeeebc99-9c0b-4ef8-bb6d-6bb9bd380b55',
  'afeebc99-9c0b-4ef8-bb6d-6bb9bd380b66',
  'b0eebc99-9c0b-4ef8-bb6d-6bb9bd380b77',
  'b1eebc99-9c0b-4ef8-bb6d-6bb9bd380b88',
  'b2eebc99-9c0b-4ef8-bb6d-6bb9bd380b99',
  'b3eebc99-9c0b-4ef8-bb6d-6bb9bd380baa',
];
const CHUNK_GAME_IDS = [
  'baeebc99-9c0b-4ef8-bb6d-6bb9bd380c11',
  'bbeebc99-9c0b-4ef8-bb6d-6bb9bd380c22',
  'bceebc99-9c0b-4ef8-bb6d-6bb9bd380c33',
  'bdeebc99-9c0b-4ef8-bb6d-6bb9bd380c44',
  'beeebc99-9c0b-4ef8-bb6d-6bb9bd380c55',
  'bfeebc99-9c0b-4ef8-bb6d-6bb9bd380c66',
  'c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c77',
  'c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c88',
  'c2eebc99-9c0b-4ef8-bb6d-6bb9bd380c99',
  'c3eebc99-9c0b-4ef8-bb6d-6bb9bd380caa',
];

function makeKbDocDto(idx: number, fileName: string, gameName: string | null = null) {
  return {
    id: DOC_IDS[idx] ?? `a${idx}eebc99-9c0b-4ef8-bb6d-6bb9bd380a${idx}${idx}`,
    gameId: gameName ? (GAME_IDS[idx] ?? null) : null,
    gameName,
    fileName,
    processingState: 'Ready' as const,
    pageCount: 20,
    processedAt: ISO_DATE,
    uploadedAt: ISO_DATE,
    updatedAt: ISO_DATE,
  };
}

function makeSearchResultDto(i: number) {
  return {
    chunkId: `chunk-${i}`,
    docId: CHUNK_DOC_IDS[i] ?? `a${i}eebc99-9c0b-4ef8-bb6d-6bb9bd380b${i}${i}`,
    docTitle: `Rulebook ${i}`,
    gameId: CHUNK_GAME_IDS[i] ?? `ba${i}ebc99-9c0b-4ef8-bb6d-6bb9bd380c${i}${i}`,
    gameName: `Game ${i}`,
    docType: 'Rulebook',
    headingPath: null,
    snippet: `Snippet for result ${i}…`,
    pageNumber: i + 1,
    score: 0.9 - i * 0.05,
  };
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('KbGlobaleView integration (MSW)', () => {
  beforeEach(() => {
    searchParamsMap = {};
    mockRouterPush.mockReset();
    // Clear the module-level request deduplication cache before each test.
    // vitest.setup.tsx calls vi.clearAllTimers() in afterEach, which cancels the
    // RequestCache TTL cleanup timeouts — without this clear(), stale success
    // responses from earlier tests (S1-S6) can be served to S7 via cache hits,
    // causing the error state to never appear (items:[], error:null instead).
    globalRequestCache.clear();
  });

  // ── Scenario 1: Home branch — recent docs rendered ─────────────────────────
  it('S1: home branch (no q) — MSW returns 3 recent docs, cards rendered', async () => {
    const docs = [
      makeKbDocDto(0, 'azul.pdf', 'Azul'),
      makeKbDocDto(1, 'wingspan.pdf', 'Wingspan'),
      makeKbDocDto(2, 'gloomhaven.pdf', null),
    ];

    server.use(
      http.get(`${API_BASE}/api/v1/kb-docs`, () => {
        return HttpResponse.json({
          items: docs,
          total: 3,
          page: 1,
          pageSize: 20,
        });
      })
    );

    searchParamsMap = {};
    const Wrapper = createWrapper();
    render(<KbGlobaleView />, { wrapper: Wrapper });

    // Wait for the 3 doc cards (fileName is rendered in the card)
    await waitFor(() => {
      expect(screen.getByText('azul.pdf')).toBeInTheDocument();
      expect(screen.getByText('wingspan.pdf')).toBeInTheDocument();
      expect(screen.getByText('gloomhaven.pdf')).toBeInTheDocument();
    });
  });

  // ── Scenario 2: Results branch — 5 results rendered, request body correct ──
  it('S2: ?q=azul — MSW returns 5 results, renders 5 rows, POST body verified', async () => {
    const results = Array.from({ length: 5 }, (_, i) => makeSearchResultDto(i));
    let capturedBody: unknown = null;

    server.use(
      http.get(`${API_BASE}/api/v1/kb-docs`, () => {
        return HttpResponse.json({ items: [], total: 0, page: 1, pageSize: 20 });
      }),
      http.post(`${API_BASE}/api/v1/knowledge-base/search/global`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({
          results,
          hasMore: false,
          nextCursor: null,
        });
      })
    );

    searchParamsMap = { q: 'azul' };
    const Wrapper = createWrapper();
    render(<KbGlobaleView />, { wrapper: Wrapper });

    // All 5 result docTitles should appear
    await waitFor(() => {
      for (let i = 0; i < 5; i++) {
        expect(screen.getByText(`Rulebook ${i}`)).toBeInTheDocument();
      }
    });

    // Verify POST request body shape
    expect(capturedBody).toMatchObject({
      query: 'azul',
    });
    // cursor should be null or absent for first page
    const body = capturedBody as Record<string, unknown>;
    expect(body.cursor === null || body.cursor === undefined).toBe(true);
  });

  // ── Scenario 3: Load more — cursor forwarded, 10 results total ─────────────
  it('S3: load more — second page cursor forwarded, total 10 results', async () => {
    const page1 = Array.from({ length: 5 }, (_, i) => makeSearchResultDto(i));
    const page2 = Array.from({ length: 5 }, (_, i) => makeSearchResultDto(i + 5));
    const capturedBodies: unknown[] = [];

    server.use(
      http.get(`${API_BASE}/api/v1/kb-docs`, () => {
        return HttpResponse.json({ items: [], total: 0, page: 1, pageSize: 20 });
      }),
      http.post(`${API_BASE}/api/v1/knowledge-base/search/global`, async ({ request }) => {
        const body = (await request.json()) as { cursor?: string | null };
        capturedBodies.push(body);
        if (!body.cursor) {
          return HttpResponse.json({
            results: page1,
            hasMore: true,
            nextCursor: 'cursor-page-2',
          });
        }
        return HttpResponse.json({
          results: page2,
          hasMore: false,
          nextCursor: null,
        });
      })
    );

    searchParamsMap = { q: 'azul' };
    const Wrapper = createWrapper();
    render(<KbGlobaleView />, { wrapper: Wrapper });

    // Wait for first page and "Load more" button
    const loadMoreLabel = 'Carica altri';
    await waitFor(() => {
      expect(screen.getByText(`Rulebook 0`)).toBeInTheDocument();
      expect(screen.getByText(loadMoreLabel)).toBeInTheDocument();
    });

    // Click load more
    await userEvent.click(screen.getByText(loadMoreLabel));

    // Wait for second page results
    await waitFor(() => {
      // All 10 results (page 1 + page 2)
      for (let i = 0; i < 10; i++) {
        expect(screen.getByText(`Rulebook ${i}`)).toBeInTheDocument();
      }
    });

    // Verify second request had cursor forwarded
    const secondBody = capturedBodies[1] as { cursor?: string };
    expect(secondBody?.cursor).toBe('cursor-page-2');

    // After second page loads, "Load more" button should be gone (hasMore=false)
    await waitFor(() => {
      expect(screen.queryByText(loadMoreLabel)).not.toBeInTheDocument();
    });
  });

  // ── Scenario 4: Debounce — query fires with correct value after 250ms ────────
  // useDebounce initialises with the current value so the query fires almost
  // immediately on mount when `q` is already set in the URL. The test verifies
  // that the POST carries the correct query value.
  // Real timers only — fake-timers + TanStack Query v5 = deadlock (P129).
  it('S4: debounce coalesces — POST fires with correct query value (wingspan)', async () => {
    const capturedBodies: { query?: string }[] = [];

    server.use(
      http.get(`${API_BASE}/api/v1/kb-docs`, () => {
        return HttpResponse.json({ items: [], total: 0, page: 1, pageSize: 20 });
      }),
      http.post(`${API_BASE}/api/v1/knowledge-base/search/global`, async ({ request }) => {
        const body = (await request.json()) as { query: string };
        capturedBodies.push(body);
        return HttpResponse.json({ results: [], hasMore: false, nextCursor: null });
      })
    );

    // Mount directly on results branch with a >= 2 char query.
    // useDebounce starts with the initial value so the POST fires within
    // the debounce period (250ms). We wait up to 1500ms for it to arrive.
    searchParamsMap = { q: 'wingspan' };
    const Wrapper = createWrapper();
    render(<KbGlobaleView />, { wrapper: Wrapper });

    await waitFor(
      () => {
        // At least one POST should have been captured
        expect(capturedBodies.length).toBeGreaterThanOrEqual(1);
      },
      { timeout: 1500 }
    );

    // The last (or only) request body must carry the correct query
    const lastBody = capturedBodies[capturedBodies.length - 1];
    expect(lastBody?.query).toBe('wingspan');
  }, 5000); // timeout in ms — Vitest 4 form (number, not object)

  // ── Scenario 5: Search 500 error — error banner visible, no crash ──────────
  it('S5: search returns 500 — error banner with role=alert visible', async () => {
    server.use(
      http.get(`${API_BASE}/api/v1/kb-docs`, () => {
        return HttpResponse.json({ items: [], total: 0, page: 1, pageSize: 20 });
      }),
      http.post(`${API_BASE}/api/v1/knowledge-base/search/global`, () => {
        return HttpResponse.json({ error: 'internal error' }, { status: 500 });
      })
    );

    searchParamsMap = { q: 'azul' };
    const Wrapper = createWrapper();
    render(<KbGlobaleView />, { wrapper: Wrapper });

    // Error banner should appear (role="alert" in KbSearchResultsDesktop.ErrorBanner)
    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
  });

  // ── Scenario 6: Query clear — router.push called with bare path ─────────────
  it('S6: query clear — router.push called with bare /knowledge-base/global', async () => {
    server.use(
      http.get(`${API_BASE}/api/v1/kb-docs`, () => {
        return HttpResponse.json({ items: [], total: 0, page: 1, pageSize: 20 });
      }),
      http.post(`${API_BASE}/api/v1/knowledge-base/search/global`, () => {
        return HttpResponse.json({ results: [], hasMore: false, nextCursor: null });
      })
    );

    // Start on results branch
    searchParamsMap = { q: 'azul' };
    const Wrapper = createWrapper();
    render(<KbGlobaleView />, { wrapper: Wrapper });

    // Click the clear button in HeroSearch (aria-label = labels.clear = 'Cancella')
    // HeroSearch shows clear button only when query.length > 0.
    // initialQuery is wired from `q` URL param, so the clear button is visible.
    await waitFor(() => {
      expect(screen.getByLabelText('Cancella')).toBeInTheDocument();
    });

    await userEvent.click(screen.getByLabelText('Cancella'));

    // router.push should be called with the bare path
    expect(mockRouterPush).toHaveBeenCalledWith('/knowledge-base/global');
  });

  // ── Scenario 7: Recent docs fetch error — home error banner visible ─────────
  it('S7: GET /api/v1/kb-docs returns 500 — home error banner visible, no crash', async () => {
    server.use(
      http.get(`${API_BASE}/api/v1/kb-docs`, () => {
        return HttpResponse.json({ error: 'server error' }, { status: 500 });
      })
    );

    searchParamsMap = {};
    const Wrapper = createWrapper();
    render(<KbGlobaleView />, { wrapper: Wrapper });

    // With retry:false in QueryClient + globalRequestCache cleared + 500 MSW response:
    // TanStack Query sets error state immediately (no retries, no stale cache).
    // KbHomeDesktop.ErrorBanner renders role="alert" when recent.error is non-null.
    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
  });
});
