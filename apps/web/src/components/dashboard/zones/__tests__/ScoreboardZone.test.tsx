/**
 * ScoreboardZone Tests
 *
 * Tests:
 * 1. Renders scoreboard-zone data-testid
 * 2. Shows empty state when no active session
 * 3. Renders scoreboard content when session is active (mock Scoreboard)
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type {
  LiveSessionDto,
  LiveSessionRoundScoreDto,
} from '@/lib/api/schemas/live-sessions.schemas';

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

// Mock the Scoreboard component to avoid rendering its internals
vi.mock('@/components/session/Scoreboard', () => ({
  Scoreboard: ({ data, isRealTime }: { data: unknown; isRealTime?: boolean }) => (
    <div data-testid="mock-scoreboard" data-real-time={isRealTime}>
      Scoreboard rendered
    </div>
  ),
}));

// Mock the adapters module
vi.mock('@/components/session/adapters', () => ({
  toScoreboardData: vi.fn(() => ({
    participants: [],
    scores: [],
    rounds: [],
    categories: [],
  })),
}));

// Mock the session store
const mockActiveSession: LiveSessionDto | null = null;
const mockScores: LiveSessionRoundScoreDto[] = [];

const mockUseSessionStore = vi.fn((selector: (state: Record<string, unknown>) => unknown) => {
  const state = {
    activeSession: mockActiveSession,
    scores: mockScores,
  };
  return selector(state);
});

vi.mock('@/lib/stores/sessionStore', () => ({
  useSessionStore: (selector: (state: Record<string, unknown>) => unknown) =>
    mockUseSessionStore(selector),
}));

// Import after mock setup
const { ScoreboardZone } = await import('../ScoreboardZone');

// ---------------------------------------------------------------------------
// Test data
// ---------------------------------------------------------------------------

function makeSession(overrides?: Partial<LiveSessionDto>): LiveSessionDto {
  return {
    id: 'session-1',
    sessionCode: 'ABC123',
    status: 'InProgress',
    gameId: 'game-1',
    gameName: 'Catan',
    gameImageUrl: null,
    createdAt: '2026-03-16T10:00:00Z',
    startedAt: '2026-03-16T10:05:00Z',
    completedAt: null,
    hostUserId: 'user-1',
    hostDisplayName: 'Host Player',
    location: null,
    notes: null,
    maxPlayers: 6,
    agentMode: 'None',
    players: [
      {
        id: 'player-1',
        userId: 'user-1',
        displayName: 'Alice',
        avatarUrl: null,
        color: 'Red',
        role: 'Host',
        teamId: null,
        totalScore: 10,
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
        totalScore: 7,
        currentRank: 2,
        joinedAt: '2026-03-16T10:01:00Z',
        isActive: true,
      },
    ],
    roundScores: [],
    tools: [],
    snapshots: [],
    ...overrides,
  } as LiveSessionDto;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('ScoreboardZone', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders scoreboard-zone data-testid', () => {
    mockUseSessionStore.mockImplementation(
      (selector: (state: Record<string, unknown>) => unknown) => {
        return selector({ activeSession: null, scores: [] });
      }
    );

    render(<ScoreboardZone />);
    expect(screen.getByTestId('scoreboard-zone')).toBeInTheDocument();
  });

  it('shows empty state when no active session', () => {
    mockUseSessionStore.mockImplementation(
      (selector: (state: Record<string, unknown>) => unknown) => {
        return selector({ activeSession: null, scores: [] });
      }
    );

    render(<ScoreboardZone />);

    expect(screen.getByTestId('scoreboard-zone-empty')).toBeInTheDocument();
    expect(screen.getByText('No active session')).toBeInTheDocument();
  });

  it('renders scoreboard content when session is active', () => {
    const session = makeSession();

    mockUseSessionStore.mockImplementation(
      (selector: (state: Record<string, unknown>) => unknown) => {
        return selector({ activeSession: session, scores: [] });
      }
    );

    render(<ScoreboardZone />);

    expect(screen.getByTestId('scoreboard-zone')).toBeInTheDocument();
    expect(screen.getByTestId('mock-scoreboard')).toBeInTheDocument();
    expect(screen.queryByTestId('scoreboard-zone-empty')).not.toBeInTheDocument();
  });
});
