/**
 * @vitest-environment jsdom
 *
 * NightLiveClientView integration tests — #2633 Slice B.
 *
 * The view is now backend-driven via useGameNightLive: no header/planned-games
 * fixtures remain. These tests cover the resilience matrix (LD-10), terminal-night
 * routing (LD-14), the empty happy-path (LD-11), the read-only projection (LD-13),
 * and the real session jump (AC3).
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import {
  ConflictError,
  ForbiddenError,
  NotFoundError,
  UnauthorizedError,
  NetworkError,
} from '@/lib/api/core/errors';
import { MAX_LIVE_SESSIONS_EXCEEDED } from '@/lib/game-nights/hooks/useStartNextGame';
import type { NightLiveViewModel } from '@/lib/game-nights/mapNightLive';

import { NightLiveClientView } from '../NightLiveClientView';

const useGameNightLiveMock = vi.hoisted(() => vi.fn());
vi.mock('@/lib/game-nights/hooks/useGameNightLive', () => ({
  useGameNightLive: useGameNightLiveMock,
}));

// Mock only the mutation hook; keep isMaxLiveBlockedError + MAX_LIVE_SESSIONS_EXCEEDED real so the
// view's real 409 discrimination is exercised (no QueryClientProvider needed in these tests).
const startNextMutateMock = vi.hoisted(() => vi.fn());
const startNextState = vi.hoisted(() => ({ isPending: false }));
vi.mock('@/lib/game-nights/hooks/useStartNextGame', async importActual => {
  const actual = await importActual<typeof import('@/lib/game-nights/hooks/useStartNextGame')>();
  return {
    ...actual,
    useStartNextGame: () => ({ mutate: startNextMutateMock, isPending: startNextState.isPending }),
  };
});

// Slice C2: the view composes a second read (the diary). Mock it to a resolved-empty default so
// these live-focused tests don't need a QueryClient; a dedicated block drives it for diary render.
const useNightLiveDiaryMock = vi.hoisted(() => vi.fn());
vi.mock('@/lib/game-nights/hooks/useNightLiveDiary', () => ({
  useNightLiveDiary: useNightLiveDiaryMock,
  nightLiveDiaryKeys: {
    all: ['game-nights', 'diary'],
    detail: (id: string) => ['game-nights', 'diary', id],
  },
}));

// #2634 C4: the organizer "Completa" mutation.
const completeMutateMock = vi.hoisted(() => vi.fn());
const completeResetMock = vi.hoisted(() => vi.fn());
const completeState = vi.hoisted(() => ({
  isPending: false,
  isError: false,
  error: null as Error | null,
}));
vi.mock('@/lib/game-nights/hooks/useCompleteGameNightSession', () => ({
  useCompleteGameNightSession: () => ({
    mutate: completeMutateMock,
    reset: completeResetMock,
    isPending: completeState.isPending,
    isError: completeState.isError,
    error: completeState.error,
  }),
}));

const pushMock = vi.fn();
const replaceMock = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: pushMock, replace: replaceMock }),
}));

const NIGHT_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const IN_PROGRESS_SESSION_ID = '33333333-3333-4333-8333-333333333333';

function vm(over: Partial<NightLiveViewModel> = {}): NightLiveViewModel {
  return {
    night: { title: 'Serata Eldoria' },
    nightStatus: 'Published',
    isViewerOrganizer: false,
    nextGame: null,
    status: 'live',
    current: 2,
    total: 3,
    elapsed: '2h 35m',
    confirmedPlayers: undefined,
    totalPlayers: undefined,
    plannedGames: [
      {
        id: '11111111-1111-4111-8111-111111111111',
        title: 'Brass',
        status: 'completed',
        order: 1,
        actual: '113m',
      },
      {
        id: IN_PROGRESS_SESSION_ID,
        title: 'Spirit Island',
        status: 'inprogress',
        order: 2,
        actual: '35m',
      },
      {
        id: '55555555-5555-4555-8555-555555555555',
        title: 'Wingspan',
        status: 'upcoming',
        order: 3,
      },
    ],
    currentGame: null,
    sessions: [],
    winnerCandidates: [],
    ...over,
  };
}

function mockQuery(over: Record<string, unknown>) {
  useGameNightLiveMock.mockReturnValue({
    data: undefined,
    isLoading: false,
    isError: false,
    error: null,
    ...over,
  });
}

beforeEach(() => {
  vi.clearAllMocks();
  startNextState.isPending = false;
  completeState.isPending = false;
  completeState.isError = false;
  completeState.error = null;
  useNightLiveDiaryMock.mockReturnValue({ data: undefined });
});

const NEXT_GAME = { gameId: '77777777-7777-4777-8777-777777777777', gameTitle: 'Catan' };

describe('NightLiveClientView — loading', () => {
  it('renders a loading skeleton while the query is pending', () => {
    mockQuery({ isLoading: true });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(screen.getByRole('status', { name: /Caricamento serata/i })).toBeInTheDocument();
    // the hub is NOT mounted with placeholder data
    expect(screen.queryByText('Serata Eldoria')).toBeNull();
  });
});

describe('NightLiveClientView — Published happy path', () => {
  it('renders the hub header + planned games from the backend viewmodel (no fixtures)', () => {
    mockQuery({ data: vm() });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(screen.getByText('Serata Eldoria')).toBeInTheDocument();
    expect(screen.getByText('Brass')).toBeInTheDocument();
    expect(screen.getByText('Spirit Island')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    // the old fixture night title is gone
    expect(screen.queryByText('Sabato boardgame con i Padovani')).toBeNull();
  });

  it('is a read-only projection: no pause/transition/end drive controls (LD-13)', () => {
    mockQuery({ data: vm() });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(screen.queryByRole('button', { name: /Transition/ })).toBeNull();
    expect(screen.queryByRole('button', { name: /Pausa/ })).toBeNull();
    expect(screen.queryByRole('button', { name: /^End$/ })).toBeNull();
  });

  it('navigates back to the night detail on "Indietro"', async () => {
    mockQuery({ data: vm() });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    await userEvent.click(screen.getByRole('button', { name: 'Indietro' }));
    expect(pushMock).toHaveBeenCalledWith(`/game-nights/${NIGHT_ID}`);
  });

  it('jumps to the session via its SessionId route key (AC3)', async () => {
    // currentGame is Slice C (null from the mapper today) — but the jump handler
    // is wired now, so we exercise it with a non-null currentGame to assert the
    // /sessions/{SessionId} routing contract Slice C will rely on.
    mockQuery({
      data: vm({
        currentGame: {
          id: IN_PROGRESS_SESSION_ID,
          sessionId: IN_PROGRESS_SESSION_ID,
          title: 'Spirit Island',
        },
      }),
    });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    await userEvent.click(screen.getByRole('button', { name: /Apri sessione live/i }));
    expect(pushMock).toHaveBeenCalledWith(`/sessions/${IN_PROGRESS_SESSION_ID}`);
  });

  it('renders a defined empty state for a Published night with 0 sessions (LD-11)', () => {
    mockQuery({
      data: vm({ plannedGames: [], current: 0, total: 0, elapsed: '0h 0m', status: 'transition' }),
    });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(screen.getByText('Nessun gioco pianificato')).toBeInTheDocument();
  });
});

describe('NightLiveClientView — organizer interactive start (WS1 DEC-10)', () => {
  it('does NOT render the start CTA for a non-organizer viewer', () => {
    mockQuery({
      data: vm({ isViewerOrganizer: false, nextGame: NEXT_GAME, status: 'transition' }),
    });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(screen.queryByRole('button', { name: /Avvia:/i })).toBeNull();
  });

  it('does NOT render the start CTA when there is no next game', () => {
    mockQuery({ data: vm({ isViewerOrganizer: true, nextGame: null, status: 'transition' }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(screen.queryByRole('button', { name: /Avvia:/i })).toBeNull();
  });

  it('hides the start CTA while a game is already live (status==="live")', () => {
    mockQuery({ data: vm({ isViewerOrganizer: true, nextGame: NEXT_GAME, status: 'live' }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(screen.queryByRole('button', { name: /Avvia:/i })).toBeNull();
  });

  it('renders the start CTA for the organizer when a next game is queued and no game is live', () => {
    mockQuery({ data: vm({ isViewerOrganizer: true, nextGame: NEXT_GAME, status: 'transition' }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(screen.getByRole('button', { name: /Avvia: Catan/i })).toBeInTheDocument();
  });

  it('starts the next game with its id + title on click', async () => {
    mockQuery({ data: vm({ isViewerOrganizer: true, nextGame: NEXT_GAME, status: 'transition' }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    await userEvent.click(screen.getByRole('button', { name: /Avvia: Catan/i }));
    expect(startNextMutateMock).toHaveBeenCalledWith(
      { gameId: NEXT_GAME.gameId, gameTitle: NEXT_GAME.gameTitle },
      expect.objectContaining({ onError: expect.any(Function) })
    );
  });

  it('disables the CTA while the mutation is pending', () => {
    startNextState.isPending = true;
    mockQuery({ data: vm({ isViewerOrganizer: true, nextGame: NEXT_GAME, status: 'transition' }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(screen.getByRole('button', { name: /Avvio/i })).toBeDisabled();
  });

  it('surfaces the blocked modal when the start returns a max-1-live 409', async () => {
    startNextMutateMock.mockImplementation((_vars, opts) => {
      opts?.onError?.(new ConflictError({ message: 'blocked', code: MAX_LIVE_SESSIONS_EXCEEDED }));
    });
    mockQuery({ data: vm({ isViewerOrganizer: true, nextGame: NEXT_GAME, status: 'transition' }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    await userEvent.click(screen.getByRole('button', { name: /Avvia: Catan/i }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /già una partita live/i })).toBeInTheDocument();
  });

  it('does NOT surface the blocked modal for a non-409 error', async () => {
    startNextMutateMock.mockImplementation((_vars, opts) => {
      opts?.onError?.(new Error('boom'));
    });
    mockQuery({ data: vm({ isViewerOrganizer: true, nextGame: NEXT_GAME, status: 'transition' }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    await userEvent.click(screen.getByRole('button', { name: /Avvia: Catan/i }));
    expect(screen.queryByRole('dialog')).toBeNull();
  });
});

describe('NightLiveClientView — diary composition (Slice C2)', () => {
  const GAME_ID = '88888888-8888-4888-8888-888888888888';

  it('renders diary events composed from the diary read + the live sessionId→game lookup', () => {
    useNightLiveDiaryMock.mockReturnValue({
      data: {
        gameNightId: NIGHT_ID,
        entries: [
          {
            id: '99999999-9999-4999-8999-999999999999',
            sessionId: IN_PROGRESS_SESSION_ID,
            eventType: 'score_updated',
            description: '📊 Punteggio aggiornato',
            payload: null,
            actorId: null,
            timestamp: '2026-07-04T20:00:00',
          },
        ],
      },
    });
    mockQuery({
      data: vm({
        sessions: [
          { sessionId: IN_PROGRESS_SESSION_ID, gameId: GAME_ID, gameTitle: 'Spirit Island' },
        ],
      }),
    });

    render(<NightLiveClientView nightId={NIGHT_ID} />);

    expect(screen.getAllByText(/Punteggio aggiornato/i).length).toBeGreaterThan(0);
  });

  it('renders the hub with an empty diary when the diary read has not resolved (AC6)', () => {
    useNightLiveDiaryMock.mockReturnValue({ data: undefined });
    mockQuery({ data: vm() });

    render(<NightLiveClientView nightId={NIGHT_ID} />);

    // the live hub still renders (no crash / no dependency on the diary read)
    expect(screen.getByText('Serata Eldoria')).toBeInTheDocument();
  });
});

describe('NightLiveClientView — organizer Completa + winner picker (C4)', () => {
  const ROSTER = [
    { id: '77777777-7777-4777-8777-777777777777', displayName: 'Alice' },
    { id: '88888888-8888-4888-8888-888888888888', displayName: 'Bob' },
  ];

  it('shows the "Completa partita" CTA for the organizer while a game is live', () => {
    mockQuery({ data: vm({ isViewerOrganizer: true, status: 'live', winnerCandidates: ROSTER }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(screen.getByRole('button', { name: /Completa partita/i })).toBeInTheDocument();
  });

  it('does NOT show the Completa CTA for a non-organizer', () => {
    mockQuery({ data: vm({ isViewerOrganizer: false, status: 'live', winnerCandidates: ROSTER }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(screen.queryByRole('button', { name: /Completa partita/i })).toBeNull();
  });

  it('does NOT show the Completa CTA when no game is live', () => {
    mockQuery({
      data: vm({ isViewerOrganizer: true, status: 'transition', winnerCandidates: [] }),
    });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(screen.queryByRole('button', { name: /Completa partita/i })).toBeNull();
  });

  it('opens the winner picker with the live roster candidates', async () => {
    mockQuery({ data: vm({ isViewerOrganizer: true, status: 'live', winnerCandidates: ROSTER }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    await userEvent.click(screen.getByRole('button', { name: /Completa partita/i }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
  });

  it('resets a stale mutation error before re-opening the picker', async () => {
    // A previous attempt left a 409 on the mutation; opening the picker must clear it first so
    // the re-opened modal does not immediately show the old error (review MINOR #2).
    completeState.isError = true;
    completeState.error = new ConflictError({ message: 'no live' });
    mockQuery({ data: vm({ isViewerOrganizer: true, status: 'live', winnerCandidates: ROSTER }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    await userEvent.click(screen.getByRole('button', { name: /Completa partita/i }));
    expect(completeResetMock).toHaveBeenCalledTimes(1);
  });

  it('completes with the selected winner participant id', async () => {
    mockQuery({ data: vm({ isViewerOrganizer: true, status: 'live', winnerCandidates: ROSTER }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    await userEvent.click(screen.getByRole('button', { name: /Completa partita/i }));
    await userEvent.click(screen.getByRole('radio', { name: 'Alice' }));
    await userEvent.click(screen.getByRole('button', { name: 'Completa' }));
    expect(completeMutateMock).toHaveBeenCalledWith(
      { winnerId: ROSTER[0].id },
      expect.objectContaining({ onSuccess: expect.any(Function) })
    );
  });

  it('completes with no winner when "Nessun vincitore" is selected (default)', async () => {
    mockQuery({ data: vm({ isViewerOrganizer: true, status: 'live', winnerCandidates: ROSTER }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    await userEvent.click(screen.getByRole('button', { name: /Completa partita/i }));
    await userEvent.click(screen.getByRole('button', { name: 'Completa' }));
    expect(completeMutateMock).toHaveBeenCalledWith(
      { winnerId: undefined },
      expect.objectContaining({ onSuccess: expect.any(Function) })
    );
  });
});

describe('NightLiveClientView — terminal nights (LD-14)', () => {
  it('redirects a Completed night to the summary route', () => {
    mockQuery({ data: vm({ nightStatus: 'Completed' }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(replaceMock).toHaveBeenCalledWith(`/game-nights/${NIGHT_ID}/summary`);
  });

  it('renders a cancelled state for a Cancelled night (no hub)', () => {
    mockQuery({ data: vm({ nightStatus: 'Cancelled' }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(screen.getByRole('heading', { name: /annullata/i })).toBeInTheDocument();
    expect(screen.queryByText('Brass')).toBeNull();
  });

  it('renders a not-live state for a Draft night (no hub)', () => {
    mockQuery({ data: vm({ nightStatus: 'Draft' }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(screen.getByText(/non ancora avviata/i)).toBeInTheDocument();
    expect(screen.queryByText('Brass')).toBeNull();
  });
});

describe('NightLiveClientView — error taxonomy (LD-10)', () => {
  it('401 UnauthorizedError → session-expired copy', () => {
    mockQuery({ isError: true, error: new UnauthorizedError({ message: 'x' }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(screen.getByText(/Sessione scaduta/i)).toBeInTheDocument();
  });

  it('401 UnauthorizedError → offers a login action routing to /login (LD-10 recovery)', async () => {
    mockQuery({ isError: true, error: new UnauthorizedError({ message: 'x' }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    await userEvent.click(screen.getByRole('button', { name: /Accedi/i }));
    expect(pushMock).toHaveBeenCalledWith('/login');
  });

  it('CircuitBreakerError → connection-lost copy (AC9)', () => {
    const err = new Error('circuit open');
    err.name = 'CircuitBreakerError';
    mockQuery({ isError: true, error: err });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(screen.getByRole('heading', { name: /Connessione persa/i })).toBeInTheDocument();
  });

  it('403 ForbiddenError → non-participant copy', () => {
    mockQuery({ isError: true, error: new ForbiddenError({ message: 'x' }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(screen.getByRole('heading', { name: /riservato/i })).toBeInTheDocument();
  });

  it('404 NotFoundError → not-found copy', () => {
    mockQuery({ isError: true, error: new NotFoundError({ message: 'x' }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(screen.getByText(/non trovata/i)).toBeInTheDocument();
  });

  it('NetworkError → connection-lost copy', () => {
    mockQuery({ isError: true, error: new NetworkError({ message: 'x' }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(screen.getByRole('heading', { name: /Connessione persa/i })).toBeInTheDocument();
  });
});
