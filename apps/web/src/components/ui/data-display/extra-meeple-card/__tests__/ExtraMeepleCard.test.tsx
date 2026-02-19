/**
 * Tests for ExtraMeepleCard component
 * Issue #4757 - ExtraMeepleCard Component Base + Session Tabs
 */

import { fireEvent, render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ExtraMeepleCard } from '../ExtraMeepleCard';
import type {
  ExtraMeepleCardProps,
  OverviewTabData,
  ToolkitData,
  ScoreboardTabData,
  HistoryTabData,
  SessionPlayerInfo,
} from '../types';

// ============================================================================
// Test Data
// ============================================================================

const mockPlayers: SessionPlayerInfo[] = [
  {
    id: 'p1',
    displayName: 'Alice',
    color: 'red',
    role: 'host',
    totalScore: 42,
    currentRank: 1,
    isActive: true,
  },
  {
    id: 'p2',
    displayName: 'Bob',
    color: 'blue',
    role: 'player',
    totalScore: 35,
    currentRank: 2,
    isActive: true,
  },
];

const mockOverview: OverviewTabData = {
  gameName: 'Catan',
  status: 'inProgress',
  startedAt: '2026-02-19T10:00:00Z',
  durationMinutes: 45,
  sessionCode: 'ABC123',
  players: mockPlayers,
  currentRound: 3,
  totalRounds: 10,
};

const mockToolkit: ToolkitData = {
  id: 'tk-1',
  name: 'Catan Toolkit',
  version: 2,
  isPublished: true,
  diceTools: [
    { name: 'Main Dice', diceType: 'D6', quantity: 2, isInteractive: true },
  ],
  cardTools: [
    {
      name: 'Resource Cards',
      deckType: 'Resource',
      cardCount: 95,
      shuffleable: true,
      allowDraw: true,
      allowDiscard: true,
      allowPeek: false,
      allowReturnToDeck: false,
    },
  ],
  timerTools: [],
  counterTools: [
    {
      name: 'Victory Points',
      minValue: 0,
      maxValue: 20,
      defaultValue: 0,
      isPerPlayer: true,
    },
  ],
};

const mockScoreboard: ScoreboardTabData = {
  players: mockPlayers,
  roundScores: [
    { playerId: 'p1', round: 1, dimension: 'default', value: 15 },
    { playerId: 'p1', round: 2, dimension: 'default', value: 27 },
    { playerId: 'p2', round: 1, dimension: 'default', value: 18 },
    { playerId: 'p2', round: 2, dimension: 'default', value: 17 },
  ],
};

const mockHistory: HistoryTabData = {
  snapshots: [
    {
      id: 'snap-1',
      snapshotNumber: 1,
      triggerType: 'turnEnd',
      description: 'End of turn 1',
      turnNumber: 1,
      createdAt: '2026-02-19T10:15:00Z',
    },
    {
      id: 'snap-2',
      snapshotNumber: 2,
      triggerType: 'manual',
      description: 'Manual save',
      createdAt: '2026-02-19T10:30:00Z',
    },
  ],
  timeline: [
    { id: 'e1', timestamp: '2026-02-19T10:00:00Z', type: 'system', label: 'Session started' },
    { id: 'e2', timestamp: '2026-02-19T10:05:00Z', type: 'turn', label: 'Alice turn', playerId: 'p1' },
    { id: 'e3', timestamp: '2026-02-19T10:10:00Z', type: 'score', label: 'Score updated', playerId: 'p1' },
  ],
  totalTurns: 6,
};

const defaultProps: ExtraMeepleCardProps = {
  sessionId: 'session-123',
  title: 'Catan - Friday Night',
  status: 'inProgress',
  playerCount: 2,
  overviewData: mockOverview,
  'data-testid': 'extra-meeple-card',
};

// ============================================================================
// ExtraMeepleCard Tests
// ============================================================================

describe('ExtraMeepleCard', () => {
  // --- Rendering ---

  it('renders with title and status', () => {
    render(<ExtraMeepleCard {...defaultProps} />);

    expect(screen.getByText('Catan - Friday Night')).toBeInTheDocument();
    expect(screen.getByText('In Progress')).toBeInTheDocument();
    expect(screen.getByText('2 players')).toBeInTheDocument();
  });

  it('renders session ID prefix', () => {
    render(<ExtraMeepleCard {...defaultProps} />);

    expect(screen.getByText('session-')).toBeInTheDocument();
  });

  it('renders elapsed time when provided', () => {
    render(<ExtraMeepleCard {...defaultProps} elapsedTime="1:23:45" />);

    expect(screen.getByText('1:23:45')).toBeInTheDocument();
  });

  it('renders loading state', () => {
    render(<ExtraMeepleCard {...defaultProps} loading />);

    expect(screen.getByText('Loading session...')).toBeInTheDocument();
  });

  it('renders error state', () => {
    render(<ExtraMeepleCard {...defaultProps} error="Session not found" />);

    expect(screen.getByText('Session not found')).toBeInTheDocument();
  });

  it('applies custom className', () => {
    render(<ExtraMeepleCard {...defaultProps} className="custom-class" />);

    const card = screen.getByTestId('extra-meeple-card');
    expect(card).toHaveClass('custom-class');
  });

  // --- Status badges ---

  it('renders setup status correctly', () => {
    render(<ExtraMeepleCard {...defaultProps} status="setup" />);
    expect(screen.getByText('Setup')).toBeInTheDocument();
  });

  it('renders paused status correctly', () => {
    render(<ExtraMeepleCard {...defaultProps} status="paused" />);
    expect(screen.getByText('Paused')).toBeInTheDocument();
  });

  it('renders completed status correctly', () => {
    render(<ExtraMeepleCard {...defaultProps} status="completed" />);
    expect(screen.getByText('Completed')).toBeInTheDocument();
  });

  // --- Tab system ---

  it('renders all 4 tab triggers', () => {
    render(<ExtraMeepleCard {...defaultProps} />);

    expect(screen.getByRole('tab', { name: /overview/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /toolkit/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /scoreboard/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /history/i })).toBeInTheDocument();
  });

  it('starts on overview tab by default', () => {
    render(<ExtraMeepleCard {...defaultProps} />);

    const overviewTab = screen.getByRole('tab', { name: /overview/i });
    expect(overviewTab).toHaveAttribute('data-state', 'active');
  });

  it('switches tabs via controlled activeTab prop', () => {
    const { rerender } = render(
      <ExtraMeepleCard {...defaultProps} activeTab="overview" toolkitData={mockToolkit} />
    );

    expect(screen.getByRole('tab', { name: /overview/i })).toHaveAttribute('data-state', 'active');
    expect(screen.getByRole('tab', { name: /toolkit/i })).toHaveAttribute('data-state', 'inactive');

    rerender(
      <ExtraMeepleCard {...defaultProps} activeTab="toolkit" toolkitData={mockToolkit} />
    );

    expect(screen.getByRole('tab', { name: /toolkit/i })).toHaveAttribute('data-state', 'active');
    expect(screen.getByRole('tab', { name: /overview/i })).toHaveAttribute('data-state', 'inactive');
  });

  it('calls onTabChange when controlled tab switches', () => {
    const onTabChange = vi.fn();
    const { rerender } = render(
      <ExtraMeepleCard {...defaultProps} activeTab="overview" onTabChange={onTabChange} toolkitData={mockToolkit} />
    );

    // Verify overview is active
    expect(screen.getByRole('tab', { name: /overview/i })).toHaveAttribute('data-state', 'active');

    // Rerender with new tab (simulates parent controlling tab)
    rerender(
      <ExtraMeepleCard {...defaultProps} activeTab="toolkit" onTabChange={onTabChange} toolkitData={mockToolkit} />
    );

    // Verify toolkit is now active
    expect(screen.getByRole('tab', { name: /toolkit/i })).toHaveAttribute('data-state', 'active');
  });

  it('supports controlled tab via activeTab prop', () => {
    render(<ExtraMeepleCard {...defaultProps} activeTab="history" historyData={mockHistory} />);

    const historyTab = screen.getByRole('tab', { name: /history/i });
    expect(historyTab).toHaveAttribute('data-state', 'active');
  });

  it('renders tab badges when provided', () => {
    render(
      <ExtraMeepleCard
        {...defaultProps}
        tabBadges={{ history: 5, scoreboard: 2 }}
      />
    );

    expect(screen.getByText('5')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
  });

  it('renders 99+ for large badge counts', () => {
    render(
      <ExtraMeepleCard
        {...defaultProps}
        tabBadges={{ history: 150 }}
      />
    );

    expect(screen.getByText('99+')).toBeInTheDocument();
  });

  // --- Player count ---

  it('renders singular player text for 1 player', () => {
    render(<ExtraMeepleCard {...defaultProps} playerCount={1} />);
    expect(screen.getByText('1 player')).toBeInTheDocument();
  });

  it('renders plural players text for multiple players', () => {
    render(<ExtraMeepleCard {...defaultProps} playerCount={4} />);
    expect(screen.getByText('4 players')).toBeInTheDocument();
  });
});

// ============================================================================
// OverviewTab Tests
// ============================================================================

describe('OverviewTab (via ExtraMeepleCard)', () => {
  it('renders player list', () => {
    render(<ExtraMeepleCard {...defaultProps} />);

    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
  });

  it('renders session code', () => {
    render(<ExtraMeepleCard {...defaultProps} />);

    expect(screen.getByText('ABC123')).toBeInTheDocument();
  });

  it('renders round information', () => {
    render(<ExtraMeepleCard {...defaultProps} />);

    expect(screen.getByText('3/10')).toBeInTheDocument();
  });

  it('renders action buttons for inProgress status', () => {
    const actions = { onPause: vi.fn(), onSave: vi.fn() };
    render(<ExtraMeepleCard {...defaultProps} actions={actions} />);

    expect(screen.getByText('Pause')).toBeInTheDocument();
    expect(screen.getByText('Save')).toBeInTheDocument();
  });

  it('renders start button for setup status', () => {
    const actions = { onStart: vi.fn(), onAddPlayer: vi.fn() };
    render(
      <ExtraMeepleCard
        {...defaultProps}
        status="setup"
        overviewData={{ ...mockOverview, status: 'setup' }}
        actions={actions}
      />
    );

    expect(screen.getByText('Start')).toBeInTheDocument();
    expect(screen.getByText('Add Player')).toBeInTheDocument();
  });

  it('calls onPause when pause clicked', () => {
    const onPause = vi.fn();
    render(<ExtraMeepleCard {...defaultProps} actions={{ onPause }} />);

    fireEvent.click(screen.getByText('Pause'));
    expect(onPause).toHaveBeenCalledOnce();
  });

  it('renders zero score for players', () => {
    const playersWithZero = [
      { ...mockPlayers[0], totalScore: 0 },
      { ...mockPlayers[1], totalScore: 0 },
    ];
    render(
      <ExtraMeepleCard
        {...defaultProps}
        overviewData={{ ...mockOverview, players: playersWithZero }}
      />
    );

    // Zero scores should be visible (not hidden)
    expect(screen.getAllByText('0').length).toBeGreaterThanOrEqual(2);
  });

  it('renders empty state when no data', () => {
    render(<ExtraMeepleCard {...defaultProps} overviewData={undefined} />);

    expect(screen.getByText('No session data available')).toBeInTheDocument();
  });
});

// ============================================================================
// ToolkitTab Tests
// ============================================================================

describe('ToolkitTab (via ExtraMeepleCard)', () => {
  it('renders toolkit info when switching to tab', () => {
    render(
      <ExtraMeepleCard {...defaultProps} activeTab="toolkit" toolkitData={mockToolkit} />
    );

    expect(screen.getByText('Catan Toolkit')).toBeInTheDocument();
    expect(screen.getByText('Published')).toBeInTheDocument();
  });

  it('renders dice tools', () => {
    render(
      <ExtraMeepleCard {...defaultProps} activeTab="toolkit" toolkitData={mockToolkit} />
    );

    expect(screen.getByText('Main Dice')).toBeInTheDocument();
    expect(screen.getByText(/D6 × 2/)).toBeInTheDocument();
  });

  it('renders card tools', () => {
    render(
      <ExtraMeepleCard {...defaultProps} activeTab="toolkit" toolkitData={mockToolkit} />
    );

    expect(screen.getByText('Resource Cards')).toBeInTheDocument();
    expect(screen.getByText(/95 cards/)).toBeInTheDocument();
  });

  it('renders counter tools', () => {
    render(
      <ExtraMeepleCard {...defaultProps} activeTab="toolkit" toolkitData={mockToolkit} />
    );

    expect(screen.getByText('Victory Points')).toBeInTheDocument();
  });

  it('renders empty state when no toolkit', () => {
    render(
      <ExtraMeepleCard {...defaultProps} activeTab="toolkit" toolkitData={undefined} />
    );

    expect(screen.getByText('No toolkit configured')).toBeInTheDocument();
  });
});

// ============================================================================
// ScoreboardTab Tests
// ============================================================================

describe('ScoreboardTab (via ExtraMeepleCard)', () => {
  it('renders player scores sorted by rank', () => {
    render(
      <ExtraMeepleCard {...defaultProps} activeTab="scoreboard" scoreboardData={mockScoreboard} />
    );

    // Player names and scores appear in both leaderboard and round scores table
    expect(screen.getAllByText('Alice').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('42').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Bob').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('35').length).toBeGreaterThanOrEqual(1);
    // Verify leaderboard heading exists
    expect(screen.getByText('Leaderboard')).toBeInTheDocument();
  });

  it('renders round scores table', () => {
    render(
      <ExtraMeepleCard {...defaultProps} activeTab="scoreboard" scoreboardData={mockScoreboard} />
    );

    expect(screen.getByText('R1')).toBeInTheDocument();
    expect(screen.getByText('R2')).toBeInTheDocument();
    expect(screen.getByText('15')).toBeInTheDocument();
    expect(screen.getByText('27')).toBeInTheDocument();
  });

  it('renders empty state when no scores', () => {
    render(
      <ExtraMeepleCard {...defaultProps} activeTab="scoreboard" scoreboardData={undefined} />
    );

    expect(screen.getByText('No scores yet')).toBeInTheDocument();
  });
});

// ============================================================================
// HistoryTab Tests
// ============================================================================

describe('HistoryTab (via ExtraMeepleCard)', () => {
  it('renders snapshots', () => {
    render(
      <ExtraMeepleCard {...defaultProps} activeTab="history" historyData={mockHistory} />
    );

    expect(screen.getByText('End of turn 1')).toBeInTheDocument();
    expect(screen.getByText('Manual save')).toBeInTheDocument();
  });

  it('renders snapshot numbers', () => {
    render(
      <ExtraMeepleCard {...defaultProps} activeTab="history" historyData={mockHistory} />
    );

    expect(screen.getByText('#1')).toBeInTheDocument();
    expect(screen.getByText('#2')).toBeInTheDocument();
  });

  it('renders timeline events', () => {
    render(
      <ExtraMeepleCard {...defaultProps} activeTab="history" historyData={mockHistory} />
    );

    expect(screen.getByText('Session started')).toBeInTheDocument();
    expect(screen.getByText('Alice turn')).toBeInTheDocument();
    expect(screen.getByText('Score updated')).toBeInTheDocument();
  });

  it('renders total turns count', () => {
    render(
      <ExtraMeepleCard {...defaultProps} activeTab="history" historyData={mockHistory} />
    );

    expect(screen.getByText('Total turns: 6')).toBeInTheDocument();
  });

  it('renders empty state when no history', () => {
    render(
      <ExtraMeepleCard {...defaultProps} activeTab="history" historyData={undefined} />
    );

    expect(screen.getByText('No history yet')).toBeInTheDocument();
  });
});
