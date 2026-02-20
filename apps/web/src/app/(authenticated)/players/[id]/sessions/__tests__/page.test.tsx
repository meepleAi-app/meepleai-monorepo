/**
 * Player Sessions Page Tests (Issue #4890)
 */

import { screen, waitFor } from '@testing-library/react';
import { useParams } from 'next/navigation';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import PlayerSessionsPage from '../page';

const mockGetHistory = vi.hoisted(() => vi.fn());

vi.mock('next/navigation', () => ({
  useParams: vi.fn(),
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

vi.mock('@/lib/api', () => ({
  api: {
    sessions: {
      getHistory: mockGetHistory,
    },
  },
}));

const mockSessions = [
  {
    id: 'session-1',
    gameId: 'game-1',
    status: 'Completed',
    startedAt: '2026-01-15T10:00:00Z',
    completedAt: '2026-01-15T12:00:00Z',
    playerCount: 2,
    winnerName: 'Alice',
    notes: null,
    durationMinutes: 120,
    players: [
      { playerName: 'Alice', playerOrder: 1, color: null },
      { playerName: 'Bob', playerOrder: 2, color: null },
    ],
  },
  {
    id: 'session-2',
    gameId: 'game-2',
    status: 'Completed',
    startedAt: '2026-01-20T14:00:00Z',
    completedAt: '2026-01-20T16:00:00Z',
    playerCount: 3,
    winnerName: 'Charlie',
    notes: null,
    durationMinutes: 90,
    players: [
      { playerName: 'Alice', playerOrder: 1, color: null },
      { playerName: 'Charlie', playerOrder: 2, color: null },
      { playerName: 'Dave', playerOrder: 3, color: null },
    ],
  },
];

describe('PlayerSessionsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (useParams as ReturnType<typeof vi.fn>).mockReturnValue({ id: 'alice' });
  });

  it('renders sessions for this player', async () => {
    mockGetHistory.mockResolvedValue({ sessions: mockSessions, total: 2, page: 1, pageSize: 50 });

    renderWithQuery(<PlayerSessionsPage />);

    await waitFor(() => {
      expect(screen.getByText('2')).toBeInTheDocument(); // badge count
    });
  });

  it('shows page heading', async () => {
    mockGetHistory.mockResolvedValue({ sessions: mockSessions, total: 2, page: 1, pageSize: 50 });

    renderWithQuery(<PlayerSessionsPage />);

    await waitFor(() => {
      expect(screen.getByText('Sessions')).toBeInTheDocument();
    });
  });

  it('filters sessions by player name', async () => {
    const noAliceSessions = [{ ...mockSessions[0], players: [{ playerName: 'Bob', playerOrder: 1, color: null }] }];
    mockGetHistory.mockResolvedValue({ sessions: noAliceSessions, total: 1, page: 1, pageSize: 50 });

    renderWithQuery(<PlayerSessionsPage />);

    await waitFor(() => {
      expect(screen.getByText(/No sessions found/i)).toBeInTheDocument();
    });
  });

  it('shows empty state when no sessions', async () => {
    mockGetHistory.mockResolvedValue({ sessions: [], total: 0, page: 1, pageSize: 50 });

    renderWithQuery(<PlayerSessionsPage />);

    await waitFor(() => {
      expect(screen.getByText(/No sessions found/i)).toBeInTheDocument();
    });
  });

  it('shows error state on API failure', async () => {
    mockGetHistory.mockRejectedValue(new Error('Network error'));

    renderWithQuery(<PlayerSessionsPage />);

    await waitFor(() => {
      expect(screen.getByText('Network error')).toBeInTheDocument();
    });
  });

  it('shows loading skeletons', () => {
    mockGetHistory.mockReturnValue(new Promise(() => {}));

    renderWithQuery(<PlayerSessionsPage />);

    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });
});
