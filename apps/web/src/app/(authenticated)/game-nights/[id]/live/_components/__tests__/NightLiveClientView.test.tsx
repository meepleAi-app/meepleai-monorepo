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
  ForbiddenError,
  NotFoundError,
  UnauthorizedError,
  NetworkError,
} from '@/lib/api/core/errors';
import type { NightLiveViewModel } from '@/lib/game-nights/mapNightLive';

import { NightLiveClientView } from '../NightLiveClientView';

const useGameNightLiveMock = vi.hoisted(() => vi.fn());
vi.mock('@/lib/game-nights/hooks/useGameNightLive', () => ({
  useGameNightLive: useGameNightLiveMock,
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
    diaryEvents: [],
    diaryGames: [],
    diaryPlayers: [],
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
});

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
