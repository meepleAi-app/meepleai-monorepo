/**
 * ToolkitHistoryPage — orchestrator smoke tests (Issue #3006, Task A9).
 *
 * Renders the real i18n catalog via `renderWithQuery` (EN messages, matching
 * the established `@/__tests__/utils/query-test-utils` pattern used across
 * the app) so assertions exercise the actual translation ids, not a
 * passthrough mock. `api.sessions.getHistory` and `useLibrary` are mocked;
 * `next/navigation`'s `useRouter` is mocked to a `push` spy since the empty
 * state CTA navigates to `/toolkit`.
 */

import { screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import ToolkitHistoryPage from '../client';

const mockGetHistory = vi.hoisted(() => vi.fn());
const mockUseLibrary = vi.hoisted(() => vi.fn());
const pushSpy = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    sessions: {
      getHistory: mockGetHistory,
    },
  },
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibrary: mockUseLibrary,
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: pushSpy }),
}));

const LIBRARY_DATA = {
  items: [
    { gameId: 'game-1', gameTitle: 'Wingspan' },
    { gameId: 'game-2', gameTitle: 'Pandemic' },
  ],
  page: 1,
  pageSize: 500,
  totalCount: 2,
};

const SESSION_1 = {
  id: 'session-1',
  gameId: 'game-1',
  status: 'Completed',
  startedAt: '2026-07-10T14:30:00Z',
  completedAt: '2026-07-10T16:05:00Z',
  playerCount: 2,
  players: [
    { playerName: 'Alice', playerOrder: 0, color: null },
    { playerName: 'Bob', playerOrder: 1, color: null },
  ],
  winnerName: 'Alice',
  notes: 'Great game!',
  durationMinutes: 95,
  scoringType: null,
  scoreData: JSON.stringify({ Alice: 42, Bob: 30 }),
  turnOrderType: null,
};

const SESSION_2 = {
  id: 'session-2',
  gameId: 'game-2',
  status: 'Completed',
  startedAt: '2026-07-05T10:00:00Z',
  completedAt: '2026-07-05T11:00:00Z',
  playerCount: 2,
  players: [
    { playerName: 'Cara', playerOrder: 0, color: null },
    { playerName: 'Dan', playerOrder: 1, color: null },
  ],
  winnerName: null,
  notes: null,
  durationMinutes: 60,
  scoringType: null,
  scoreData: null,
  turnOrderType: null,
};

function mockHistoryResponse(sessions: unknown[]) {
  mockGetHistory.mockResolvedValue({
    sessions,
    total: sessions.length,
    page: 1,
    pageSize: 500,
  });
}

describe('ToolkitHistoryPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseLibrary.mockReturnValue({ data: LIBRARY_DATA, isLoading: false, isError: false });
  });

  it('renders the localized page title', async () => {
    mockHistoryResponse([SESSION_1, SESSION_2]);
    renderWithQuery(<ToolkitHistoryPage />);

    expect(await screen.findByRole('heading', { name: 'Session history' })).toBeInTheDocument();
  });

  it('renders a table row for each fetched session', async () => {
    mockHistoryResponse([SESSION_1, SESSION_2]);
    renderWithQuery(<ToolkitHistoryPage />);

    await waitFor(() => {
      expect(screen.getAllByRole('row')).toHaveLength(3); // 1 header + 2 sessions
    });
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('Pandemic')).toBeInTheDocument();
  });

  it('shows the empty state when there are no sessions', async () => {
    mockHistoryResponse([]);
    renderWithQuery(<ToolkitHistoryPage />);

    expect(await screen.findByText('No sessions yet')).toBeInTheDocument();
  });

  it('shows the error state and retries when the query rejects', async () => {
    mockGetHistory.mockRejectedValue(new Error('network down'));
    renderWithQuery(<ToolkitHistoryPage />);

    expect(await screen.findByRole('alert')).toHaveTextContent('Unable to load history');
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument();
  });
});
