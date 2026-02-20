/**
 * Tests for SessionBackContent component
 * Issue #4752 - MeepleCard Session Back
 */

import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { SessionBackContent } from '../SessionBackContent';
import type {
  SessionBackData,
  SessionPlayerInfo,
} from '../session-types';

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
  {
    id: 'p3',
    displayName: 'Carol',
    color: 'green',
    role: 'player',
    totalScore: 28,
    currentRank: 3,
    isActive: true,
  },
];

const mockBackData: SessionBackData = {
  durationMinutes: 45,
  totalTurns: 12,
  totalRounds: 4,
  averageScore: 35,
  timeline: [
    { id: 'e1', timestamp: '2026-01-01T10:00:00Z', type: 'turn', playerId: 'p1', label: 'Alice inizia turno 1' },
    { id: 'e2', timestamp: '2026-01-01T10:05:00Z', type: 'score', playerId: 'p1', label: 'Alice segna 15 punti' },
    { id: 'e3', timestamp: '2026-01-01T10:10:00Z', type: 'turn', playerId: 'p2', label: 'Bob inizia turno 2' },
  ],
  mediaCounts: { photos: 3, videos: 1, audio: 0 },
  agentChatHref: '/sessions/s1/agent',
  sessionCode: 'ABC123',
  scoringType: 'Punti Vittoria',
  turnType: 'Sequenziale',
};

// ============================================================================
// Header Tests
// ============================================================================

describe('SessionBackContent - Header', () => {
  it('renders header with title and status label', () => {
    render(
      <SessionBackContent
        status="inProgress"
        players={mockPlayers}
        backData={mockBackData}
        title="Partita di Catan"
      />,
    );
    const content = screen.getByTestId('session-back-content');
    expect(content).toBeInTheDocument();
    expect(screen.getByText('Partita di Catan')).toBeInTheDocument();
    expect(screen.getByText('In Corso')).toBeInTheDocument();
  });

  it('renders correct status labels for all statuses', () => {
    const statuses = [
      { status: 'setup' as const, label: 'Configurazione' },
      { status: 'inProgress' as const, label: 'In Corso' },
      { status: 'paused' as const, label: 'In Pausa' },
      { status: 'completed' as const, label: 'Completata' },
    ];

    for (const { status, label } of statuses) {
      const { unmount } = render(
        <SessionBackContent
          status={status}
          players={mockPlayers}
          backData={mockBackData}
        />,
      );
      // Use getAllByText for 'Configurazione' since it appears as both
      // the status label and setup config heading
      const elements = screen.getAllByText(label);
      expect(elements.length).toBeGreaterThanOrEqual(1);
      unmount();
    }
  });

  it('defaults title to "Sessione" when not provided', () => {
    render(
      <SessionBackContent
        status="inProgress"
        players={mockPlayers}
        backData={mockBackData}
      />,
    );
    expect(screen.getByText('Sessione')).toBeInTheDocument();
  });
});

// ============================================================================
// Statistics Grid Tests
// ============================================================================

describe('SessionBackContent - Statistics', () => {
  it('renders statistics grid for inProgress status', () => {
    render(
      <SessionBackContent
        status="inProgress"
        players={mockPlayers}
        backData={mockBackData}
      />,
    );
    const statsGrid = screen.getByTestId('stats-grid');
    expect(statsGrid).toBeInTheDocument();
    expect(screen.getByTestId('stat-durata')).toBeInTheDocument();
    expect(screen.getByTestId('stat-turni')).toBeInTheDocument();
  });

  it('does not render statistics grid for setup status', () => {
    render(
      <SessionBackContent
        status="setup"
        players={mockPlayers}
        backData={mockBackData}
      />,
    );
    expect(screen.queryByTestId('stats-grid')).not.toBeInTheDocument();
  });

  it('shows duration in minutes', () => {
    render(
      <SessionBackContent
        status="inProgress"
        players={mockPlayers}
        backData={mockBackData}
      />,
    );
    expect(screen.getByText('45m')).toBeInTheDocument();
  });

  it('shows "Vincitore" label for completed status', () => {
    render(
      <SessionBackContent
        status="completed"
        players={mockPlayers}
        backData={mockBackData}
      />,
    );
    expect(screen.getByText('Vincitore')).toBeInTheDocument();
  });

  it('shows "Leader" label for non-completed status', () => {
    render(
      <SessionBackContent
        status="inProgress"
        players={mockPlayers}
        backData={mockBackData}
      />,
    );
    expect(screen.getByText('Leader')).toBeInTheDocument();
  });
});

// ============================================================================
// Setup Config Tests
// ============================================================================

describe('SessionBackContent - Setup Config', () => {
  it('renders setup configuration for setup status', () => {
    render(
      <SessionBackContent
        status="setup"
        players={mockPlayers}
        backData={mockBackData}
      />,
    );
    const config = screen.getByTestId('setup-config');
    expect(config).toBeInTheDocument();
    expect(screen.getByText('ABC123')).toBeInTheDocument();
  });

  it('shows scoring type and turn type', () => {
    render(
      <SessionBackContent
        status="setup"
        players={mockPlayers}
        backData={mockBackData}
      />,
    );
    expect(screen.getByText(/Punti Vittoria/)).toBeInTheDocument();
    expect(screen.getByText(/Sequenziale/)).toBeInTheDocument();
  });

  it('shows player count excluding spectators', () => {
    const playersWithSpectator: SessionPlayerInfo[] = [
      ...mockPlayers,
      {
        id: 'p4',
        displayName: 'Dave',
        color: 'yellow',
        role: 'spectator',
        totalScore: 0,
        currentRank: 4,
        isActive: true,
      },
    ];
    render(
      <SessionBackContent
        status="setup"
        players={playersWithSpectator}
        backData={mockBackData}
      />,
    );
    expect(screen.getByText('Giocatori: 3')).toBeInTheDocument();
  });

  it('does not render setup config for non-setup statuses', () => {
    render(
      <SessionBackContent
        status="inProgress"
        players={mockPlayers}
        backData={mockBackData}
      />,
    );
    expect(screen.queryByTestId('setup-config')).not.toBeInTheDocument();
  });
});

// ============================================================================
// Ranking Tests
// ============================================================================

describe('SessionBackContent - Ranking', () => {
  it('renders ranking section for inProgress status', () => {
    render(
      <SessionBackContent
        status="inProgress"
        players={mockPlayers}
        backData={mockBackData}
      />,
    );
    const ranking = screen.getByTestId('ranking-section');
    expect(ranking).toBeInTheDocument();
  });

  it('renders player rows sorted by rank', () => {
    render(
      <SessionBackContent
        status="inProgress"
        players={mockPlayers}
        backData={mockBackData}
      />,
    );
    expect(screen.getByTestId('rank-row-p1')).toBeInTheDocument();
    expect(screen.getByTestId('rank-row-p2')).toBeInTheDocument();
    expect(screen.getByTestId('rank-row-p3')).toBeInTheDocument();
  });

  it('shows medal icons for top 3', () => {
    render(
      <SessionBackContent
        status="completed"
        players={mockPlayers}
        backData={mockBackData}
      />,
    );
    const row1 = screen.getByTestId('rank-row-p1');
    expect(within(row1).getByText('🥇')).toBeInTheDocument();
    const row2 = screen.getByTestId('rank-row-p2');
    expect(within(row2).getByText('🥈')).toBeInTheDocument();
    const row3 = screen.getByTestId('rank-row-p3');
    expect(within(row3).getByText('🥉')).toBeInTheDocument();
  });

  it('shows "Classifica Finale" for completed status', () => {
    render(
      <SessionBackContent
        status="completed"
        players={mockPlayers}
        backData={mockBackData}
      />,
    );
    expect(screen.getByText('Classifica Finale')).toBeInTheDocument();
  });

  it('shows "Classifica" for non-completed status', () => {
    render(
      <SessionBackContent
        status="inProgress"
        players={mockPlayers}
        backData={mockBackData}
      />,
    );
    expect(screen.getByText('Classifica')).toBeInTheDocument();
  });

  it('does not render ranking for setup status', () => {
    render(
      <SessionBackContent
        status="setup"
        players={mockPlayers}
        backData={mockBackData}
      />,
    );
    expect(screen.queryByTestId('ranking-section')).not.toBeInTheDocument();
  });
});

// ============================================================================
// Timeline Tests
// ============================================================================

describe('SessionBackContent - Timeline', () => {
  it('renders timeline events', () => {
    render(
      <SessionBackContent
        status="inProgress"
        players={mockPlayers}
        backData={mockBackData}
      />,
    );
    const timeline = screen.getByTestId('timeline-section');
    expect(timeline).toBeInTheDocument();
    expect(screen.getByText('Alice inizia turno 1')).toBeInTheDocument();
  });

  it('limits timeline to last 5 events', () => {
    const manyEvents = Array.from({ length: 10 }, (_, i) => ({
      id: `e${i}`,
      timestamp: `2026-01-01T${10 + i}:00:00Z`,
      type: 'turn' as const,
      label: `Event ${i + 1}`,
    }));
    render(
      <SessionBackContent
        status="inProgress"
        players={mockPlayers}
        backData={{ ...mockBackData, timeline: manyEvents }}
      />,
    );
    // Last 5 should be visible (Event 6-10)
    expect(screen.getByText('Event 6')).toBeInTheDocument();
    expect(screen.getByText('Event 10')).toBeInTheDocument();
    expect(screen.queryByText('Event 5')).not.toBeInTheDocument();
  });

  it('does not render timeline for setup status', () => {
    render(
      <SessionBackContent
        status="setup"
        players={mockPlayers}
        backData={mockBackData}
      />,
    );
    expect(screen.queryByTestId('timeline-section')).not.toBeInTheDocument();
  });

  it('does not render timeline when empty', () => {
    render(
      <SessionBackContent
        status="inProgress"
        players={mockPlayers}
        backData={{ ...mockBackData, timeline: [] }}
      />,
    );
    expect(screen.queryByTestId('timeline-section')).not.toBeInTheDocument();
  });
});

// ============================================================================
// Media & Links Tests
// ============================================================================

describe('SessionBackContent - Media & Links', () => {
  it('renders media counts', () => {
    render(
      <SessionBackContent
        status="inProgress"
        players={mockPlayers}
        backData={mockBackData}
      />,
    );
    const media = screen.getByTestId('media-counts');
    expect(media).toBeInTheDocument();
    expect(within(media).getByText('3')).toBeInTheDocument(); // photos
    expect(within(media).getByText('1')).toBeInTheDocument(); // videos
  });

  it('hides media counts when all zero', () => {
    render(
      <SessionBackContent
        status="inProgress"
        players={mockPlayers}
        backData={{ ...mockBackData, mediaCounts: { photos: 0, videos: 0, audio: 0 } }}
      />,
    );
    expect(screen.queryByTestId('media-counts')).not.toBeInTheDocument();
  });

  it('renders agent chat link', () => {
    render(
      <SessionBackContent
        status="inProgress"
        players={mockPlayers}
        backData={mockBackData}
      />,
    );
    const agentLink = screen.getByTestId('agent-chat-link');
    expect(agentLink).toBeInTheDocument();
    expect(agentLink).toHaveAttribute('href', '/sessions/s1/agent');
  });

  it('renders PlayRecord link for completed status', () => {
    render(
      <SessionBackContent
        status="completed"
        players={mockPlayers}
        backData={{ ...mockBackData, playRecordHref: '/playrecords/pr1' }}
      />,
    );
    const prLink = screen.getByTestId('playrecord-link');
    expect(prLink).toBeInTheDocument();
    expect(prLink).toHaveAttribute('href', '/playrecords/pr1');
  });

  it('does not render PlayRecord link for non-completed status', () => {
    render(
      <SessionBackContent
        status="inProgress"
        players={mockPlayers}
        backData={{ ...mockBackData, playRecordHref: '/playrecords/pr1' }}
      />,
    );
    expect(screen.queryByTestId('playrecord-link')).not.toBeInTheDocument();
  });

  it('renders detail link when detailHref provided', () => {
    render(
      <SessionBackContent
        status="inProgress"
        players={mockPlayers}
        backData={mockBackData}
        detailHref="/sessions/s1"
      />,
    );
    const detailLink = screen.getByTestId('session-detail-link');
    expect(detailLink).toBeInTheDocument();
    expect(detailLink).toHaveAttribute('href', '/sessions/s1');
  });

  it('does not render detail link when detailHref not provided', () => {
    render(
      <SessionBackContent
        status="inProgress"
        players={mockPlayers}
        backData={mockBackData}
      />,
    );
    expect(screen.queryByTestId('session-detail-link')).not.toBeInTheDocument();
  });
});
