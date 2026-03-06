/**
 * DashboardSessionHero — Unit Tests
 * Issue #5095, Epic #5094
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { DashboardSessionHero } from '../session-hero';

// ─── Mocks ───────────────────────────────────────────────────────────────────

vi.mock('@/hooks/queries/useActiveSessions', () => ({
  useActiveSessions: vi.fn(),
}));

vi.mock('@/hooks/queries/useGames', () => ({
  useGame: vi.fn(() => ({ data: null })),
}));

const { useActiveSessions } = await import('@/hooks/queries/useActiveSessions');

const mockActiveSession = {
  id: 'session-1',
  gameId: 'game-1',
  status: 'InProgress',
  startedAt: new Date().toISOString(),
  completedAt: null,
  playerCount: 4,
  players: [],
  winnerName: null,
  notes: null,
  durationMinutes: 75,
};

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('DashboardSessionHero', () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it('renders loading skeleton while fetching', () => {
    (useActiveSessions as ReturnType<typeof vi.fn>).mockReturnValue({
      data: undefined,
      isLoading: true,
    });

    const { container } = render(<DashboardSessionHero />);
    // Skeleton renders without crashing
    expect(container.firstChild).toBeTruthy();
  });

  it('renders active hero when a session is in progress', () => {
    (useActiveSessions as ReturnType<typeof vi.fn>).mockReturnValue({
      data: { sessions: [mockActiveSession], total: 1, page: 1, pageSize: 1 },
      isLoading: false,
    });

    render(<DashboardSessionHero />);

    expect(screen.getByText('Sessione attiva')).toBeInTheDocument();
    expect(screen.getByText(/4 giocatori/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Riprendi/ })).toHaveAttribute(
      'href',
      '/sessions/session-1'
    );
  });

  it('shows game title from useGame when available', async () => {
    const { useGame } = await import('@/hooks/queries/useGames');
    (useGame as ReturnType<typeof vi.fn>).mockReturnValue({
      data: { title: 'I Coloni di Catan' },
    });
    (useActiveSessions as ReturnType<typeof vi.fn>).mockReturnValue({
      data: { sessions: [mockActiveSession], total: 1, page: 1, pageSize: 1 },
      isLoading: false,
    });

    render(<DashboardSessionHero />);

    expect(screen.getByText('I Coloni di Catan')).toBeInTheDocument();
  });

  it('falls back to "Sessione in corso" when game title is unavailable', async () => {
    const { useGame } = await import('@/hooks/queries/useGames');
    (useGame as ReturnType<typeof vi.fn>).mockReturnValue({ data: null });
    (useActiveSessions as ReturnType<typeof vi.fn>).mockReturnValue({
      data: { sessions: [mockActiveSession], total: 1, page: 1, pageSize: 1 },
      isLoading: false,
    });

    render(<DashboardSessionHero />);

    expect(screen.getByText('Sessione in corso')).toBeInTheDocument();
  });

  it('renders nothing when no active sessions and no last session', () => {
    (useActiveSessions as ReturnType<typeof vi.fn>).mockReturnValue({
      data: { sessions: [], total: 0, page: 1, pageSize: 1 },
      isLoading: false,
    });

    const { container } = render(<DashboardSessionHero />);

    expect(container.firstChild).toBeNull();
  });

  it('renders empty state when no active sessions but has last session', () => {
    (useActiveSessions as ReturnType<typeof vi.fn>).mockReturnValue({
      data: { sessions: [], total: 0, page: 1, pageSize: 1 },
      isLoading: false,
    });

    const lastSession = {
      id: 's-recent',
      gameName: 'Azul',
      sessionDate: new Date().toISOString(),
      playerCount: 3,
    };

    render(<DashboardSessionHero lastSession={lastSession as never} />);

    expect(screen.getByText('Nessuna sessione in corso')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Nuova sessione/ })).toHaveAttribute(
      'href',
      '/sessions/new'
    );
  });

  it('shows last session info and "Riprendi ultima" CTA in empty state', () => {
    (useActiveSessions as ReturnType<typeof vi.fn>).mockReturnValue({
      data: { sessions: [], total: 0, page: 1, pageSize: 1 },
      isLoading: false,
    });

    const lastSession = {
      id: 's-old',
      gameName: 'Wingspan',
      gameImageUrl: null,
      sessionDate: new Date(Date.now() - 3 * 24 * 60 * 60 * 1000).toISOString(),
      playerCount: 2,
    };

    render(<DashboardSessionHero lastSession={lastSession as never} />);

    expect(screen.getByText(/Wingspan/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Riprendi ultima/ })).toHaveAttribute(
      'href',
      '/sessions'
    );
  });

  it('hides "Riprendi ultima" CTA in empty state when lastSession is undefined', () => {
    (useActiveSessions as ReturnType<typeof vi.fn>).mockReturnValue({
      data: { sessions: [], total: 0, page: 1, pageSize: 1 },
      isLoading: false,
    });

    // With no lastSession, the component returns null entirely
    const { container } = render(<DashboardSessionHero />);

    expect(container.firstChild).toBeNull();
    expect(screen.queryByText(/Riprendi ultima/)).not.toBeInTheDocument();
  });
});
