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

import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { type ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { http, HttpResponse } from 'msw';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { server } from '@/__tests__/mocks/server';
import { globalRequestCache } from '@/lib/api/core/requestCache';
import type { KbAskEvent } from '@/lib/api/schemas/kb-ask.schemas';

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

// ─── next/dynamic mock ────────────────────────────────────────────────────────
// Resolves lazy imports synchronously so ssr:false dynamic components
// (KbDocViewerDesktopLazy, DrawerShellLazy) mount immediately in jsdom.
vi.mock('next/dynamic', () => ({
  default: (
    loader: () => Promise<{ default: React.ComponentType<Record<string, unknown>> }>
  ): React.ComponentType<Record<string, unknown>> => {
    let Inner: React.ComponentType<Record<string, unknown>> | null = null;
    void loader().then(mod => {
      Inner = mod.default;
    });
    const Wrapper = (props: Record<string, unknown>) =>
      Inner ? React.createElement(Inner, props) : null;
    Wrapper.displayName = 'NextDynamicStub';
    return Wrapper;
  },
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

// ─── Phase 2 — MSW integration S8..S11 ───────────────────────────────────────
//
// SSE helper — mirrors kbAskClient.test.ts `buildSseStream`. Returns a
// ReadableStream<Uint8Array> encoding each event as `data: <JSON>\n\n`.
//
// Content-Type: text/event-stream is set on the MSW HttpResponse so the
// kbAskClient SSE parser receives a proper event-stream response.

function buildSseStream(events: KbAskEvent[]): ReadableStream<Uint8Array> {
  const encoder = new TextEncoder();
  return new ReadableStream({
    start(controller) {
      for (const evt of events) {
        controller.enqueue(encoder.encode(`data: ${JSON.stringify(evt)}\n\n`));
      }
      controller.close();
    },
  });
}

// ── react-pdf mock for Phase 2 viewer tests ──────────────────────────────────
// KbDocViewerDesktop imports react-pdf which requires canvas/browser APIs.
// In jsdom we mock it to a simple div so the viewer can mount without crashing.
vi.mock('react-pdf', () => ({
  Document: ({
    children,
    onLoadSuccess,
  }: {
    children: ReactNode;
    onLoadSuccess?: (p: { numPages: number }) => void;
  }) => {
    onLoadSuccess?.({ numPages: 5 });
    return <div data-testid="react-pdf-document">{children}</div>;
  },
  Page: ({ pageNumber }: { pageNumber: number }) => <div data-testid={`pdf-page-${pageNumber}`} />,
  pdfjs: { GlobalWorkerOptions: { workerSrc: '' } },
}));

describe('Phase 2 — MSW integration S8..S11', () => {
  beforeEach(() => {
    mockRouterPush.mockClear();
    // Clear request cache so each test starts with a clean fetch slate.
    globalRequestCache.clear();
  });

  // ── Scenario S8: ?docId mounts KbDocViewerDesktop ─────────────────────────
  it('S8: ?docId mounts KbDocViewerDesktop with doc detail', async () => {
    const docId = DOC_IDS[0]!;

    server.use(
      // useKbDocDetail fetches GET /api/v1/kb-docs/:id
      http.get(`${API_BASE}/api/v1/kb-docs/:id`, ({ params }) => {
        if (params['id'] !== docId) return new HttpResponse(null, { status: 404 });
        return HttpResponse.json({
          id: docId,
          title: 'Wingspan Rulebook',
          docType: 'rulebook',
          gameId: GAME_IDS[0],
          gameName: 'Wingspan',
          uploaderName: 'testuser',
          uploadedAt: ISO_DATE,
          lastIngestedAt: ISO_DATE,
          processingStatus: 'ready',
          chunkCount: 10,
          pageCount: 20,
          language: 'it',
          tags: [],
        });
      }),
      // useUserKbDocs — return empty so home branch baseline is stable
      http.get(`${API_BASE}/api/v1/kb-docs`, () =>
        HttpResponse.json({ items: [], total: 0, page: 1, pageSize: 20 })
      )
    );

    // Mount at home branch (q empty) with ?docId set → viewer should appear
    searchParamsMap = { docId, page: '3' };
    const Wrapper = createWrapper();
    render(<KbGlobaleView />, { wrapper: Wrapper });

    // KbDocViewerDesktopLazy (ssr:false dynamic) renders once doc detail resolves.
    // data-slot="kb-doc-viewer-desktop" is set on the root div of KbDocViewerDesktop.
    await waitFor(() => {
      expect(document.querySelector('[data-slot="kb-doc-viewer-desktop"]')).toBeInTheDocument();
    });

    // react-pdf mock renders pdf-page-3 (activePage from ?page=3)
    await waitFor(() => {
      expect(screen.getByTestId('pdf-page-3')).toBeInTheDocument();
    });
  }, 5000);

  // ── Scenario S9: ?ask=1 + suggestion click → SSE → completed state ─────────
  // Uses vi.spyOn(global, 'fetch') (not MSW) for the SSE POST — MSW's ReadableStream
  // body routing through response.body.getReader() in jsdom has known limitations.
  // The kb-docs GET still goes through MSW (TanStack Query, not SSE).
  it('S9: ?ask=1 + ask flow streams to completed state', async () => {
    const sseEvents: KbAskEvent[] = [
      { type: 0, data: { message: 'Ricerca…' } },
      { type: 7, data: { token: 'Risposta' } },
      {
        type: 4,
        data: {
          totalTokens: 1,
          promptTokens: 10,
          completionTokens: 1,
          estimatedReadingTimeMinutes: 0,
          confidence: null,
        },
      },
    ];

    // Stub kb-docs via MSW (TanStack Query REST call).
    server.use(
      http.get(`${API_BASE}/api/v1/kb-docs`, () =>
        HttpResponse.json({ items: [], total: 0, page: 1, pageSize: 20 })
      )
    );

    // Stub the SSE POST via fetch spy (bypasses MSW for the streaming call).
    const originalFetch = global.fetch;
    vi.spyOn(global, 'fetch').mockImplementation(async (input, init) => {
      const url = typeof input === 'string' ? input : (input as Request).url;
      if (url.includes('/knowledge-base/ask/global')) {
        return new Response(buildSseStream(sseEvents), {
          status: 200,
          headers: { 'content-type': 'text/event-stream' },
        });
      }
      // All other calls → real fetch (MSW will intercept REST calls).
      return originalFetch(input, init);
    });

    // Mount at ?ask=1 → DrawerShellLazy mounts → DrawerIdle visible
    searchParamsMap = { ask: '1' };
    const Wrapper = createWrapper();
    render(<KbGlobaleView />, { wrapper: Wrapper });

    // Wait for DrawerIdle to appear (data-testid set in DrawerIdle.tsx)
    await waitFor(() => {
      expect(screen.getByTestId('drawer-state-idle')).toBeInTheDocument();
    });

    // Click the first suggestion button to trigger the ask flow.
    // Suggestions from LABELS.drawer.suggestions:
    //   "Come funziona il setup iniziale?"
    //   "Quali sono le abilità della classe Scout?"
    //   "Differenza tra base e enhanced effect?"
    const allButtons = screen.getAllByRole('button');
    // Suggestion buttons are type="button" (not submit), click first non-close button.
    const suggestionBtn =
      allButtons.find(
        b => b.getAttribute('type') === 'button' && b.textContent?.includes('setup')
      ) ?? allButtons[0];
    await userEvent.click(suggestionBtn!);

    // SSE stream fires → hook FSM transitions streaming → completed
    await waitFor(
      () => {
        expect(screen.getByTestId('drawer-state-completed')).toBeInTheDocument();
      },
      { timeout: 3000 }
    );

    vi.restoreAllMocks();
  }, 8000);

  // ── Scenario S10: citation click → router.push ?docId=&page= ──────────────
  // jsdom limitation: next/navigation router.push is a mock, URL does not change.
  // We assert the mock call payload instead.
  it.skip('S10: citation click pushes ?docId=&page= URL (deferred — requires full E2E harness)', () => {
    // TODO: S10 requires:
    //   1. Pre-seed useKbAskStream state to status='completed' with citations.
    //   2. Click a CitationPill to trigger openViewer callback.
    //   3. Assert mockRouterPush was called with ?docId=<id>&page=<n>.
    // In jsdom, forcing the FSM hook into 'completed' state from outside
    // requires either an integration with the real SSE flow (covered by S9)
    // or a test-seeded initial state — which the hook does not expose externally.
    // Deferred to a Playwright E2E test in a follow-up PR.
  });

  // ── Scenario S11: completed-empty state (no docs in library) ──────────────
  // Uses vi.spyOn(global, 'fetch') for the SSE POST (same reason as S9).
  it('S11: completed-empty CTA visible when Complete event has zero accessible docs', async () => {
    // 0 citations + Complete(totalTokens=0) → FSM enters 'completed-empty'
    const sseEventsEmpty: KbAskEvent[] = [
      { type: 0, data: { message: 'Ricerca…' } },
      // No Citation event (type 1) — zero accessible docs
      {
        type: 4,
        data: {
          totalTokens: 0,
          promptTokens: 0,
          completionTokens: 0,
          estimatedReadingTimeMinutes: 0,
          confidence: null,
        },
      },
    ];

    server.use(
      http.get(`${API_BASE}/api/v1/kb-docs`, () =>
        HttpResponse.json({ items: [], total: 0, page: 1, pageSize: 20 })
      )
    );

    const originalFetch = global.fetch;
    vi.spyOn(global, 'fetch').mockImplementation(async (input, init) => {
      const url = typeof input === 'string' ? input : (input as Request).url;
      if (url.includes('/knowledge-base/ask/global')) {
        return new Response(buildSseStream(sseEventsEmpty), {
          status: 200,
          headers: { 'content-type': 'text/event-stream' },
        });
      }
      return originalFetch(input, init);
    });

    searchParamsMap = { ask: '1' };
    const Wrapper = createWrapper();
    render(<KbGlobaleView />, { wrapper: Wrapper });

    // Wait for DrawerIdle to appear
    await waitFor(() => {
      expect(screen.getByTestId('drawer-state-idle')).toBeInTheDocument();
    });

    // Click a suggestion to fire the ask request
    const allButtons = screen.getAllByRole('button');
    const suggestionBtn =
      allButtons.find(
        b => b.getAttribute('type') === 'button' && b.textContent?.includes('setup')
      ) ?? allButtons[0];
    await userEvent.click(suggestionBtn!);

    // No citations → FSM → completed-empty → DrawerEmpty mounts
    await waitFor(
      () => {
        expect(screen.getByTestId('drawer-state-empty')).toBeInTheDocument();
      },
      { timeout: 3000 }
    );

    // DrawerEmpty should render the CTA button (label: 'Vai alla libreria')
    expect(screen.getByRole('button', { name: /libreria/i })).toBeInTheDocument();

    vi.restoreAllMocks();
  }, 8000);
});
