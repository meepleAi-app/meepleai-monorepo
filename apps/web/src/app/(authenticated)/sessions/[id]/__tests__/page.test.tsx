import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

// Mock React.use so params resolves synchronously
vi.mock('react', async () => {
  const actual = await vi.importActual<typeof import('react')>('react');
  return { ...actual, use: (v: unknown) => v };
});

// Session store mock — returns a loaded session by default
const mockSessionStore = {
  activeSession: null as Record<string, unknown> | null,
  scores: [] as unknown[],
  isLoading: false,
  error: null as string | null,
  loadScores: vi.fn(),
  pauseSession: vi.fn(),
  resumeSession: vi.fn(),
  completeSession: vi.fn(),
  handleSessionUpdate: vi.fn(),
};

vi.mock('@/lib/stores/session-store', () => ({
  useSessionStore: (selector: (s: typeof mockSessionStore) => unknown) =>
    selector(mockSessionStore),
}));

vi.mock('@/lib/domain-hooks/useSessionSync', () => ({
  useSessionSync: () => ({ isConnected: true }),
}));

vi.mock('@/components/session', () => ({
  LiveIndicator: () => <div data-testid="live-indicator" />,
  Scoreboard: () => <div data-testid="scoreboard" />,
  SessionHeader: () => <div data-testid="session-header" />,
  SessionParticipantsList: () => <div data-testid="session-participants" />,
  SessionDiaryTimeline: () => <div data-testid="session-diary" />,
  SessionQuickActions: () => <div data-testid="session-quick-actions" />,
}));

vi.mock('@/components/session/adapters', () => ({
  toScoreboardData: () => ({ players: [], rounds: [] }),
  toSession: () => ({
    id: 'session-1',
    title: 'Test Session',
    status: 'InProgress',
    gameName: 'Catan',
  }),
}));

vi.mock('@/components/ui/data-display/entity-link/related-entities-section', () => ({
  RelatedEntitiesSection: () => <div data-testid="related-entities" />,
}));

vi.mock('@/hooks/useConnectionBarNav', () => ({
  useConnectionBarNav: () => ({ handlePipClick: vi.fn() }),
}));

vi.mock('@/components/ui/data-display/connection-bar', () => ({
  ConnectionBar: ({ connections }: { connections: Array<unknown> }) =>
    connections.length > 0 ? <div data-testid="connection-bar" /> : null,
  buildSessionConnections: (counts: Record<string, number>) =>
    Object.values(counts).some(v => v > 0) ? [{ count: 1 }] : [],
}));

const SESSION_ID = '00000000-0000-4000-8000-000000000001';

function makeActiveSession(overrides: Record<string, unknown> = {}) {
  return {
    id: SESSION_ID,
    sessionCode: 'ABC123',
    gameId: 'game-1',
    gameName: 'Catan',
    createdByUserId: 'user-1',
    status: 'InProgress',
    visibility: 'Public',
    groupId: null,
    createdAt: '2025-01-01T00:00:00Z',
    startedAt: '2025-01-01T00:00:00Z',
    pausedAt: null,
    completedAt: null,
    updatedAt: '2025-01-01T00:00:00Z',
    lastSavedAt: null,
    currentTurnIndex: 0,
    currentTurnPlayerId: null,
    agentMode: 'None',
    chatSessionId: null,
    notes: null,
    players: [
      {
        id: 'player-1',
        displayName: 'Alice',
        color: '#ff0000',
        role: 'Player',
        totalScore: 10,
        currentRank: 1,
        isActive: true,
      },
    ],
    teams: [],
    roundScores: [],
    scoringConfig: { mode: 'Points' },
    ...overrides,
  };
}

import SessionScoreboardPage from '../page';

describe('SessionScoreboardPage', () => {
  beforeEach(() => {
    mockSessionStore.activeSession = null;
    mockSessionStore.scores = [];
    mockSessionStore.isLoading = false;
    mockSessionStore.error = null;
  });

  it('renders nothing when isLoading and no session', () => {
    mockSessionStore.isLoading = true;
    const { container } = render(
      <SessionScoreboardPage params={Promise.resolve({ id: SESSION_ID })} />
    );
    // Loading spinner shown, no session content
    expect(screen.queryByTestId('session-header')).not.toBeInTheDocument();
    expect(container.firstChild).not.toBeNull();
  });

  it('renders session header when session is loaded', () => {
    mockSessionStore.activeSession = makeActiveSession();
    render(<SessionScoreboardPage params={Promise.resolve({ id: SESSION_ID })} />);
    expect(screen.getByTestId('session-header')).toBeInTheDocument();
  });

  it('renders connection-bar when session has players and game', () => {
    mockSessionStore.activeSession = makeActiveSession();
    render(<SessionScoreboardPage params={Promise.resolve({ id: SESSION_ID })} />);
    expect(screen.getByTestId('connection-bar')).toBeInTheDocument();
  });

  it('does not render connection-bar when session has no game and no players', () => {
    mockSessionStore.activeSession = makeActiveSession({ gameId: null, players: [] });
    render(<SessionScoreboardPage params={Promise.resolve({ id: SESSION_ID })} />);
    expect(screen.queryByTestId('connection-bar')).not.toBeInTheDocument();
  });
});
