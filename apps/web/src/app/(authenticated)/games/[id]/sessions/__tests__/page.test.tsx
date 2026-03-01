/**
 * Game Sessions Page Tests (Issue #4889)
 */

import { screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import GameSessionsPage from '../page';

const mockGetSessions = vi.hoisted(() => vi.fn());

const mockUseParams = vi.hoisted(() => vi.fn(() => ({ id: 'game-123' })));

vi.mock('next/navigation', () => ({
  useParams: mockUseParams,
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

vi.mock('@/lib/api', () => ({
  api: {
    games: {
      getSessions: mockGetSessions,
    },
  },
}));

const mockSessions = [
  {
    id: 'session-1',
    gameId: 'game-123',
    status: 'Completed',
    startedAt: '2026-01-10T10:00:00Z',
    completedAt: '2026-01-10T12:00:00Z',
    playerCount: 3,
    players: [],
    winnerName: 'Alice',
    notes: null,
    durationMinutes: 120,
  },
  {
    id: 'session-2',
    gameId: 'game-123',
    status: 'Completed',
    startedAt: '2026-01-15T14:00:00Z',
    completedAt: '2026-01-15T15:30:00Z',
    playerCount: 2,
    players: [],
    winnerName: null,
    notes: null,
    durationMinutes: 90,
  },
];

describe('GameSessionsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders session count', async () => {
    mockGetSessions.mockResolvedValue(mockSessions);

    renderWithQuery(<GameSessionsPage />);

    await waitFor(() => {
      expect(screen.getByText('2 Sessions')).toBeInTheDocument();
    });
  });

  it('shows winner name', async () => {
    mockGetSessions.mockResolvedValue(mockSessions);

    renderWithQuery(<GameSessionsPage />);

    await waitFor(() => {
      expect(screen.getByText('Alice')).toBeInTheDocument();
    });
  });

  it('shows page heading', async () => {
    mockGetSessions.mockResolvedValue(mockSessions);

    renderWithQuery(<GameSessionsPage />);

    await waitFor(() => {
      expect(screen.getByText('Sessions')).toBeInTheDocument();
    });
  });

  it('shows loading skeletons', () => {
    mockGetSessions.mockReturnValue(new Promise(() => {}));

    renderWithQuery(<GameSessionsPage />);

    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows error state', async () => {
    mockGetSessions.mockRejectedValue(new Error('Network error'));

    renderWithQuery(<GameSessionsPage />);

    await waitFor(() => {
      expect(screen.getByText(/Failed to load sessions/i)).toBeInTheDocument();
    });
  });

  it('shows empty state when no sessions', async () => {
    mockGetSessions.mockResolvedValue([]);

    renderWithQuery(<GameSessionsPage />);

    await waitFor(() => {
      expect(screen.getByText(/No sessions recorded/i)).toBeInTheDocument();
    });
  });
});
