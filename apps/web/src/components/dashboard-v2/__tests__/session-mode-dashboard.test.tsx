import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { SessionModeDashboard } from '../session-mode-dashboard';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/stores/use-card-hand', () => ({
  useCardHand: () => ({ drawCard: vi.fn() }),
}));

vi.mock('@/hooks/queries/useGames', () => ({
  useGame: () => ({ data: { title: 'Catan', id: 'g1' } }),
}));

vi.mock('@/hooks/queries/useActiveSessions', () => ({
  useActiveSessions: () => ({
    data: {
      sessions: [{ id: 's1', gameId: 'g1', playerCount: 4, durationMinutes: 45, status: 'Active' }],
    },
    isLoading: false,
  }),
}));

const session = {
  id: 's1',
  gameId: 'g1',
  playerCount: 4,
  durationMinutes: 45,
  status: 'Active' as const,
};

describe('SessionModeDashboard', () => {
  it('renders session info with game title', () => {
    render(<SessionModeDashboard session={session} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('renders quick action cards', () => {
    render(<SessionModeDashboard session={session} />);
    expect(screen.getByText('Regole')).toBeInTheDocument();
    expect(screen.getByText('FAQ')).toBeInTheDocument();
    expect(screen.getByText('Chiedi AI')).toBeInTheDocument();
    expect(screen.getByText('Punteggi')).toBeInTheDocument();
  });

  it('shows live session timer', () => {
    render(<SessionModeDashboard session={session} />);
    expect(screen.getByTestId('session-timer')).toBeInTheDocument();
  });
});
