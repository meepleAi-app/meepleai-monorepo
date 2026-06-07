/**
 * `/games` hub page tests — Asse D follow-up P2 (#1899, umbrella #1895).
 *
 * Verifies invariante #20 of the GameNight/Session domain model spec:
 * the `Games` sidebar voice lands on `/games?tab=discover` and the hub
 * renders the Discover surface as default (no `?tab` or invalid `?tab`).
 *
 * Tab-specific assertions are kept DOM-only (data-testid + data-active-tab)
 * to avoid coupling the orchestrator test to the Discover internals — those
 * are covered by `DiscoverHub.test.tsx` and the `features/discover/*` suite.
 */

import { render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

// ─── Mocks ────────────────────────────────────────────────────────────────

const searchParamsState: Record<string, string | null> = {};

vi.mock('next/navigation', () => ({
  useSearchParams: () => ({
    get: (k: string) => searchParamsState[k] ?? null,
    toString: () =>
      Object.entries(searchParamsState)
        .filter(([, v]) => v !== null && v !== undefined)
        .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`)
        .join('&'),
  }),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
  usePathname: () => '/games',
}));

// Identity translator returning the key (assertions match by key).
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (k: string) => k,
  }),
}));

// `useMiniNavConfig` is a no-op side effect; we mock it to a spy so we can
// assert wiring (activeTabId + tab href configuration).
const miniNavSpy = vi.fn();
vi.mock('@/hooks/useMiniNavConfig', () => ({
  useMiniNavConfig: (config: unknown) => {
    miniNavSpy(config);
  },
}));

// `DiscoverHub` is a heavy component (lazy chunk + 6 query hooks). Mock to a
// lightweight stub so the orchestrator test stays focused on tab routing.
const discoverHubSpy = vi.fn();
vi.mock('@/components/features/discover/DiscoverHub', () => ({
  DiscoverHub: (props: unknown) => {
    discoverHubSpy(props);
    return <div data-testid="discover-hub-mock">DiscoverHub mock</div>;
  },
}));

// ─── Import under test (after mocks) ──────────────────────────────────────

import GamesHubPage from '../page';

// ─── Helpers ───────────────────────────────────────────────────────────────

function setQueryParam(key: string, value: string | null) {
  searchParamsState[key] = value;
}

function resetParams() {
  Object.keys(searchParamsState).forEach(k => delete searchParamsState[k]);
}

// ─── Tests ─────────────────────────────────────────────────────────────────

describe('/games hub multi-tab page (Asse D P2)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    resetParams();
  });

  afterEach(() => {
    resetParams();
  });

  describe('default tab routing (invariante #20)', () => {
    it('renders Discover tab when no ?tab query param is set', () => {
      render(<GamesHubPage />);

      const hub = screen.getByTestId('games-hub');
      expect(hub).toBeInTheDocument();
      expect(hub).toHaveAttribute('data-active-tab', 'discover');
      expect(screen.getByTestId('discover-hub-mock')).toBeInTheDocument();
    });

    it('renders Discover tab when ?tab=discover is explicit', () => {
      setQueryParam('tab', 'discover');
      render(<GamesHubPage />);

      const hub = screen.getByTestId('games-hub');
      expect(hub).toHaveAttribute('data-active-tab', 'discover');
      expect(screen.getByTestId('discover-hub-mock')).toBeInTheDocument();
    });

    it('falls back to Discover when ?tab=invalid is provided', () => {
      setQueryParam('tab', 'not-a-real-tab');
      render(<GamesHubPage />);

      const hub = screen.getByTestId('games-hub');
      expect(hub).toHaveAttribute('data-active-tab', 'discover');
      expect(screen.getByTestId('discover-hub-mock')).toBeInTheDocument();
    });

    it('falls back to Discover when ?tab is empty string', () => {
      setQueryParam('tab', '');
      render(<GamesHubPage />);

      const hub = screen.getByTestId('games-hub');
      expect(hub).toHaveAttribute('data-active-tab', 'discover');
    });

    it('passes pathnameOverride="/games" to DiscoverHub so URL writes stay scoped', () => {
      render(<GamesHubPage />);

      expect(discoverHubSpy).toHaveBeenCalledWith(
        expect.objectContaining({ pathnameOverride: '/games' })
      );
    });
  });

  describe('coming-soon tab placeholders', () => {
    it('renders Catalogo placeholder when ?tab=catalogo', () => {
      setQueryParam('tab', 'catalogo');
      render(<GamesHubPage />);

      const hub = screen.getByTestId('games-hub');
      expect(hub).toHaveAttribute('data-active-tab', 'catalogo');
      expect(screen.getByTestId('games-tab-catalogo-coming-soon')).toBeInTheDocument();
      expect(screen.getByText('Catalogo')).toBeInTheDocument();
      expect(screen.queryByTestId('discover-hub-mock')).not.toBeInTheDocument();
    });

    it('renders Trending placeholder when ?tab=trending', () => {
      setQueryParam('tab', 'trending');
      render(<GamesHubPage />);

      const hub = screen.getByTestId('games-hub');
      expect(hub).toHaveAttribute('data-active-tab', 'trending');
      expect(screen.getByTestId('games-tab-trending-coming-soon')).toBeInTheDocument();
      expect(screen.getByText('Trending')).toBeInTheDocument();
      expect(screen.queryByTestId('discover-hub-mock')).not.toBeInTheDocument();
    });

    it('renders Community placeholder when ?tab=community', () => {
      setQueryParam('tab', 'community');
      render(<GamesHubPage />);

      const hub = screen.getByTestId('games-hub');
      expect(hub).toHaveAttribute('data-active-tab', 'community');
      expect(screen.getByTestId('games-tab-community-coming-soon')).toBeInTheDocument();
      expect(screen.getByText('Community')).toBeInTheDocument();
      expect(screen.queryByTestId('discover-hub-mock')).not.toBeInTheDocument();
    });
  });

  describe('mini-nav wiring', () => {
    it('configures the breadcrumb to "Games" with 4 tabs (Discover default)', () => {
      render(<GamesHubPage />);

      expect(miniNavSpy).toHaveBeenCalledTimes(1);
      const config = miniNavSpy.mock.calls[0][0];

      expect(config.breadcrumb).toBe('Games');
      expect(config.activeTabId).toBe('discover');
      expect(config.tabs).toHaveLength(4);
      expect(config.tabs.map((t: { id: string }) => t.id)).toEqual([
        'discover',
        'catalogo',
        'trending',
        'community',
      ]);
      expect(config.tabs[0].href).toBe('/games?tab=discover');
      expect(config.tabs[1].href).toBe('/games?tab=catalogo');
      expect(config.tabs[2].href).toBe('/games?tab=trending');
      expect(config.tabs[3].href).toBe('/games?tab=community');
    });

    it('sets activeTabId to the parsed tab when ?tab=catalogo', () => {
      setQueryParam('tab', 'catalogo');
      render(<GamesHubPage />);

      const config = miniNavSpy.mock.calls[0][0];
      expect(config.activeTabId).toBe('catalogo');
    });

    it('sets activeTabId to discover when ?tab is invalid (parse fallback)', () => {
      setQueryParam('tab', 'whatever');
      render(<GamesHubPage />);

      const config = miniNavSpy.mock.calls[0][0];
      expect(config.activeTabId).toBe('discover');
    });
  });
});
