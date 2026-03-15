/**
 * Guest Join Tests
 *
 * Game Night Improvvisata — Task 18
 *
 * Tests GuestJoinView (the testable inner component) directly,
 * avoiding the async-params wrapper of page.tsx.
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

// ─── Mocks ────────────────────────────────────────────────────────────────────

const mockFetch = vi.fn();
global.fetch = mockFetch;

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

// Mutable store state — allows per-test customization
const mockStoreState = {
  pendingProposals: [] as Array<{
    id: string;
    playerName: string;
    delta: number;
    timestamp: number;
  }>,
  players: [] as Array<{ id: string; name: string; isHost: boolean; isOnline: boolean }>,
  scores: {} as Record<string, number>,
  setSession: vi.fn(),
  addProposal: vi.fn(),
};

vi.mock('@/lib/stores/live-session-store', () => ({
  useLiveSessionStore: (selector: (s: typeof mockStoreState) => unknown) =>
    selector(mockStoreState),
}));

vi.mock('@/components/sessions/live/ScoreBoard', () => ({
  ScoreBoard: ({ sessionId }: { sessionId: string }) => (
    <div data-testid="scoreboard" data-session-id={sessionId}>
      Scoreboard
    </div>
  ),
}));

import { GuestJoinView } from '../[inviteToken]/GuestJoinView';

// ─── Helpers ──────────────────────────────────────────────────────────────────

const SESSION_RESPONSE = {
  id: 'session-uuid-123',
  gameName: 'Catan',
  inviteCode: 'ABC123',
  status: 'InProgress',
  currentTurn: 1,
  players: [{ id: 'host-1', displayName: 'Mario', isHost: true, isOnline: true }],
  scores: { Mario: 10 },
};

function setupFetch(opts: { sessionOk?: boolean; tokenValid?: boolean; joinOk?: boolean } = {}) {
  const { sessionOk = true, tokenValid = false, joinOk = true } = opts;

  mockFetch.mockImplementation((url: string) => {
    if (url.includes('/code/')) {
      return Promise.resolve({
        ok: sessionOk,
        json: () => Promise.resolve(SESSION_RESPONSE),
      });
    }
    if (url.includes('/guest/validate')) {
      return Promise.resolve({ ok: tokenValid });
    }
    if (url.includes('/guest/join')) {
      return Promise.resolve({
        ok: joinOk,
        json: () => Promise.resolve({ participantToken: 'pt-abc123' }),
      });
    }
    if (url.includes('/scores/propose')) {
      return Promise.resolve({
        ok: true,
        json: () => Promise.resolve({ proposalId: 'prop-1' }),
      });
    }
    return Promise.resolve({ ok: false, json: () => Promise.resolve({}) });
  });
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('GuestJoinView', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockStoreState.pendingProposals = [];
    mockStoreState.players = [];
    mockStoreState.scores = {};
    localStorage.clear();
  });

  it('shows loading state initially', () => {
    setupFetch();
    render(<GuestJoinView inviteToken="ABC123" />);
    expect(screen.getByTestId('loading-text')).toBeInTheDocument();
  });

  it('renders name input after session loads', async () => {
    setupFetch();
    render(<GuestJoinView inviteToken="ABC123" />);

    await waitFor(() => {
      expect(screen.getByLabelText(/il tuo nome/i)).toBeInTheDocument();
    });

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('ABC123')).toBeInTheDocument();
  });

  it('disables submit button when name is empty', async () => {
    setupFetch();
    render(<GuestJoinView inviteToken="ABC123" />);

    await waitFor(() => {
      expect(screen.getByLabelText(/il tuo nome/i)).toBeInTheDocument();
    });

    // Button should be disabled when input is empty
    const button = screen.getByRole('button', { name: /entra/i });
    expect(button).toBeDisabled();
  });

  it('shows validation error for whitespace-only name via aria-label', async () => {
    setupFetch();
    const user = userEvent.setup();
    render(<GuestJoinView inviteToken="ABC123" />);

    await waitFor(() => {
      expect(screen.getByLabelText(/il tuo nome/i)).toBeInTheDocument();
    });

    // Enable the button by typing something valid, then clear to simulate whitespace
    const nameInput = screen.getByLabelText(/il tuo nome/i);
    // Type valid name so button enables
    await user.type(nameInput, 'x');
    // Clear and type whitespace
    await user.clear(nameInput);
    await user.type(nameInput, '   ');
    // button is still disabled because ' '.trim() === ''
    expect(screen.getByRole('button', { name: /entra/i })).toBeDisabled();
  });

  it('joins session and shows scoreboard after submitting valid name', async () => {
    setupFetch();
    const user = userEvent.setup();
    render(<GuestJoinView inviteToken="ABC123" />);

    await waitFor(() => {
      expect(screen.getByLabelText(/il tuo nome/i)).toBeInTheDocument();
    });

    await user.type(screen.getByLabelText(/il tuo nome/i), 'Luigi');
    await user.click(screen.getByRole('button', { name: /entra/i }));

    await waitFor(() => {
      expect(screen.getByTestId('scoreboard')).toBeInTheDocument();
    });

    expect(screen.getAllByText(/Luigi/).length).toBeGreaterThan(0);
    expect(screen.getByText(/proponi punteggio/i)).toBeInTheDocument();
  });

  it('shows error state when session fetch fails', async () => {
    setupFetch({ sessionOk: false });
    render(<GuestJoinView inviteToken="INVALID" />);

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /sessione non trovata/i })).toBeInTheDocument();
    });
  });

  it('auto-rejoins when valid saved token exists', async () => {
    localStorage.setItem('improvvisata_participant_token', 'valid-token');
    localStorage.setItem('improvvisata_guest_name', 'SavedUser');
    setupFetch({ tokenValid: true });

    render(<GuestJoinView inviteToken="ABC123" />);

    await waitFor(() => {
      expect(screen.getByTestId('scoreboard')).toBeInTheDocument();
    });

    expect(screen.getAllByText(/SavedUser/).length).toBeGreaterThan(0);
  });
});

describe('GuestScoreProposal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockStoreState.pendingProposals = [
      { id: '1', playerName: 'Luigi', delta: 5, timestamp: 1 },
      { id: '2', playerName: 'Luigi', delta: 3, timestamp: 2 },
      { id: '3', playerName: 'Luigi', delta: -2, timestamp: 3 },
    ];
  });

  it('enforces max 3 pending proposals limit', async () => {
    const { GuestScoreProposal } = await import('@/components/sessions/live/GuestScoreProposal');

    render(<GuestScoreProposal guestName="Luigi" onPropose={vi.fn()} />);

    expect(screen.getByText(/hai raggiunto il limite/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /proponi/i })).not.toBeInTheDocument();
  });

  it('shows proposal form when under limit', async () => {
    mockStoreState.pendingProposals = []; // no pending
    const { GuestScoreProposal } = await import('@/components/sessions/live/GuestScoreProposal');

    render(<GuestScoreProposal guestName="Luigi" onPropose={vi.fn()} />);

    expect(screen.getByRole('button', { name: /proponi/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/delta punteggio/i)).toBeInTheDocument();
  });
});
