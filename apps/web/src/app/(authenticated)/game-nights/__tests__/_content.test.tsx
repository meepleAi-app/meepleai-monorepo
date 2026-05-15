/**
 * GameNightsContent — Stage 3 orchestrator smoke tests (Issue #1170 commit 3).
 *
 * Exercises the v2 orchestrator (calendar/list views, filter pills, drawer,
 * empty + error branches). Pure-presentational components are covered by
 * their own unit tests; this suite verifies wiring + state branching.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { GameNightDto } from '@/lib/api/schemas/game-nights.schemas';

// ─── Mocks ────────────────────────────────────────────────────────────────

const replaceMock = vi.fn();
const pushMock = vi.fn();
const searchParamsState: Record<string, string | null> = {};

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: replaceMock, push: pushMock }),
  useSearchParams: () => ({
    get: (k: string) => searchParamsState[k] ?? null,
    toString: () =>
      Object.entries(searchParamsState)
        .filter(([, v]) => v !== null && v !== undefined)
        .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`)
        .join('&'),
  }),
}));

// Identity translator returning the key (so we can assert by key).
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (k: string, _values?: Record<string, unknown>) => k,
  }),
}));

const useUpcomingMock = vi.fn();
const useMineMock = vi.fn();

vi.mock('@/hooks/queries/useGameNights', () => ({
  useUpcomingGameNights: () => useUpcomingMock(),
  useMyGameNights: () => useMineMock(),
}));

const useCurrentUserMock = vi.fn();

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => useCurrentUserMock(),
}));

// Recents store no-op to avoid persistence side effects.
vi.mock('@/stores/use-recents', () => ({
  useRecentsStore: {
    getState: () => ({ push: vi.fn() }),
  },
}));

// ─── Import under test (after mocks) ──────────────────────────────────────

import { GameNightsContent } from '../_content';

// ─── Fixtures ──────────────────────────────────────────────────────────────

const VIEWER_ID = '11111111-1111-1111-1111-111111111111';

function makeDto(overrides: Partial<GameNightDto> = {}): GameNightDto {
  const now = new Date();
  // Schedule "today" so the calendar grid will surface it under any month context.
  const scheduledAt = new Date(
    now.getFullYear(),
    now.getMonth(),
    now.getDate(),
    20,
    30,
    0
  ).toISOString();
  return {
    id: '22222222-2222-2222-2222-222222222222',
    organizerId: VIEWER_ID,
    organizerName: 'Marco R.',
    title: 'Serata Catan',
    description: null,
    scheduledAt,
    location: 'Casa Marco',
    maxPlayers: 6,
    gameIds: [],
    status: 'Published',
    acceptedCount: 3,
    pendingCount: 0,
    totalInvited: 3,
    createdAt: new Date().toISOString(),
    updatedAt: null,
    ...overrides,
  };
}

// ─── Setup ────────────────────────────────────────────────────────────────

beforeEach(() => {
  vi.clearAllMocks();
  for (const k of Object.keys(searchParamsState)) delete searchParamsState[k];

  useCurrentUserMock.mockReturnValue({ data: { id: VIEWER_ID } });
});

// ─── Tests ────────────────────────────────────────────────────────────────

describe('GameNightsContent (orchestrator)', () => {
  it('renders the header + calendar view by default when events exist', () => {
    const dto = makeDto({ title: 'Serata Catan' });
    const other = makeDto({
      id: '33333333-3333-3333-3333-333333333333',
      organizerId: 'someone-else-id-00000000000000000000',
      title: 'Serata Risk',
    });
    useUpcomingMock.mockReturnValue({
      data: [dto, other],
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    });
    useMineMock.mockReturnValue({
      data: [dto],
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    });

    render(<GameNightsContent />);

    // Header pulls translation keys (identity translator).
    expect(screen.getByText('gameNightsIndex.header.title')).toBeInTheDocument();
    // Calendar grid is the default view.
    expect(screen.getByTestId('game-nights-calendar-month-grid')).toBeInTheDocument();
    // The empty/list/error testids must NOT be present.
    expect(screen.queryByTestId('game-nights-empty')).not.toBeInTheDocument();
    expect(screen.queryByTestId('game-nights-list')).not.toBeInTheDocument();
    expect(screen.queryByTestId('game-nights-error')).not.toBeInTheDocument();
  });

  it('renders the empty state when both queries return zero events', () => {
    useUpcomingMock.mockReturnValue({
      data: [],
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    });
    useMineMock.mockReturnValue({
      data: [],
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    });

    render(<GameNightsContent />);

    expect(screen.getByTestId('game-nights-empty')).toBeInTheDocument();
    expect(screen.getByText('gameNightsIndex.states.empty.title')).toBeInTheDocument();
    expect(screen.getByText('gameNightsIndex.states.empty.cta')).toBeInTheDocument();
    // Calendar grid must not render.
    expect(screen.queryByTestId('game-nights-calendar-month-grid')).not.toBeInTheDocument();
  });

  it('renders the error state with a retry button when both queries error', () => {
    const upcomingRefetch = vi.fn();
    const mineRefetch = vi.fn();
    useUpcomingMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: new Error('boom'),
      refetch: upcomingRefetch,
    });
    useMineMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: new Error('boom'),
      refetch: mineRefetch,
    });

    render(<GameNightsContent />);

    expect(screen.getByTestId('game-nights-error')).toBeInTheDocument();
    const retry = screen.getByRole('button', { name: 'gameNightsIndex.states.error.retry' });
    fireEvent.click(retry);
    expect(upcomingRefetch).toHaveBeenCalledTimes(1);
    expect(mineRefetch).toHaveBeenCalledTimes(1);
  });

  it('reads ?view=list from the URL and renders the list view', () => {
    searchParamsState.view = 'list';
    const dto = makeDto();
    useUpcomingMock.mockReturnValue({
      data: [dto],
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    });
    useMineMock.mockReturnValue({
      data: [],
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    });

    render(<GameNightsContent />);

    expect(screen.getByTestId('game-nights-list')).toBeInTheDocument();
    expect(screen.queryByTestId('game-nights-calendar-month-grid')).not.toBeInTheDocument();
    // The list-card primitive should render at least one row.
    expect(screen.getAllByTestId('game-nights-list-card').length).toBeGreaterThanOrEqual(1);
  });

  it('updates the URL when the user switches view via the header tablist', () => {
    const dto = makeDto();
    useUpcomingMock.mockReturnValue({
      data: [dto],
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    });
    useMineMock.mockReturnValue({
      data: [],
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    });

    render(<GameNightsContent />);

    const listTab = screen.getByRole('tab', { name: 'gameNightsIndex.view.list' });
    fireEvent.click(listTab);

    expect(replaceMock).toHaveBeenCalled();
    const target = String(replaceMock.mock.calls[0]?.[0] ?? '');
    expect(target).toMatch(/view=list/);
  });

  it('filters to organizing events when the "Organizzo" filter is clicked', () => {
    const mine = makeDto({ organizerId: VIEWER_ID, title: 'Mia serata' });
    const others = makeDto({
      id: '44444444-4444-4444-4444-444444444444',
      organizerId: 'someone-else-id-00000000000000000000',
      title: 'Serata altrui',
    });
    useUpcomingMock.mockReturnValue({
      data: [mine, others],
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    });
    useMineMock.mockReturnValue({
      data: [mine],
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    });

    render(<GameNightsContent />);

    const organizing = screen.getByRole('button', { name: 'gameNightsIndex.filter.organizing' });
    fireEvent.click(organizing);

    expect(replaceMock).toHaveBeenCalled();
    const target = String(replaceMock.mock.calls[0]?.[0] ?? '');
    expect(target).toMatch(/filter=organizing/);
  });
});
