/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { SessionBar } from '../SessionBar';

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

const mockSessionStore = vi.fn();

vi.mock('@/lib/stores/sessionStore', () => ({
  useSessionStore: (selector: (s: unknown) => unknown) => selector(mockSessionStore()),
}));

const mockCascadeStore = vi.fn();

vi.mock('@/lib/stores/cascadeNavigationStore', () => ({
  useCascadeNavigationStore: (selector: (s: unknown) => unknown) => selector(mockCascadeStore()),
}));

const mockUseGame = vi.fn();

vi.mock('@/hooks/queries/useGames', () => ({
  useGame: (...args: unknown[]) => mockUseGame(...args),
}));

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });
}

function renderWithProviders(ui: React.ReactElement) {
  const queryClient = createQueryClient();
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

// ---------------------------------------------------------------------------
// Test data
// ---------------------------------------------------------------------------

const MOCK_SESSION = {
  id: 'session-1',
  sessionCode: 'ABC123',
  gameId: 'game-1',
  gameName: 'Catan',
  createdByUserId: 'user-1',
  status: 'InProgress',
  visibility: 'Private',
  groupId: null,
  createdAt: '2026-03-16T10:00:00Z',
  startedAt: '2026-03-16T10:05:00Z',
  pausedAt: null,
  completedAt: null,
  updatedAt: '2026-03-16T10:30:00Z',
  lastSavedAt: null,
  currentTurnIndex: 3,
  currentTurnPlayerId: 'player-1',
  agentMode: 'None',
  chatSessionId: null,
  notes: null,
  players: [
    {
      id: 'player-1',
      userId: 'user-1',
      displayName: 'Alice',
      avatarUrl: null,
      color: 'Red',
      role: 'Host',
      teamId: null,
      totalScore: 8,
      currentRank: 1,
      joinedAt: '2026-03-16T10:00:00Z',
      isActive: true,
    },
    {
      id: 'player-2',
      userId: 'user-2',
      displayName: 'Bob',
      avatarUrl: null,
      color: 'Blue',
      role: 'Player',
      teamId: null,
      totalScore: 6,
      currentRank: 2,
      joinedAt: '2026-03-16T10:00:00Z',
      isActive: true,
    },
    {
      id: 'player-3',
      userId: 'user-3',
      displayName: 'Charlie',
      avatarUrl: null,
      color: 'Green',
      role: 'Player',
      teamId: null,
      totalScore: 4,
      currentRank: 3,
      joinedAt: '2026-03-16T10:01:00Z',
      isActive: true,
    },
  ],
  teams: [],
  roundScores: [],
  scoringConfig: {
    enabledDimensions: ['points'],
    dimensionUnits: { points: 'VP' },
  },
};

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('SessionBar', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockCascadeStore.mockReturnValue({ openDeckStack: vi.fn() });
    mockUseGame.mockReturnValue({ data: null });
  });

  it('renders game name from session store', () => {
    mockSessionStore.mockReturnValue({
      activeSession: MOCK_SESSION,
      isLoading: false,
    });

    renderWithProviders(<SessionBar />);

    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('has dashboard-hero class', () => {
    mockSessionStore.mockReturnValue({
      activeSession: MOCK_SESSION,
      isLoading: false,
    });

    renderWithProviders(<SessionBar />);

    const bar = screen.getByTestId('session-bar');
    expect(bar.className).toContain('dashboard-hero');
  });

  it('has session-bar data-testid', () => {
    mockSessionStore.mockReturnValue({
      activeSession: MOCK_SESSION,
      isLoading: false,
    });

    renderWithProviders(<SessionBar />);

    expect(screen.getByTestId('session-bar')).toBeInTheDocument();
  });

  it('shows live dot', () => {
    mockSessionStore.mockReturnValue({
      activeSession: MOCK_SESSION,
      isLoading: false,
    });

    renderWithProviders(<SessionBar />);

    const liveDot = screen.getByLabelText('Live session');
    expect(liveDot).toBeInTheDocument();
    expect(liveDot.className).toContain('live-dot');
  });

  it('renders mana pip row with 3 pips', () => {
    mockSessionStore.mockReturnValue({
      activeSession: MOCK_SESSION,
      isLoading: false,
    });

    renderWithProviders(<SessionBar />);

    expect(screen.getByTestId('mana-pip-row')).toBeInTheDocument();
    expect(screen.getByTestId('mana-pip-kb')).toBeInTheDocument();
    expect(screen.getByTestId('mana-pip-agent')).toBeInTheDocument();
    expect(screen.getByTestId('mana-pip-player')).toBeInTheDocument();
  });

  it('shows skeleton when loading', () => {
    mockSessionStore.mockReturnValue({
      activeSession: null,
      isLoading: true,
    });

    renderWithProviders(<SessionBar />);

    expect(screen.getByTestId('session-bar-skeleton')).toBeInTheDocument();
    expect(screen.queryByTestId('session-bar')).not.toBeInTheDocument();
  });

  it('skeleton has dashboard-hero class', () => {
    mockSessionStore.mockReturnValue({
      activeSession: null,
      isLoading: true,
    });

    renderWithProviders(<SessionBar />);

    const skeleton = screen.getByTestId('session-bar-skeleton');
    expect(skeleton.className).toContain('dashboard-hero');
  });

  it('renders null when no session and not loading', () => {
    mockSessionStore.mockReturnValue({
      activeSession: null,
      isLoading: false,
    });

    const { container } = renderWithProviders(<SessionBar />);

    expect(container.innerHTML).toBe('');
  });

  it('uses game title from useGame when available', () => {
    mockSessionStore.mockReturnValue({
      activeSession: MOCK_SESSION,
      isLoading: false,
    });
    mockUseGame.mockReturnValue({ data: { title: 'Settlers of Catan (Full)' } });

    renderWithProviders(<SessionBar />);

    expect(screen.getByText('Settlers of Catan (Full)')).toBeInTheDocument();
  });

  it('calls openDeckStack when mana pip is clicked', async () => {
    const mockOpenDeckStack = vi.fn();
    mockCascadeStore.mockReturnValue({ openDeckStack: mockOpenDeckStack });
    mockSessionStore.mockReturnValue({
      activeSession: MOCK_SESSION,
      isLoading: false,
    });

    renderWithProviders(<SessionBar />);

    const kbPip = screen.getByTestId('mana-pip-kb');
    kbPip.click();

    expect(mockOpenDeckStack).toHaveBeenCalledWith('kb', 'session-1');
  });
});
