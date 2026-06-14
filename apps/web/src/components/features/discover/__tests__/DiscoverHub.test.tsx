/**
 * `DiscoverHub` smoke tests — Asse D follow-up P2 (#1899).
 *
 * Verifies the surface component extracted from `/discover/page.tsx`:
 *   - Mounts without crashing when wrapped in a Suspense boundary.
 *   - Reads URL state (q + entity) via `useSearchParams`.
 *   - Renders the eager above-the-fold row (trending).
 *
 * Deeper interaction coverage (search commit, pill change, telemetry) lives
 * in the upstream `/discover/page` integration suite and per-primitive tests
 * (`features/discover/__tests__/*.test.tsx`). This file is intentionally
 * narrow — its purpose is to guard the extraction boundary.
 */

import { render } from '@testing-library/react';
import { Suspense } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

// ─── Mocks ────────────────────────────────────────────────────────────────

const searchParamsState: Record<string, string | null> = {};
const replaceSpy = vi.fn();
const pushSpy = vi.fn();

vi.mock('next/navigation', () => ({
  useSearchParams: () => ({
    get: (k: string) => searchParamsState[k] ?? null,
    toString: () =>
      Object.entries(searchParamsState)
        .filter(([, v]) => v !== null && v !== undefined)
        .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`)
        .join('&'),
  }),
  useRouter: () => ({ replace: replaceSpy, push: pushSpy }),
  usePathname: () => '/discover',
}));

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (k: string) => k,
  }),
}));

const trendingState: { data: unknown[]; isLoading: boolean; isError: boolean } = {
  data: [],
  isLoading: false,
  isError: false,
};

vi.mock('@/hooks/queries/useCatalogTrending', () => ({
  useCatalogTrending: () => ({
    data: trendingState.data,
    isLoading: trendingState.isLoading,
    isError: trendingState.isError,
    refetch: vi.fn(),
  }),
}));

vi.mock('@/lib/analytics/track-event', () => ({
  trackEvent: vi.fn(),
}));

// Mock the heavy lazy chunk so the component renders synchronously in JSDOM.
vi.mock('@/app/(authenticated)/discover/_DiscoverBelowFoldRows', () => ({
  DiscoverBelowFoldRows: () => <div data-testid="below-fold-rows-mock" />,
}));

// Stub HubLayout to expose children without coupling tests to its DOM.
vi.mock('@/components/layout/HubLayout', () => ({
  HubLayout: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="hub-layout">{children}</div>
  ),
}));

// ─── Import under test (after mocks) ──────────────────────────────────────

import { DiscoverHub } from '../DiscoverHub';

// ─── Helpers ───────────────────────────────────────────────────────────────

function resetParams() {
  Object.keys(searchParamsState).forEach(k => delete searchParamsState[k]);
  trendingState.data = [];
  trendingState.isLoading = false;
  trendingState.isError = false;
}

function renderHub(props: Parameters<typeof DiscoverHub>[0] = {}) {
  return render(
    <Suspense fallback={null}>
      <DiscoverHub {...props} />
    </Suspense>
  );
}

// ─── Tests ─────────────────────────────────────────────────────────────────

describe('DiscoverHub component (extracted surface)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    resetParams();
  });

  it('mounts inside Suspense without crashing (default URL state)', () => {
    const { getByTestId } = renderHub();
    expect(getByTestId('hub-layout')).toBeInTheDocument();
  });

  it('renders the rows container slot (eager Row 1 + lazy below-fold mock)', () => {
    const { container, getByTestId } = renderHub();
    const rowsContainer = container.querySelector('[data-slot="discover-rows"]');
    expect(rowsContainer).toBeInTheDocument();
    expect(getByTestId('below-fold-rows-mock')).toBeInTheDocument();
  });

  it('reads ?q and ?entity from URL state on mount', () => {
    // Best-effort smoke: the component reads searchParams synchronously in
    // its initialQ/initialEntity init; we just assert the render succeeds
    // when those values are present (no throw, no missing-key crashes).
    searchParamsState.q = 'catan';
    searchParamsState.entity = 'games';

    const { getByTestId } = renderHub();
    expect(getByTestId('hub-layout')).toBeInTheDocument();
  });

  it('accepts a pathnameOverride prop (used by /games hub)', () => {
    // We can't easily intercept the internal `updateUrl` from this layer,
    // but we can assert that passing the override does not break the render.
    const { getByTestId } = renderHub({ pathnameOverride: '/games' });
    expect(getByTestId('hub-layout')).toBeInTheDocument();
  });
});

describe('DiscoverHub navigation (#2085 F2/F3)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    resetParams();
  });

  it('navigates to /games/{id} when a trending card is clicked', async () => {
    trendingState.data = [
      {
        rank: 1,
        gameId: 'g-azul',
        title: 'Azul',
        thumbnailUrl: null,
        score: 99,
        searchCount: 0,
        viewCount: 0,
        libraryAddCount: 0,
        playCount: 0,
        hasKnowledgeBase: true,
      },
    ];

    const { container } = renderHub();
    const card = container.querySelector('[data-slot="row-card"]') as HTMLElement | null;
    expect(card).not.toBeNull();

    // Find the inner clickable element (button or link wrapper).
    const clickable = (card!.tagName === 'BUTTON' ? card : card!.querySelector('button,a'))!;
    (clickable as HTMLElement).click();

    expect(pushSpy).toHaveBeenCalledWith('/games/g-azul');
  });

  it('does not navigate when the trending row is empty (no card to click)', () => {
    trendingState.data = [];
    renderHub();
    expect(pushSpy).not.toHaveBeenCalled();
  });
});
