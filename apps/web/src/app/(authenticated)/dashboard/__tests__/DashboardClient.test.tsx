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
  useLibrary: () => ({ data: { items: [] }, isLoading: false }),
  useAddGameToLibrary: () => ({ mutateAsync: vi.fn() }),
}));

vi.mock('@/hooks/queries/useActiveSessions', () => ({
  useActiveSessions: () => ({ data: { sessions: [] }, isLoading: false }),
}));

vi.mock('@/hooks/queries/useAgents', () => ({
  useAgents: () => ({ data: [], isLoading: false }),
}));

vi.mock('@/hooks/queries/useGames', () => ({
  useGames: () => ({ data: { games: [] }, isLoading: false }),
}));

vi.mock('@/hooks/queries/useBatchGameStatus', () => ({
  useBatchGameStatus: () => ({ data: { results: {} } }),
}));

import { DashboardClient } from '../DashboardClient';

describe('DashboardClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
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
});
