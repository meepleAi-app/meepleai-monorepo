/**
 * Session Players Page Tests (Issue #4891)
 */

import { screen, waitFor } from '@testing-library/react';
import { useParams } from 'next/navigation';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import SessionPlayersPage from '../page';

const mockGetById = vi.hoisted(() => vi.fn());

vi.mock('next/navigation', () => ({
  useParams: vi.fn(),
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

vi.mock('@/lib/api', () => ({
  api: {
    sessions: {
      getById: mockGetById,
    },
  },
}));

const mockSession = {
  id: 'session-abc',
  gameId: 'game-xyz',
  status: 'Completed',
  startedAt: '2026-01-15T10:00:00Z',
  completedAt: '2026-01-15T12:00:00Z',
  playerCount: 2,
  winnerName: 'Alice',
  notes: null,
  durationMinutes: 120,
  players: [
    { playerName: 'Alice', playerOrder: 1, color: '#ff0000' },
    { playerName: 'Bob', playerOrder: 2, color: '#0000ff' },
  ],
};

describe('SessionPlayersPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (useParams as ReturnType<typeof vi.fn>).mockReturnValue({ id: 'session-abc' });
  });

  it('renders player list when session loads', async () => {
    mockGetById.mockResolvedValue(mockSession);

    renderWithQuery(<SessionPlayersPage />);

    await waitFor(() => {
      // Alice appears in player card and winner display - use getAllByText
      expect(screen.getAllByText('Alice').length).toBeGreaterThan(0);
    });

    expect(screen.getByText('Bob')).toBeInTheDocument();
  });

  it('shows winner badge on winning player', async () => {
    mockGetById.mockResolvedValue(mockSession);

    renderWithQuery(<SessionPlayersPage />);

    await waitFor(() => {
      expect(screen.getByText('Winner')).toBeInTheDocument();
    });
  });

  it('shows player count badge', async () => {
    mockGetById.mockResolvedValue(mockSession);

    renderWithQuery(<SessionPlayersPage />);

    await waitFor(() => {
      expect(screen.getByText('2')).toBeInTheDocument();
    });
  });

  it('shows empty state when no players', async () => {
    const emptySession = { ...mockSession, players: [], playerCount: 0 };
    mockGetById.mockResolvedValue(emptySession);

    renderWithQuery(<SessionPlayersPage />);

    await waitFor(() => {
      expect(screen.getByText(/No players recorded/i)).toBeInTheDocument();
    });
  });

  it('shows error message on failure', async () => {
    mockGetById.mockRejectedValue(new Error('Network error'));

    renderWithQuery(<SessionPlayersPage />);

    await waitFor(() => {
      expect(screen.getByText('Network error')).toBeInTheDocument();
    });
  });

  it('shows loading state initially', async () => {
    // Return a promise that never resolves to keep loading state
    mockGetById.mockReturnValue(new Promise(() => {}));

    renderWithQuery(<SessionPlayersPage />);

    // During loading, skeletons should be visible
    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });
});
