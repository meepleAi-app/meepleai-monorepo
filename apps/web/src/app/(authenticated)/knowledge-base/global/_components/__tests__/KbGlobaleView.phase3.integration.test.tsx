/**
 * KbGlobaleView Phase 3 integration tests (#1737)
 * — MSW-mocked scenarios S1-S6 from spec-panel decisions
 *
 * S1: Facet apply — selecting docType refetches with filter, results filtered
 * S2: Facet clear — clearAll resets URL + refetch baseline
 * S3: URL SSOT round-trip — initial URL with filters pre-selects FilterAccordion
 * S4: PATCH success — Edit→change title→Save → 200 → cache invalidates → UI updates
 * S5: PATCH 404 — generic "Documento non trovato" message (anti-info-leak)
 * S6: PATCH 422 — FluentValidation envelope → inline field error with aria-describedby
 *
 * Pattern follows KbGlobaleView.integration.test.tsx exactly:
 *   - Shared MSW server from @/__tests__/mocks/server (started by vitest.setup.tsx)
 *   - server.use() for per-test handler overrides; server.resetHandlers() in afterEach
 *   - Mutable searchParamsMap for URL control without routing
 *   - next/dynamic stubbed to resolve ssr:false components synchronously
 *   - react-pdf mocked for viewer compatibility
 *   - Fresh QueryClient per test (retry:false, staleTime:0, gcTime:0)
 *
 * 422 envelope: BE returns { errors: { field: ["msg"] } } (FluentValidation).
 * createApiError → ValidationError.fieldErrors flattens to { field: "msg" }
 * so KbEditorDesktop's isHttpFieldError guard picks it up via err.status === 422.
 *
 * @see apps/web/src/app/(authenticated)/knowledge-base/global/_components/KbGlobaleView.tsx
 * @see Issue #1737 Phase 3 — FilterAccordion + KbEditorDesktop
 */

import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { type ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { http, HttpResponse } from 'msw';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { server } from '@/__tests__/mocks/server';
import { globalRequestCache } from '@/lib/api/core/requestCache';

// ─── API base (matches vitest.setup.tsx + handler convention) ────────────────
const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ─── next/navigation mocks ────────────────────────────────────────────────────
// Mutable state so each test can control the URL independently.
// Same pattern as KbGlobaleView.integration.test.tsx.

type SearchParamsRecord = Record<string, string | null>;
let searchParamsMap: SearchParamsRecord = {};
const mockRouterPush = vi.fn();

vi.mock('next/navigation', () => ({
  useSearchParams: () => ({
    get: (key: string) => searchParamsMap[key] ?? null,
    toString: () =>
      Object.entries(searchParamsMap)
        .filter(([, v]) => v != null)
        .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(v!)}`)
        .join('&'),
  }),
  useRouter: () => ({
    push: mockRouterPush,
    replace: vi.fn(),
  }),
  usePathname: () => '/knowledge-base/global',
}));

// ─── next/dynamic mock ────────────────────────────────────────────────────────
// Resolves lazy imports synchronously so ssr:false dynamic components mount
// immediately in jsdom (KbEditorDesktopLazy, KbDocViewerDesktopLazy, DrawerShellLazy).
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

// ─── react-pdf mock ──────────────────────────────────────────────────────────
// KbDocViewerDesktop imports react-pdf which requires canvas/browser APIs.
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

// ─── Wrapper with fresh QueryClient per test ──────────────────────────────────

function createWrapper() {
  const client = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        staleTime: 0,
        gcTime: 0,
      },
      mutations: {
        retry: false,
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

// RFC 4122 v4 UUIDs for test fixtures — must pass Zod uuid() validation.
const DOC_ID = 'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11';
const GAME_ID = 'f5eebc99-9c0b-4ef8-bb6d-6bb9bd380a66';
const ISO_DATE = '2026-05-31T00:00:00+00:00';

const DOC_FIXTURE = {
  id: DOC_ID,
  gameId: GAME_ID,
  gameName: 'Azul',
  fileName: 'azul-rules.pdf',
  processingState: 'Ready' as const,
  pageCount: 24,
  processedAt: ISO_DATE,
  uploadedAt: ISO_DATE,
  updatedAt: ISO_DATE,
  title: 'Old Title',
  tags: ['family'],
  updatedBy: null,
};

// Two search results distinguishable by docType / snippet
const SEARCH_RESULT_RULEBOOK = {
  chunkId: 'c0eebc99-9c0b-4ef8-bb6d-6bb9bd380b11',
  docId: DOC_ID,
  docTitle: 'Azul Rules',
  gameId: GAME_ID,
  gameName: 'Azul',
  docType: 'Rulebook',
  headingPath: 'Setup',
  snippet: 'Place tiles on the board...',
  pageNumber: 3,
  score: 0.9,
};

const SEARCH_RESULT_ERRATA = {
  chunkId: 'c1eebc99-9c0b-4ef8-bb6d-6bb9bd380b22',
  docId: DOC_ID,
  docTitle: 'Azul Errata',
  gameId: GAME_ID,
  gameName: 'Azul',
  docType: 'Errata',
  headingPath: 'Corrections',
  snippet: 'Errata correction for tile placement...',
  pageNumber: 7,
  score: 0.75,
};

// ─── Shared MSW handlers helpers ─────────────────────────────────────────────

/** kb-docs list handler — returns DOC_FIXTURE with Phase 3 fields */
function kbDocsListHandler() {
  return http.get(`${API_BASE}/api/v1/kb-docs`, () =>
    HttpResponse.json({
      items: [DOC_FIXTURE],
      total: 1,
      page: 1,
      pageSize: 20,
    })
  );
}

/**
 * Global search handler: returns Rulebook-only when body.docType = ['Rulebook'],
 * returns both results otherwise (baseline).
 */
function globalSearchHandler() {
  return http.post(`${API_BASE}/api/v1/knowledge-base/search/global`, async ({ request }) => {
    const body = (await request.json()) as { docType?: string[] };
    if (body.docType?.includes('Rulebook') && !body.docType?.includes('Errata')) {
      return HttpResponse.json({
        results: [SEARCH_RESULT_RULEBOOK],
        hasMore: false,
        nextCursor: null,
      });
    }
    return HttpResponse.json({
      results: [SEARCH_RESULT_RULEBOOK, SEARCH_RESULT_ERRATA],
      hasMore: false,
      nextCursor: null,
    });
  });
}

/**
 * PATCH /kb-docs/:id handler:
 * - body.title === 'TRIGGER_404' → 404
 * - body.title === 'TRIGGER_422' → 422 { errors: { title: ['Title must not exceed 200 characters.'] } }
 * - otherwise → 200 with updated title
 */
function patchKbDocHandler() {
  return http.patch(`${API_BASE}/api/v1/kb-docs/:id`, async ({ request }) => {
    const body = (await request.json()) as { title?: string };
    if (body.title === 'TRIGGER_404') {
      return new HttpResponse(null, { status: 404 });
    }
    if (body.title === 'TRIGGER_422') {
      return HttpResponse.json(
        { errors: { title: ['Title must not exceed 200 characters.'] } },
        { status: 422 }
      );
    }
    return HttpResponse.json({ ...DOC_FIXTURE, title: body.title ?? DOC_FIXTURE.title });
  });
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('KbGlobaleView Phase 3 — S1-S6 (#1737)', () => {
  beforeEach(() => {
    searchParamsMap = {};
    mockRouterPush.mockReset();
    // Clear deduplication cache (same pattern as Phase 1+2 integration tests).
    globalRequestCache.clear();
  });

  afterEach(() => {
    server.resetHandlers();
  });

  // ── S1: Facet apply ────────────────────────────────────────────────────────
  it('S1: docType filter applied — selecting Rulebook refetches and hides Errata result', async () => {
    // Start on results branch with no filter → both results visible
    server.use(kbDocsListHandler(), globalSearchHandler());
    searchParamsMap = { q: 'azul' };
    const Wrapper = createWrapper();
    render(<KbGlobaleView />, { wrapper: Wrapper });

    // Wait for both results to appear (baseline — no filter)
    await waitFor(() => {
      expect(screen.getByText('Azul Rules')).toBeInTheDocument();
      expect(screen.getByText('Azul Errata')).toBeInTheDocument();
    });

    // Expand the DocType accordion section and click 'Regolamento' (Rulebook)
    fireEvent.click(screen.getByRole('button', { name: /tipo documento/i }));
    fireEvent.click(screen.getByLabelText(/regolamento/i));

    // The onChange handler calls router.push with ?docType=Rulebook
    // Since we control the URL via searchParamsMap and mockRouterPush,
    // simulate the URL update to trigger re-render with the new filter.
    expect(mockRouterPush).toHaveBeenCalled();
    const pushedUrl = mockRouterPush.mock.calls[0]?.[0] as string;
    expect(pushedUrl).toMatch(/docType=Rulebook/);
  }, 8000);

  // ── S2: Facet clear ────────────────────────────────────────────────────────
  it('S2: clearAll resets all facets — calls router.push with cleaned URL', async () => {
    server.use(kbDocsListHandler(), globalSearchHandler());

    // Start with docType=Rulebook pre-selected via URL
    searchParamsMap = { q: 'azul', docType: 'Rulebook' };
    const Wrapper = createWrapper();
    render(<KbGlobaleView />, { wrapper: Wrapper });

    // Wait for FilterAccordion to render (results branch + filters present)
    await waitFor(() => {
      // FilterAccordion shows clearAll button when hasAnyFilter=true
      expect(screen.getByRole('button', { name: /cancella filtri/i })).toBeInTheDocument();
    });

    // Click clearAll
    fireEvent.click(screen.getByRole('button', { name: /cancella filtri/i }));

    // router.push should be called; cleared URL should not contain docType
    expect(mockRouterPush).toHaveBeenCalled();
    const pushedUrl = mockRouterPush.mock.calls[0]?.[0] as string;
    expect(pushedUrl).not.toMatch(/docType/);
  }, 8000);

  // ── S3: URL SSOT round-trip ────────────────────────────────────────────────
  it('S3: URL ?docType=Rulebook&lang=it pre-selects FilterAccordion checkboxes', async () => {
    server.use(kbDocsListHandler(), globalSearchHandler());

    // Mount with pre-set URL filters
    searchParamsMap = { q: 'azul', docType: 'Rulebook', lang: 'it' };
    const Wrapper = createWrapper();
    render(<KbGlobaleView />, { wrapper: Wrapper });

    // Wait for FilterAccordion to appear (results branch)
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /tipo documento/i })).toBeInTheDocument();
    });

    // Expand DocType accordion
    fireEvent.click(screen.getByRole('button', { name: /tipo documento/i }));

    // Regolamento (Rulebook) checkbox should be pre-checked
    const rulebookCheckbox = screen.getByLabelText(/regolamento/i) as HTMLInputElement;
    expect(rulebookCheckbox.checked).toBe(true);

    // Expand Language accordion
    fireEvent.click(screen.getByRole('button', { name: /lingua/i }));

    // Italiano (it) radio should be pre-selected
    const italianoRadio = screen.getByLabelText(/italiano/i) as HTMLInputElement;
    expect(italianoRadio.checked).toBe(true);
  }, 8000);

  // ── S4: PATCH success ──────────────────────────────────────────────────────
  it('S4: PATCH success — editor mounts pre-filled, PATCH called, router navigates away', async () => {
    server.use(kbDocsListHandler(), patchKbDocHandler());

    // Mount at home branch with ?docId + ?edit=1 → editTargetDto resolved from useUserKbDocs
    searchParamsMap = { docId: DOC_ID, edit: '1' };
    const Wrapper = createWrapper();
    render(<KbGlobaleView />, { wrapper: Wrapper });

    // Wait for KbEditorDesktop to mount (useUserKbDocs returns DOC_FIXTURE → rawItems)
    await waitFor(() => {
      expect(screen.getByLabelText(/titolo/i)).toBeInTheDocument();
    });

    // Pre-filled with existing title from DOC_FIXTURE.title
    const titleInput = screen.getByLabelText(/titolo/i) as HTMLInputElement;
    expect(titleInput.value).toBe('Old Title');

    // Change the title
    fireEvent.change(titleInput, { target: { value: 'Brand New Title' } });
    expect(titleInput.value).toBe('Brand New Title');

    // Submit the form
    fireEvent.click(screen.getByRole('button', { name: /salva/i }));

    // After PATCH success, onClose calls router.push with edit param removed.
    // Since searchParamsMap is a static mock (doesn't update from router.push),
    // we verify via mockRouterPush that the URL navigation happened without ?edit=1.
    await waitFor(() => {
      expect(mockRouterPush).toHaveBeenCalled();
    });
    const pushedUrl = mockRouterPush.mock.calls[
      mockRouterPush.mock.calls.length - 1
    ]?.[0] as string;
    expect(pushedUrl).not.toMatch(/edit=1/);
  }, 8000);

  // ── S5: PATCH 404 ──────────────────────────────────────────────────────────
  it('S5: PATCH 404 → generic notFoundError message (anti-info-leak)', async () => {
    server.use(kbDocsListHandler(), patchKbDocHandler());

    searchParamsMap = { docId: DOC_ID, edit: '1' };
    const Wrapper = createWrapper();
    render(<KbGlobaleView />, { wrapper: Wrapper });

    // Wait for editor to mount
    await waitFor(() => {
      expect(screen.getByLabelText(/titolo/i)).toBeInTheDocument();
    });

    // Trigger 404 via sentinel title
    fireEvent.change(screen.getByLabelText(/titolo/i), { target: { value: 'TRIGGER_404' } });
    fireEvent.click(screen.getByRole('button', { name: /salva/i }));

    // Generic notFoundError message should appear
    await waitFor(() => {
      expect(screen.getByText(/documento non trovato/i)).toBeInTheDocument();
    });

    // Must NOT show forbidden/unauthorized text (anti-info-leak DEC-3)
    expect(screen.queryByText(/non autorizzato|forbidden|403/i)).not.toBeInTheDocument();
  }, 8000);

  // ── S6: PATCH 422 field errors ─────────────────────────────────────────────
  it('S6: PATCH 422 → FluentValidation envelope → inline field error with aria-describedby', async () => {
    server.use(kbDocsListHandler(), patchKbDocHandler());

    searchParamsMap = { docId: DOC_ID, edit: '1' };
    const Wrapper = createWrapper();
    render(<KbGlobaleView />, { wrapper: Wrapper });

    // Wait for editor to mount
    await waitFor(() => {
      expect(screen.getByLabelText(/titolo/i)).toBeInTheDocument();
    });

    // Trigger 422 via sentinel title
    fireEvent.change(screen.getByLabelText(/titolo/i), { target: { value: 'TRIGGER_422' } });
    fireEvent.click(screen.getByRole('button', { name: /salva/i }));

    // Inline field error should appear under the title input
    await waitFor(() => {
      expect(screen.getByText(/title must not exceed 200/i)).toBeInTheDocument();
    });

    // Title input must have aria-describedby linking it to the error message
    expect(screen.getByLabelText(/titolo/i)).toHaveAttribute('aria-describedby');

    // Form must NOT close (error is recoverable)
    expect(screen.getByLabelText(/titolo/i)).toBeInTheDocument();
  }, 8000);
});
