import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { PlayerDrawerContent } from '../PlayerDrawerContent';
import type { PlayerDetailData } from '../../types';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

// Mock usePlayerDetail hook
vi.mock('../../hooks', () => ({
  usePlayerDetail: vi.fn(),
}));

import { usePlayerDetail } from '../../hooks';

const mockPlayer: PlayerDetailData = {
  id: 'p1',
  displayName: 'Mario Rossi',
  gamesPlayed: 42,
  winRate: 0.65,
  totalSessions: 20,
  favoriteGame: 'Catan',
  achievements: [{ id: 'a1', name: 'Primo Sangue', icon: '⚔️' }],
  recentGames: [{ name: 'Azul', date: '2026-04-01', result: 'win' }],
};

describe('PlayerDrawerContent', () => {
  beforeEach(() => {
    vi.mocked(usePlayerDetail).mockReturnValue({
      data: mockPlayer,
      loading: false,
      error: null,
      retry: vi.fn(),
    });
  });

  it('renders player name', () => {
    render(<PlayerDrawerContent entityId="p1" />);
    expect(screen.getByText('Mario Rossi')).toBeInTheDocument();
  });

  it('renders 3 tabs', () => {
    render(<PlayerDrawerContent entityId="p1" />);
    expect(screen.getByRole('tab', { name: /profilo/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /stats/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /storico/i })).toBeInTheDocument();
  });

  it('shows stats in profilo tab by default', () => {
    render(<PlayerDrawerContent entityId="p1" />);
    expect(screen.getByText('42')).toBeInTheDocument(); // gamesPlayed
  });

  it('renders action footer with Apri button', () => {
    render(<PlayerDrawerContent entityId="p1" />);
    expect(screen.getByText('Apri')).toBeInTheDocument();
  });

  it('shows loading skeleton when loading', () => {
    vi.mocked(usePlayerDetail).mockReturnValue({
      data: null,
      loading: true,
      error: null,
      retry: vi.fn(),
    });
    render(<PlayerDrawerContent entityId="p1" />);
    expect(screen.getByTestId('drawer-loading-skeleton')).toBeInTheDocument();
  });

  it('shows error state on error', () => {
    vi.mocked(usePlayerDetail).mockReturnValue({
      data: null,
      loading: false,
      error: 'Giocatore non trovato',
      retry: vi.fn(),
    });
    render(<PlayerDrawerContent entityId="p1" />);
    expect(screen.getByTestId('drawer-error-state')).toBeInTheDocument();
  });
});
