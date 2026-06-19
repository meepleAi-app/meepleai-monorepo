/**
 * ScoreBoard Tests
 *
 * Game Night Improvvisata — Task 16
 *
 * Block C (#2389) cleanup: ScoreBoard is now a read-only renderer for non-Host
 * viewers. The Host edit path lives in `ScoreTabContent` via
 * `PolymorphicScoreEditor`. Scores are looked up by `playerId` (not
 * `playerName`) and derived from `scoreData` (`Points` variant only).
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { ScoreDataByType, ScoreType } from '@/components/sessions/score-strategies/types';
import type { PlayerInfo, ScoreProposal } from '@/lib/stores/live-session-store';

import { ScoreBoard } from '../ScoreBoard';

// ─── Mock useSessionScores ─────────────────────────────────────────────────────

interface MockSessionScoresShape {
  scoringType: ScoreType | null;
  scoreData: ScoreDataByType[ScoreType] | null;
  players: PlayerInfo[];
  pendingProposals: ScoreProposal[];
  leader: string | null;
}

const mockSessionScores = vi.hoisted<{ value: MockSessionScoresShape }>(() => ({
  value: {
    scoringType: 'Points',
    scoreData: null,
    players: [],
    pendingProposals: [],
    leader: null,
  },
}));

vi.mock('@/lib/domain-hooks/useSessionScores', () => ({
  useSessionScores: () => mockSessionScores.value,
}));

// ─── Helpers ──────────────────────────────────────────────────────────────────

function setMockScores(partial: Partial<MockSessionScoresShape>) {
  mockSessionScores.value = { ...mockSessionScores.value, ...partial };
}

const ALICE: PlayerInfo = { id: 'p1', name: 'Alice', isHost: true, isOnline: true };
const BOB: PlayerInfo = { id: 'p2', name: 'Bob', isHost: false, isOnline: true };
const CARL: PlayerInfo = { id: 'p3', name: 'Carl', isHost: false, isOnline: false };

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('ScoreBoard (Block C playerId-keyed)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockSessionScores.value = {
      scoringType: 'Points',
      scoreData: null,
      players: [],
      pendingProposals: [],
      leader: null,
    };
  });

  it('renders player scores keyed by playerId from scoreData Points', () => {
    setMockScores({
      scoringType: 'Points',
      players: [ALICE, BOB, CARL],
      scoreData: {
        scores: [
          { playerId: 'p1', points: 5 },
          { playerId: 'p2', points: 10 },
          { playerId: 'p3', points: 3 },
        ],
      } satisfies ScoreDataByType['Points'],
      leader: 'p2',
    });

    render(<ScoreBoard sessionId="sess-1" />);

    expect(screen.getByTestId('player-score-p1')).toHaveTextContent('5');
    expect(screen.getByTestId('player-score-p2')).toHaveTextContent('10');
    expect(screen.getByTestId('player-score-p3')).toHaveTextContent('3');
  });

  it('renders player names', () => {
    setMockScores({
      scoringType: 'Points',
      players: [ALICE, BOB],
      scoreData: {
        scores: [
          { playerId: 'p1', points: 1 },
          { playerId: 'p2', points: 2 },
        ],
      } satisfies ScoreDataByType['Points'],
      leader: 'p2',
    });

    render(<ScoreBoard sessionId="sess-1" />);

    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
  });

  it('highlights leader badge on player whose id matches leader', () => {
    setMockScores({
      scoringType: 'Points',
      players: [ALICE, BOB],
      scoreData: {
        scores: [
          { playerId: 'p1', points: 3 },
          { playerId: 'p2', points: 12 },
        ],
      } satisfies ScoreDataByType['Points'],
      leader: 'p2',
    });

    const { container } = render(<ScoreBoard sessionId="sess-1" />);

    // Crown icon is rendered only on the leader's card.
    const crowns = container.querySelectorAll('[aria-label="Leader"]');
    expect(crowns).toHaveLength(1);

    // The Crown is inside Bob's card (p2). Its closest ancestor card should
    // also contain the "Bob" label.
    const bobCard = crowns[0]?.closest('div[class*="rounded-xl"]');
    expect(bobCard).not.toBeNull();
    expect(bobCard?.textContent).toContain('Bob');
    expect(bobCard?.textContent).not.toContain('Alice');
  });

  it('shows empty state when no players', () => {
    setMockScores({
      scoringType: 'Points',
      players: [],
      scoreData: null,
      leader: null,
    });

    render(<ScoreBoard sessionId="sess-1" />);

    expect(screen.getByText('Nessun giocatore ancora registrato.')).toBeInTheDocument();
  });

  it('shows 0 score for players with no entry in scoreData', () => {
    setMockScores({
      scoringType: 'Points',
      players: [ALICE, BOB, CARL],
      scoreData: {
        // Only Alice has a recorded score.
        scores: [{ playerId: 'p1', points: 7 }],
      } satisfies ScoreDataByType['Points'],
      leader: 'p1',
    });

    render(<ScoreBoard sessionId="sess-1" />);

    expect(screen.getByTestId('player-score-p1')).toHaveTextContent('7');
    expect(screen.getByTestId('player-score-p2')).toHaveTextContent('0');
    expect(screen.getByTestId('player-score-p3')).toHaveTextContent('0');
  });

  it('shows 0 scores for non-Points scoringType (Ranking)', () => {
    setMockScores({
      scoringType: 'Ranking',
      players: [ALICE, BOB],
      scoreData: {
        positions: [
          { playerId: 'p1', position: 1 },
          { playerId: 'p2', position: 2 },
        ],
      } satisfies ScoreDataByType['Ranking'],
      leader: null,
    });

    render(<ScoreBoard sessionId="sess-1" />);

    expect(screen.getByTestId('player-score-p1')).toHaveTextContent('0');
    expect(screen.getByTestId('player-score-p2')).toHaveTextContent('0');
  });

  it('does not render any +/- buttons (Host edit path moved out of ScoreBoard)', () => {
    setMockScores({
      scoringType: 'Points',
      players: [ALICE, BOB],
      scoreData: {
        scores: [
          { playerId: 'p1', points: 1 },
          { playerId: 'p2', points: 2 },
        ],
      } satisfies ScoreDataByType['Points'],
      leader: 'p2',
    });

    render(<ScoreBoard sessionId="sess-1" />);

    expect(screen.queryAllByLabelText(/Incrementa punteggio/i)).toHaveLength(0);
    expect(screen.queryAllByLabelText(/Decrementa punteggio/i)).toHaveLength(0);
  });

  it('does not render any proposal cards (proposals moved out of ScoreBoard)', () => {
    setMockScores({
      scoringType: 'Points',
      players: [ALICE, BOB],
      scoreData: {
        scores: [
          { playerId: 'p1', points: 1 },
          { playerId: 'p2', points: 2 },
        ],
      } satisfies ScoreDataByType['Points'],
      pendingProposals: [{ id: 'prop-1', playerName: 'Bob', delta: 3, timestamp: Date.now() }],
      leader: 'p2',
    });

    render(<ScoreBoard sessionId="sess-1" />);

    expect(screen.queryByText('Proposte in attesa')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Approva proposta')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Rifiuta proposta')).not.toBeInTheDocument();
  });
});
