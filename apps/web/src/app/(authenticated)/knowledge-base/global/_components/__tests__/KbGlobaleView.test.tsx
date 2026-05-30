/**
 * KbGlobaleView.test.tsx
 * Issue #1482 Task 6 — Orchestrator unit tests (Phase 1)
 * Issue #1482 Task 9 — Orchestrator viewer + drawer branches (Phase 2)
 *
 * Tests the URL SSOT routing logic, hook gating, and URL push behaviour.
 * Sub-components (HeroSearch, KbHomeDesktop, KbSearchResultsDesktop,
 * KbDocViewerDesktop, DrawerShell) are mocked with data-testid stubs.
 * Hooks are mocked with vi.fn() returning controllable stubs.
 *
 * Pattern: mirrors AgentsLibraryView.test.tsx (next/navigation mock pattern).
 */

import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { UseGlobalKbSearchResult } from '@/hooks/queries/useGlobalKbSearch';
import type { UseUserKbDocsResult } from '@/hooks/queries/useUserKbDocs';
import type { KbDocEnvelope } from '@/lib/api/schemas/kb-chunks.schemas';
import type { KbDoc } from '@/lib/library/hybrid-hub.mappers';
import type { UseQueryResult } from '@tanstack/react-query';

// ─── next/navigation mocks ────────────────────────────────────────────────
// Mutable state object so individual tests can override URL params.

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

// ─── useGlobalKbSearch mock ───────────────────────────────────────────────

const useGlobalKbSearchMock = vi.fn<[unknown], UseGlobalKbSearchResult>();

vi.mock('@/hooks/queries/useGlobalKbSearch', () => ({
  useGlobalKbSearch: (opts: unknown) => useGlobalKbSearchMock(opts),
}));

// ─── useUserKbDocs mock ───────────────────────────────────────────────────

type MockUserKbDocsReturn = Partial<UseQueryResult<UseUserKbDocsResult>>;
const useUserKbDocsMock = vi.fn<[], MockUserKbDocsReturn>();

vi.mock('@/hooks/queries/useUserKbDocs', () => ({
  useUserKbDocs: () => useUserKbDocsMock(),
}));

// ─── useKbDocDetail mock ──────────────────────────────────────────────────

type MockKbDocDetailReturn = Partial<UseQueryResult<KbDocEnvelope | null>>;
const useKbDocDetailMock = vi.fn<[unknown], MockKbDocDetailReturn>();

vi.mock('@/hooks/queries/useKbDocDetail', () => ({
  useKbDocDetail: (opts: unknown) => useKbDocDetailMock(opts),
}));

// ─── useKbAskStream mock ──────────────────────────────────────────────────

const useKbAskStreamMock = vi.fn();

vi.mock('@/hooks/useKbAskStream', () => ({
  useKbAskStream: () => useKbAskStreamMock(),
}));

// ─── react-pdf mock (avoids jsdom PDF rendering issues) ──────────────────

vi.mock('react-pdf', () => ({
  Document: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  Page: () => <div data-testid="pdf-page" />,
  pdfjs: { version: '4.0.0', GlobalWorkerOptions: { workerSrc: '' } },
}));

// ─── Phase 2 lazy component mocks ────────────────────────────────────────
// Mock the component modules directly. next/dynamic is mocked to be a
// synchronous pass-through that returns the named export from the same module
// (already intercepted by the vi.mock above). This avoids any async/require
// resolution issues in jsdom.

vi.mock('@/components/features/kb-globale/KbDocViewerDesktop', () => ({
  KbDocViewerDesktop: (props: Record<string, unknown>) => (
    <div
      data-slot="kb-doc-viewer-desktop"
      data-testid="kb-doc-viewer-desktop"
      data-doc-id={String(props['doc'] ? (props['doc'] as { id: string }).id : '')}
    />
  ),
}));

vi.mock('@/components/features/kb-globale/DrawerShell', () => ({
  DrawerShell: () => <div data-slot="kb-globale-drawer" data-testid="kb-globale-drawer" />,
}));

// next/dynamic: resolve the loader synchronously and return the default export.
// Since the module is already mocked above, the resolved value is the stub.
vi.mock('next/dynamic', () => ({
  default: (
    loader: () => Promise<{ default: React.ComponentType<Record<string, unknown>> }>
  ): React.ComponentType<Record<string, unknown>> => {
    // Synchronous resolution: call the loader eagerly (module is mocked → fast).
    // We return a wrapper that calls the loader result when rendered.
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

// ─── React import for JSX in mocks ───────────────────────────────────────
import React from 'react';

// ─── Child component mocks ────────────────────────────────────────────────
// Replace each sub-component with a minimal stub that:
//   1. Renders a known data-testid for presence assertions.
//   2. Exposes attributes for prop inspection.
//   3. Exposes callback buttons to simulate user interactions.

let capturedHeroSearchProps: Record<string, unknown> = {};
let capturedKbHomeDesktopProps: Record<string, unknown> = {};
let capturedKbSearchResultsDesktopProps: Record<string, unknown> = {};

vi.mock('@/components/features/kb-globale/HeroSearch', () => ({
  HeroSearch: (props: Record<string, unknown>) => {
    capturedHeroSearchProps = props;
    return (
      <div
        data-testid="hero-search"
        data-initial-query={String(props['initialQuery'] ?? '')}
        data-initial-mode={String(props['initialMode'] ?? '')}
      >
        <button
          data-testid="hero-submit-btn"
          onClick={() => {
            const fn = props['onSubmit'] as ((q: string, m: string) => void) | undefined;
            fn?.('azul', 'Semantic');
          }}
        >
          submit
        </button>
        <button
          data-testid="hero-clear-btn"
          onClick={() => {
            const fn = props['onClear'] as (() => void) | undefined;
            fn?.();
          }}
        >
          clear
        </button>
      </div>
    );
  },
}));

vi.mock('@/components/features/kb-globale/KbHomeDesktop', () => ({
  KbHomeDesktop: (props: Record<string, unknown>) => {
    capturedKbHomeDesktopProps = props;
    return (
      <div
        data-testid="kb-home-desktop"
        data-is-loading={String(props['isLoading'])}
        data-has-error={String(props['error'] !== null)}
      />
    );
  },
}));

vi.mock('@/components/features/kb-globale/KbSearchResultsDesktop', () => ({
  KbSearchResultsDesktop: (props: Record<string, unknown>) => {
    capturedKbSearchResultsDesktopProps = props;
    return (
      <div
        data-testid="kb-results-desktop"
        data-query={String(props['query'] ?? '')}
        data-is-loading={String(props['isLoading'])}
      />
    );
  },
}));

// ─── Import component after mocks ─────────────────────────────────────────
// Static import: vi.mock() is hoisted before imports by Vite, so order is safe.

import { KbGlobaleView } from '../KbGlobaleView';

// ─── fixture helpers ──────────────────────────────────────────────────────

function makeSearchResult(): UseGlobalKbSearchResult {
  return {
    results: [],
    hasMore: false,
    isLoading: false,
    isFetchingNextPage: false,
    error: null,
    fetchNextPage: vi.fn(),
  };
}

function makeUserKbDocsResult(): MockUserKbDocsReturn {
  return {
    data: { items: [] as KbDoc[], total: 0, page: 1, pageSize: 20 },
    isLoading: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
  } as MockUserKbDocsReturn;
}

// ─── Tests ────────────────────────────────────────────────────────────────

describe('KbGlobaleView (orchestrator)', () => {
  beforeEach(() => {
    // Reset URL state to empty (home branch) by default.
    searchParamsMap = {};
    // Full reset (clears call counts + return values) then re-configure defaults.
    mockRouterPush.mockReset();
    useGlobalKbSearchMock.mockReset();
    useUserKbDocsMock.mockReset();
    useKbDocDetailMock.mockReset();
    useKbAskStreamMock.mockReset();
    useGlobalKbSearchMock.mockReturnValue(makeSearchResult());
    useUserKbDocsMock.mockReturnValue(makeUserKbDocsResult());
    useKbDocDetailMock.mockReturnValue({
      data: null,
      isLoading: false,
      isError: false,
      error: null,
    });
    useKbAskStreamMock.mockReturnValue({
      state: {
        status: 'idle',
        partialText: '',
        citations: [],
        totalTokens: 0,
        elapsedMs: 0,
        error: null,
        retryCount: 0,
      },
      ask: vi.fn(),
      stop: vi.fn(),
      reset: vi.fn(),
    });
    capturedHeroSearchProps = {};
    capturedKbHomeDesktopProps = {};
    capturedKbSearchResultsDesktopProps = {};
  });

  // ── Test 1: HeroSearch always rendered ──────────────────────────────────
  describe('HeroSearch presence', () => {
    it('renders HeroSearch on home branch (q empty)', () => {
      searchParamsMap = {};
      render(<KbGlobaleView />);
      expect(screen.getByTestId('hero-search')).toBeInTheDocument();
    });

    it('renders HeroSearch on results branch (q present)', () => {
      searchParamsMap = { q: 'azul' };
      render(<KbGlobaleView />);
      expect(screen.getByTestId('hero-search')).toBeInTheDocument();
    });
  });

  // ── Test 2: q empty → home branch ─────────────────────────────────────
  describe('home branch (q empty)', () => {
    it('renders KbHomeDesktop, not KbSearchResultsDesktop', () => {
      searchParamsMap = {};
      render(<KbGlobaleView />);
      expect(screen.getByTestId('kb-home-desktop')).toBeInTheDocument();
      expect(screen.queryByTestId('kb-results-desktop')).not.toBeInTheDocument();
    });

    it('treats whitespace-only q as empty (home branch)', () => {
      searchParamsMap = { q: '   ' };
      render(<KbGlobaleView />);
      expect(screen.getByTestId('kb-home-desktop')).toBeInTheDocument();
      expect(screen.queryByTestId('kb-results-desktop')).not.toBeInTheDocument();
    });
  });

  // ── Test 3: q present → results branch ───────────────────────────────
  describe('results branch (q present)', () => {
    it('renders KbSearchResultsDesktop, not KbHomeDesktop', () => {
      searchParamsMap = { q: 'azul' };
      render(<KbGlobaleView />);
      expect(screen.getByTestId('kb-results-desktop')).toBeInTheDocument();
      expect(screen.queryByTestId('kb-home-desktop')).not.toBeInTheDocument();
    });

    it('passes query prop to KbSearchResultsDesktop', () => {
      searchParamsMap = { q: 'wingspan' };
      render(<KbGlobaleView />);
      expect(screen.getByTestId('kb-results-desktop')).toHaveAttribute('data-query', 'wingspan');
    });
  });

  // ── Test 4: mode=Semantic in URL ─────────────────────────────────────
  it('passes mode=Semantic to useGlobalKbSearch when present in URL', () => {
    searchParamsMap = { q: 'azul', mode: 'Semantic' };
    render(<KbGlobaleView />);
    const callArg = useGlobalKbSearchMock.mock.calls[0]?.[0] as {
      mode?: string;
      enabled?: boolean;
    };
    expect(callArg?.mode).toBe('Semantic');
  });

  // ── Test 5: mode invalid in URL → undefined ───────────────────────────
  it('falls back to mode=undefined when URL mode param is invalid', () => {
    searchParamsMap = { q: 'azul', mode: 'Keyword' }; // not in SearchMode enum
    render(<KbGlobaleView />);
    const callArg = useGlobalKbSearchMock.mock.calls[0]?.[0] as {
      mode?: string;
      enabled?: boolean;
    };
    expect(callArg?.mode).toBeUndefined();
  });

  // ── Test 6: HeroSearch.onSubmit → router.push with ?q (Semantic = default, omit) ──
  it('calls router.push with only ?q=azul when mode is Semantic (default, omitted)', () => {
    searchParamsMap = {};
    render(<KbGlobaleView />);
    screen.getByTestId('hero-submit-btn').click();
    expect(mockRouterPush).toHaveBeenCalledWith('/knowledge-base/global?q=azul');
  });

  // ── Test 7 note: SearchMode currently only has 'Semantic'.
  // Since only one mode exists, the URL serialization omits mode (it is the default).
  // Test 6 covers this path exhaustively. No non-Semantic mode test added to
  // avoid type casts that would break when SearchMode expands. Documented per spec.

  // ── Test 8: HeroSearch.onClear → router.push bare path ──────────────
  it('calls router.push with bare /knowledge-base/global on clear', () => {
    searchParamsMap = { q: 'azul' };
    render(<KbGlobaleView />);
    screen.getByTestId('hero-clear-btn').click();
    expect(mockRouterPush).toHaveBeenCalledWith('/knowledge-base/global');
  });

  // ── Test 9: useGlobalKbSearch.enabled gating ─────────────────────────
  describe('useGlobalKbSearch enabled gating (no double-fetch)', () => {
    it('passes enabled=false when q is empty', () => {
      searchParamsMap = {};
      render(<KbGlobaleView />);
      const callArg = useGlobalKbSearchMock.mock.calls[0]?.[0] as { enabled?: boolean };
      expect(callArg?.enabled).toBe(false);
    });

    it('passes enabled=true when q is present', () => {
      searchParamsMap = { q: 'azul' };
      render(<KbGlobaleView />);
      const callArg = useGlobalKbSearchMock.mock.calls[0]?.[0] as { enabled?: boolean };
      expect(callArg?.enabled).toBe(true);
    });
  });

  // ── Test 10: useUserKbDocs always called ─────────────────────────────
  describe('useUserKbDocs always called (cached, cheap)', () => {
    it('calls useUserKbDocs on home branch', () => {
      searchParamsMap = {};
      render(<KbGlobaleView />);
      expect(useUserKbDocsMock).toHaveBeenCalledTimes(1);
    });

    it('calls useUserKbDocs on results branch too', () => {
      searchParamsMap = { q: 'azul' };
      render(<KbGlobaleView />);
      expect(useUserKbDocsMock).toHaveBeenCalledTimes(1);
    });
  });

  // ── Additional: HeroSearch prop wiring ────────────────────────────────
  describe('HeroSearch prop wiring', () => {
    it('wires initialQuery from URL q', () => {
      searchParamsMap = { q: 'gloomhaven' };
      render(<KbGlobaleView />);
      expect(screen.getByTestId('hero-search')).toHaveAttribute('data-initial-query', 'gloomhaven');
    });

    it('defaults initialMode to Semantic when mode absent from URL', () => {
      searchParamsMap = {};
      render(<KbGlobaleView />);
      expect(screen.getByTestId('hero-search')).toHaveAttribute('data-initial-mode', 'Semantic');
    });

    it('wires Semantic mode from URL to initialMode', () => {
      searchParamsMap = { q: 'azul', mode: 'Semantic' };
      render(<KbGlobaleView />);
      expect(screen.getByTestId('hero-search')).toHaveAttribute('data-initial-mode', 'Semantic');
    });
  });

  // ── KbHomeDesktop receives recentDocs from useUserKbDocs ──────────────
  it('passes recentDocs from useUserKbDocs.data.items to KbHomeDesktop', () => {
    const mockDoc: KbDoc = {
      id: 'doc-1',
      gameId: 'game-1',
      gameName: 'Azul',
      fileName: 'azul.pdf',
      processingState: 'ready',
      pageCount: 10,
      processedAt: '2026-01-01T00:00:00Z',
      uploadedAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z',
    };
    searchParamsMap = {};
    useUserKbDocsMock.mockReturnValue({
      data: { items: [mockDoc], total: 1, page: 1, pageSize: 20 },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
    } as MockUserKbDocsReturn);
    render(<KbGlobaleView />);
    const homeProps = capturedKbHomeDesktopProps as { recentDocs?: KbDoc[] };
    expect(homeProps.recentDocs).toHaveLength(1);
    expect(homeProps.recentDocs?.[0]?.id).toBe('doc-1');
  });

  // ── KbSearchResultsDesktop receives results from useGlobalKbSearch ────
  it('passes results from useGlobalKbSearch to KbSearchResultsDesktop', () => {
    const mockResult = {
      chunkId: 'chunk-1',
      docId: '00000000-0000-0000-0000-000000000001',
      docTitle: 'Azul Rulebook',
      gameId: '00000000-0000-0000-0000-000000000002',
      gameName: 'Azul',
      docType: 'Rulebook',
      headingPath: null,
      snippet: 'On your turn, take tiles from factories...',
      pageNumber: 3,
      score: 0.95,
    };
    searchParamsMap = { q: 'azul' };
    useGlobalKbSearchMock.mockReturnValue({
      results: [mockResult],
      hasMore: false,
      isLoading: false,
      isFetchingNextPage: false,
      error: null,
      fetchNextPage: vi.fn(),
    });
    render(<KbGlobaleView />);
    const resultsProps = capturedKbSearchResultsDesktopProps as { results?: unknown[] };
    expect(resultsProps.results).toHaveLength(1);
  });

  // ── Phase 2: viewer branch (?docId) ──────────────────────────────────────
  describe('Phase 2 — viewer branch (?docId)', () => {
    it('mounts KbDocViewerDesktop when ?docId is present and envelope is ready', () => {
      searchParamsMap = { docId: 'doc-abc' };
      useKbDocDetailMock.mockReturnValue({
        data: {
          status: 'ready',
          doc: {
            id: 'doc-abc',
            title: 'Azul Rulebook',
            pageCount: 12,
            processingStatus: 'ready',
            uploadedAt: '2026-01-01T00:00:00Z',
            processedAt: '2026-01-01T00:00:00Z',
            gameId: 'game-1',
            gameName: 'Azul',
            fileName: 'azul.pdf',
            fileSize: 1234,
            language: null,
          },
        } satisfies KbDocEnvelope,
        isLoading: false,
        isError: false,
        error: null,
      });
      render(<KbGlobaleView />);
      expect(screen.getByTestId('kb-doc-viewer-desktop')).toBeInTheDocument();
    });

    it('calls useKbDocDetail with the docId from URL', () => {
      searchParamsMap = { docId: 'doc-xyz' };
      render(<KbGlobaleView />);
      const callArg = useKbDocDetailMock.mock.calls[0]?.[0] as {
        docId?: string;
        enabled?: boolean;
      };
      expect(callArg?.docId).toBe('doc-xyz');
      expect(callArg?.enabled).toBe(true);
    });

    it('does NOT mount viewer when envelope is locked (doc still processing)', () => {
      searchParamsMap = { docId: 'doc-locked' };
      useKbDocDetailMock.mockReturnValue({
        data: {
          status: 'locked',
          processingStatus: 'processing',
          doc: null,
        } satisfies KbDocEnvelope,
        isLoading: false,
        isError: false,
        error: null,
      });
      render(<KbGlobaleView />);
      expect(screen.queryByTestId('kb-doc-viewer-desktop')).not.toBeInTheDocument();
    });

    it('does NOT mount viewer when docId param is absent', () => {
      searchParamsMap = {};
      render(<KbGlobaleView />);
      expect(screen.queryByTestId('kb-doc-viewer-desktop')).not.toBeInTheDocument();
    });
  });

  // ── Phase 2: drawer branch (?ask=1) ──────────────────────────────────────
  describe('Phase 2 — drawer branch (?ask=1)', () => {
    it('mounts DrawerShell when ?ask=1 is present in URL', () => {
      searchParamsMap = { ask: '1' };
      render(<KbGlobaleView />);
      expect(screen.getByTestId('kb-globale-drawer')).toBeInTheDocument();
    });

    it('does NOT mount DrawerShell when ?ask param is absent', () => {
      searchParamsMap = {};
      render(<KbGlobaleView />);
      expect(screen.queryByTestId('kb-globale-drawer')).not.toBeInTheDocument();
    });

    it('does NOT mount DrawerShell when ?ask=0', () => {
      searchParamsMap = { ask: '0' };
      render(<KbGlobaleView />);
      expect(screen.queryByTestId('kb-globale-drawer')).not.toBeInTheDocument();
    });
  });
});
