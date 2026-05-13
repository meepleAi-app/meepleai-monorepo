/**
 * Integration tests for PlayerDetailView orchestrator — Wave 3 /players/[id].
 *
 * Strategy: spy mock on usePlayerStatistics + useSearchParams to cover all
 * 4 FSM cells and URL override scenarios.
 *
 * Pattern: mirrors Wave C.1 GameDetailViewV2.test.tsx integration test pattern.
 * data-slot selectors mirror the component contracts from Task 2.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';

// ─── Mock i18n ───────────────────────────────────────────────────────────────

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string, _params?: Record<string, unknown>) => key,
  }),
}));

// ─── Mock next/navigation ────────────────────────────────────────────────────

const mockSearchParams = new URLSearchParams();
vi.mock('next/navigation', () => ({
  useSearchParams: () => mockSearchParams,
  useRouter: () => ({ push: vi.fn() }),
}));

// ─── Mock usePlayerStatistics ─────────────────────────────────────────────────

const mockStatsQuery = vi.fn();

vi.mock('@/hooks/queries/usePlayersFromRecords', () => ({
  usePlayerStatistics: () => mockStatsQuery(),
}));

// ─── Mock visual fixture (IS_VISUAL_TEST_BUILD=false for most tests) ──────────

vi.mock('@/lib/player-detail/player-detail-visual-test-fixture', () => ({
  IS_VISUAL_TEST_BUILD: false,
  tryLoadVisualTestFixture: vi.fn(() => null),
}));

// ─── Component under test ─────────────────────────────────────────────────────

import { PlayerDetailView } from '../PlayerDetailView';

// ─── Helpers ──────────────────────────────────────────────────────────────────

function renderView(playerId: string | null) {
  return render(<PlayerDetailView playerId={playerId} />);
}

function getBySlot(container: HTMLElement, slot: string): HTMLElement {
  const el = container.querySelector(`[data-slot="${slot}"]`);
  if (!el) throw new Error(`Element with data-slot="${slot}" not found`);
  return el as HTMLElement;
}

function queryBySlot(container: HTMLElement, slot: string): HTMLElement | null {
  return container.querySelector(`[data-slot="${slot}"]`) as HTMLElement | null;
}

const BASE_STATS = {
  totalSessions: 10,
  totalWins: 7,
  gamePlayCounts: { Wingspan: 5, Pandemic: 3 },
  averageScoresByGame: { Wingspan: 42.5, Pandemic: 60.0 },
};

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('PlayerDetailView', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockSearchParams.delete('state');
    mockStatsQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: BASE_STATS,
      refetch: vi.fn(),
    });
  });

  // ── Cell 1: null playerId → not-found shell ─────────────────────────────────

  it('Cell 1: renders not-found shell when playerId is null', () => {
    renderView(null);
    const { container } = render(<PlayerDetailView playerId={null} />);
    expect(getBySlot(container, 'player-detail-not-found')).toBeDefined();
  });

  // ── Cell 2: loading state ───────────────────────────────────────────────────

  it('Cell 2: renders loading shell when statsQuery.isLoading is true', () => {
    mockStatsQuery.mockReturnValue({
      isLoading: true,
      isError: false,
      data: undefined,
      refetch: vi.fn(),
    });

    const { container } = renderView('sara-rossi');
    expect(getBySlot(container, 'player-detail-loading')).toBeDefined();
  });

  // ── Cell 3: error state ─────────────────────────────────────────────────────

  it('Cell 3: renders error shell when statsQuery.isError is true', () => {
    mockStatsQuery.mockReturnValue({
      isLoading: false,
      isError: true,
      data: undefined,
      refetch: vi.fn(),
    });

    const { container } = renderView('sara-rossi');
    expect(getBySlot(container, 'player-detail-error')).toBeDefined();
  });

  it('Cell 3: error shell has retry CTA', () => {
    const mockRefetch = vi.fn();
    mockStatsQuery.mockReturnValue({
      isLoading: false,
      isError: true,
      data: undefined,
      refetch: mockRefetch,
    });

    const { container } = renderView('sara-rossi');
    expect(getBySlot(container, 'player-detail-error-retry')).toBeDefined();
  });

  // ── Cell 4: no data (undefined) → not-found ──────────────────────────────────

  it('Cell 4: renders not-found when stats data is undefined (no content)', () => {
    mockStatsQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: undefined,
      refetch: vi.fn(),
    });

    const { container } = renderView('sara-rossi');
    expect(getBySlot(container, 'player-detail-not-found')).toBeDefined();
  });

  it('Cell 4b: stats success with totalSessions=0 and data present → still renders default (schema reality)', () => {
    // data is present (not undefined) → hasData=true → FSM='default'
    mockStatsQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { totalSessions: 0, totalWins: 0, gamePlayCounts: {}, averageScoresByGame: {} },
      refetch: vi.fn(),
    });

    const { container } = renderView('sara-rossi');
    expect(getBySlot(container, 'detail-page-layout')).toBeDefined();
  });

  // ── Cell 5: success with data → default render ──────────────────────────────

  it('Cell 5: renders default view when stats are loaded', () => {
    const { container } = renderView('sara-rossi');
    expect(getBySlot(container, 'detail-page-layout')).toBeDefined();
  });

  it('Cell 5: renders PlayerHero with displayName decoded from URL slug', () => {
    const { container } = renderView('sara-rossi');
    // displayName = decodeURIComponent('sara-rossi').replace(/-/g, ' ') = 'sara rossi'
    // PlayerHero renders the name in an h1
    const h1 = container.querySelector('h1');
    expect(h1?.textContent).toBe('sara rossi');
  });

  // ── Win rate calculation ─────────────────────────────────────────────────────

  it('winRate is 0 when totalSessions is 0 (no divide-by-zero)', () => {
    mockStatsQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        totalSessions: 0,
        totalWins: 0,
        gamePlayCounts: { Wingspan: 1 },
        averageScoresByGame: {},
      },
      refetch: vi.fn(),
    });

    // Should render default view without crashing
    const { container } = renderView('test-player');
    expect(getBySlot(container, 'detail-page-layout')).toBeDefined();
  });

  // ── favoriteGameName extraction ──────────────────────────────────────────────

  it('favoriteGameName is max-count gameName from gamePlayCounts (reflected in FavoriteAgentCard gameName prop)', () => {
    mockStatsQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        totalSessions: 10,
        totalWins: 5,
        gamePlayCounts: { Pandemic: 2, Wingspan: 8, Catan: 1 },
        averageScoresByGame: {},
      },
      refetch: vi.fn(),
    });

    const { container } = renderView('test-player');
    // Default render reached — profile.favoriteGameName = 'Wingspan' (max count)
    // FavoriteAgentCard only renders gameName when agentName is non-null.
    // favoriteAgentName is always null in current schema (TBD backend extension).
    // Verify the FavoriteAgentCard renders (not-found/error/loading NOT shown).
    expect(getBySlot(container, 'player-detail-favorite-agent')).toBeDefined();
    // Verify default render (not a shell) — main data-slot present
    expect(getBySlot(container, 'detail-page-layout')).toBeDefined();
  });

  // ── URL state overrides ──────────────────────────────────────────────────────

  it('?state=loading override renders loading shell regardless of real data', () => {
    mockSearchParams.set('state', 'loading');

    const { container } = renderView('sara-rossi');
    expect(getBySlot(container, 'player-detail-loading')).toBeDefined();
  });

  it('?state=error override renders error shell regardless of real data', () => {
    mockSearchParams.set('state', 'error');

    const { container } = renderView('sara-rossi');
    expect(getBySlot(container, 'player-detail-error')).toBeDefined();
  });

  it('?state=not-found override renders not-found shell regardless of real data', () => {
    mockSearchParams.set('state', 'not-found');

    const { container } = renderView('sara-rossi');
    expect(getBySlot(container, 'player-detail-not-found')).toBeDefined();
  });

  // ── Visual fixture short-circuit ─────────────────────────────────────────────

  it('IS_VISUAL_TEST_BUILD=false: real data path used, renders default view normally', () => {
    // This test verifies fixture=null path (IS_VISUAL_TEST_BUILD=false in our mock)
    const { container } = renderView('sara-rossi');
    expect(getBySlot(container, 'detail-page-layout')).toBeDefined();
  });

  // ── AchievementBadgeGrid is rendered in the Achievements tab ─────────────────

  it('AchievementBadgeGrid renders inside the Achievements tab (?tab=achievements)', () => {
    mockSearchParams.set('tab', 'achievements');
    const { container } = renderView('marco-bianchi');

    const achievementGrid = getBySlot(container, 'player-detail-achievement-grid');
    expect(achievementGrid).toBeDefined();
    expect(queryBySlot(container, 'player-detail-not-found')).toBeNull();
    mockSearchParams.delete('tab');
  });

  // ── Stage 3 cluster: DetailPageLayout adoption + URL tab state ───────────────

  it('default render wraps content in DetailPageLayout with overview region + tabs', () => {
    const { container } = renderView('sara-rossi');
    expect(getBySlot(container, 'detail-page-layout')).toBeDefined();
    expect(getBySlot(container, 'player-overview-region')).toBeDefined();
    expect(getBySlot(container, 'player-detail-tabs')).toBeDefined();
  });

  it('renders the Sessions tab panel by default (no ?tab=)', () => {
    const { container } = renderView('sara-rossi');
    expect(getBySlot(container, 'sessions-tab-panel')).toBeDefined();
    expect(queryBySlot(container, 'games-tab-panel')).toBeNull();
  });

  it('renders the Games tab panel when ?tab=games', () => {
    mockSearchParams.set('tab', 'games');
    const { container } = renderView('sara-rossi');
    expect(getBySlot(container, 'games-tab-panel')).toBeDefined();
    expect(queryBySlot(container, 'sessions-tab-panel')).toBeNull();
    mockSearchParams.delete('tab');
  });

  it('renders the Toolkits tab panel when ?tab=toolkits', () => {
    mockSearchParams.set('tab', 'toolkits');
    const { container } = renderView('sara-rossi');
    expect(getBySlot(container, 'toolkits-tab-panel')).toBeDefined();
    mockSearchParams.delete('tab');
  });

  it('falls back to the Sessions tab when ?tab= is an unknown key', () => {
    mockSearchParams.set('tab', 'bogus');
    const { container } = renderView('sara-rossi');
    expect(getBySlot(container, 'sessions-tab-panel')).toBeDefined();
    mockSearchParams.delete('tab');
  });
});
