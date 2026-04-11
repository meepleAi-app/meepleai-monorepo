import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => ({
    user: { id: 'u1', displayName: 'Marco' },
  }),
}));

vi.mock('@/lib/stores/mini-nav-config-store', () => {
  const state = { config: null, setConfig: vi.fn(), clear: vi.fn() };
  return {
    useMiniNavConfigStore: (selector?: (s: typeof state) => unknown) =>
      selector ? selector(state) : state,
  };
});

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibrary: vi.fn(() => ({ data: { items: [] }, isLoading: false })),
  useAddGameToLibrary: () => ({ mutateAsync: vi.fn() }),
}));

vi.mock('@/hooks/queries/useActiveSessions', () => ({
  useActiveSessions: () => ({ data: { sessions: [] }, isLoading: false }),
}));

vi.mock('@/hooks/queries/useAgents', () => ({
  useAgents: () => ({ data: [], isLoading: false }),
}));

vi.mock('@/hooks/queries/useGames', () => ({
  useGames: vi.fn(() => ({ data: { games: [] }, isLoading: false })),
}));

vi.mock('@/hooks/queries/useBatchGameStatus', () => ({
  useBatchGameStatus: () => ({ data: { results: {} } }),
}));

import { useLibrary } from '@/hooks/queries/useLibrary';
import { useGames } from '@/hooks/queries/useGames';
import { DashboardClient } from '../DashboardClient';

describe('DashboardClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useLibrary).mockReturnValue({
      data: { items: [] },
      isLoading: false,
    } as ReturnType<typeof useLibrary>);
    vi.mocked(useGames).mockReturnValue({
      data: { games: [] },
      isLoading: false,
    } as ReturnType<typeof useGames>);
  });

  it('renders the greeting with the user name', () => {
    render(<DashboardClient />);
    expect(screen.getByText(/Marco/)).toBeInTheDocument();
  });

  it('renders hub block titles', () => {
    render(<DashboardClient />);
    expect(screen.getAllByText(/Giochi/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Sessioni/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Agenti/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Toolkit/i).length).toBeGreaterThan(0);
  });

  it('shows catalog hint for new user with empty library', () => {
    render(<DashboardClient />);
    expect(screen.getByText(/Libreria vuota/i)).toBeInTheDocument();
  });

  it('shows empty CTA for sessions', () => {
    render(<DashboardClient />);
    expect(screen.getByText(/Nessuna sessione/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Crea sessione/i })).toBeInTheDocument();
  });

  it('shows empty CTA for agents with two actions', () => {
    render(<DashboardClient />);
    expect(screen.getByText(/Nessun agente attivo/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Avvia chat/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Crea agente/i })).toBeInTheDocument();
  });

  it('renders toolkit tools', () => {
    render(<DashboardClient />);
    expect(screen.getByText('Dado')).toBeInTheDocument();
    expect(screen.getByText('Clessidra')).toBeInTheDocument();
    expect(screen.getByText('Scoreboard')).toBeInTheDocument();
  });

  describe('returning user (library has games)', () => {
    beforeEach(() => {
      vi.mocked(useLibrary).mockReturnValue({
        data: {
          items: [
            {
              id: 'lib-1',
              gameId: 'g1',
              gameTitle: 'Puerto Rico',
              gamePublisher: 'Alea',
              gameImageUrl: null,
              averageRating: 8.5,
            },
          ],
          total: 1,
          page: 1,
          pageSize: 12,
        },
        isLoading: false,
      } as ReturnType<typeof useLibrary>);
    });

    it('does NOT show catalog hint banner for returning user', () => {
      render(<DashboardClient />);
      expect(screen.queryByText(/Libreria vuota/i)).not.toBeInTheDocument();
    });

    it('shows library game title for returning user', () => {
      render(<DashboardClient />);
      expect(screen.getByText('Puerto Rico')).toBeInTheDocument();
    });
  });

  describe('new user catalog sort — KB-first', () => {
    beforeEach(() => {
      vi.mocked(useGames).mockReturnValue({
        data: {
          games: [
            // rating più alta ma senza KB
            {
              id: 'g1',
              title: 'Game NoKB High Rating',
              publisher: 'Pub',
              createdAt: '2024-01-01T00:00:00Z',
              averageRating: 9.5,
              hasKnowledgeBase: false,
            },
            // rating più bassa ma con KB
            {
              id: 'g2',
              title: 'Game WithKB Low Rating',
              publisher: 'Pub',
              createdAt: '2024-01-01T00:00:00Z',
              averageRating: 7.0,
              hasKnowledgeBase: true,
            },
          ],
        },
        isLoading: false,
      } as ReturnType<typeof useGames>);
    });

    it('renders KB game before non-KB game regardless of rating', () => {
      render(<DashboardClient />);
      const gameNames = screen.getAllByText(/Game (WithKB|NoKB)/);
      // First match should be the KB game
      expect(gameNames[0]).toHaveTextContent('Game WithKB Low Rating');
      expect(gameNames[1]).toHaveTextContent('Game NoKB High Rating');
    });
  });
});
