/**
 * ScoreBoard Tests
 *
 * Game Night Improvvisata — Task 16
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ScoreBoard } from '../ScoreBoard';

// ─── Mock Zustand store ───────────────────────────────────────────────────────

const mockScores: Record<string, number> = { Alice: 10, Bob: 7 };
const mockPlayers = [
  { id: 'p1', name: 'Alice', isHost: true, isOnline: true },
  { id: 'p2', name: 'Bob', isHost: false, isOnline: true },
];
const mockPendingProposals = [{ id: 'prop-1', playerName: 'Bob', delta: 3, timestamp: Date.now() }];

const mockResolveProposal = vi.hoisted(() => vi.fn());
const mockUpdateScore = vi.hoisted(() => vi.fn());

vi.mock('@/lib/stores/live-session-store', () => ({
  useLiveSessionStore: (selector: (s: Record<string, unknown>) => unknown) => {
    const state = {
      scores: mockScores,
      players: mockPlayers,
      pendingProposals: mockPendingProposals,
      disputes: [],
      resolveProposal: mockResolveProposal,
      updateScore: mockUpdateScore,
    };
    return selector(state);
  },
}));

// ─── Mock useSessionScores ─────────────────────────────────────────────────────

vi.mock('@/lib/hooks/use-session-scores', () => ({
  useSessionScores: () => ({
    scores: mockScores,
    players: mockPlayers,
    pendingProposals: mockPendingProposals,
    leader: 'Alice',
  }),
}));

// ─── Mock useSignalRSession ───────────────────────────────────────────────────

vi.mock('@/lib/hooks/use-signalr-session', () => ({
  useSignalRSession: () => ({
    connection: null,
    isConnected: true,
    sendScore: vi.fn().mockResolvedValue(undefined),
    proposeScore: vi.fn().mockResolvedValue(undefined),
    appBackgrounded: vi.fn().mockResolvedValue(undefined),
  }),
}));

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('ScoreBoard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders player names', () => {
    render(<ScoreBoard sessionId="sess-1" />);
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
  });

  it('renders player scores', () => {
    render(<ScoreBoard sessionId="sess-1" />);
    expect(screen.getByText('10')).toBeInTheDocument();
    expect(screen.getByText('7')).toBeInTheDocument();
  });

  it('renders crown icon on leader (Alice)', () => {
    render(<ScoreBoard sessionId="sess-1" />);
    const crown = document.querySelector('[aria-label="Leader"]');
    expect(crown).toBeInTheDocument();
  });

  it('does not show +/- buttons for guests (isHost=false)', () => {
    render(<ScoreBoard sessionId="sess-1" isHost={false} />);
    const incrementBtns = screen.queryAllByLabelText(/Incrementa punteggio/i);
    expect(incrementBtns).toHaveLength(0);
  });

  it('shows +/- buttons for the host', () => {
    render(<ScoreBoard sessionId="sess-1" isHost />);
    const incrementBtns = screen.getAllByLabelText(/Incrementa punteggio/i);
    const decrementBtns = screen.getAllByLabelText(/Decrementa punteggio/i);
    expect(incrementBtns.length).toBeGreaterThan(0);
    expect(decrementBtns.length).toBeGreaterThan(0);
  });

  it('calls updateScore when host clicks + button', () => {
    render(<ScoreBoard sessionId="sess-1" isHost />);
    const aliceIncrement = screen.getByLabelText('Incrementa punteggio di Alice');
    fireEvent.click(aliceIncrement);
    expect(mockUpdateScore).toHaveBeenCalledWith('Alice', 11);
  });

  it('shows pending proposals for host', () => {
    render(<ScoreBoard sessionId="sess-1" isHost />);
    expect(screen.getByText('Proposte in attesa')).toBeInTheDocument();
    // Bob appears in both the player card and the proposal card — use getAllByText
    expect(screen.getAllByText('Bob').length).toBeGreaterThan(0);
    expect(screen.getByText('+3')).toBeInTheDocument();
  });

  it('does not show pending proposals section for non-host', () => {
    render(<ScoreBoard sessionId="sess-1" isHost={false} />);
    expect(screen.queryByText('Proposte in attesa')).not.toBeInTheDocument();
  });

  it('calls resolveProposal(id, true) on approve', () => {
    render(<ScoreBoard sessionId="sess-1" isHost />);
    const approveBtn = screen.getByLabelText('Approva proposta');
    fireEvent.click(approveBtn);
    expect(mockResolveProposal).toHaveBeenCalledWith('prop-1', true);
  });

  it('calls resolveProposal(id, false) on reject', () => {
    render(<ScoreBoard sessionId="sess-1" isHost />);
    const rejectBtn = screen.getByLabelText('Rifiuta proposta');
    fireEvent.click(rejectBtn);
    expect(mockResolveProposal).toHaveBeenCalledWith('prop-1', false);
  });
});
