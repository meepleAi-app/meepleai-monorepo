import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { LiveSessionWidget } from '../widgets/LiveSessionWidget';
import type { SessionSummaryDto } from '@/lib/api/dashboard-client';

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

const SESSION: SessionSummaryDto = {
  id: 'sess-1',
  gameName: 'Catan',
  sessionDate: '2026-04-01T20:00:00Z',
  playerCount: 4,
  winnerName: 'Marco',
};

describe('LiveSessionWidget', () => {
  beforeEach(() => mockPush.mockClear());

  it('renders skeleton when loading', () => {
    render(
      <LiveSessionWidget session={undefined} isLoading={true} error={null} onRetry={vi.fn()} />
    );
    expect(screen.queryByText('Sessione Live')).not.toBeInTheDocument();
    expect(screen.queryByText('Nessuna sessione attiva')).not.toBeInTheDocument();
  });

  it('renders empty state when no session', () => {
    render(
      <LiveSessionWidget session={undefined} isLoading={false} error={null} onRetry={vi.fn()} />
    );
    expect(screen.getByText('Nessuna sessione attiva')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Nuova partita' })).toBeInTheDocument();
  });

  it('shows error message and retry button', async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    render(
      <LiveSessionWidget
        session={undefined}
        isLoading={false}
        error="Errore di rete"
        onRetry={onRetry}
      />
    );
    expect(screen.getByText('Errore nel caricamento')).toBeInTheDocument();
    expect(screen.getByText('Errore di rete')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Riprova' }));
    expect(onRetry).toHaveBeenCalledOnce();
  });

  it('renders session info when active', () => {
    render(
      <LiveSessionWidget session={SESSION} isLoading={false} error={null} onRetry={vi.fn()} />
    );
    expect(screen.getByText('Sessione Live')).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText(/4 giocatori/)).toBeInTheDocument();
    expect(screen.getByText(/Vincitore: Marco/)).toBeInTheDocument();
  });

  it('navigates to session on click', async () => {
    const user = userEvent.setup();
    render(
      <LiveSessionWidget session={SESSION} isLoading={false} error={null} onRetry={vi.fn()} />
    );
    await user.click(screen.getByText('Catan'));
    expect(mockPush).toHaveBeenCalledWith('/sessions/sess-1');
  });
});
