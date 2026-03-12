import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { PausedSessionCard } from '@/components/library/private-game-detail/PausedSessionCard';

const baseSession = {
  id: 'sess-1',
  sessionDate: new Date(Date.now() - 3600_000).toISOString(), // 1 hour ago
  participants: [
    { displayName: 'Alice', score: 42 },
    { displayName: 'Bob', score: 38 },
  ],
  hasPhotos: false,
  hasNotes: true,
  hasAgentSummary: false,
};

describe('PausedSessionCard', () => {
  it('renders participant scores', () => {
    render(<PausedSessionCard session={baseSession} onResume={vi.fn()} onAbandon={vi.fn()} />);
    expect(screen.getByText(/Alice: 42/)).toBeInTheDocument();
    expect(screen.getByText(/Bob: 38/)).toBeInTheDocument();
  });

  it('renders "Partita in pausa" label', () => {
    render(<PausedSessionCard session={baseSession} onResume={vi.fn()} onAbandon={vi.fn()} />);
    expect(screen.getByText('Partita in pausa')).toBeInTheDocument();
  });

  it('shows Note indicator when hasNotes is true', () => {
    render(<PausedSessionCard session={baseSession} onResume={vi.fn()} onAbandon={vi.fn()} />);
    expect(screen.getByText('Note')).toBeInTheDocument();
  });

  it('does not show Riepilogo AI indicator when hasAgentSummary is false', () => {
    render(<PausedSessionCard session={baseSession} onResume={vi.fn()} onAbandon={vi.fn()} />);
    expect(screen.queryByText('Riepilogo AI')).not.toBeInTheDocument();
  });

  it('calls onResume with sessionId when Riprendi is clicked', () => {
    const onResume = vi.fn();
    render(<PausedSessionCard session={baseSession} onResume={onResume} onAbandon={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: /riprendi/i }));
    expect(onResume).toHaveBeenCalledWith('sess-1');
  });

  it('calls onAbandon with sessionId when Abbandona is clicked', () => {
    const onAbandon = vi.fn();
    render(<PausedSessionCard session={baseSession} onResume={vi.fn()} onAbandon={onAbandon} />);
    fireEvent.click(screen.getByRole('button', { name: /abbandona/i }));
    expect(onAbandon).toHaveBeenCalledWith('sess-1');
  });

  it('shows "Vecchia" badge for sessions older than 30 days', () => {
    const oldSession = {
      ...baseSession,
      sessionDate: new Date(Date.now() - 31 * 24 * 60 * 60 * 1000).toISOString(),
    };
    render(<PausedSessionCard session={oldSession} onResume={vi.fn()} onAbandon={vi.fn()} />);
    expect(screen.getByText('Vecchia')).toBeInTheDocument();
  });

  it('does not show "Vecchia" badge for recent sessions', () => {
    render(<PausedSessionCard session={baseSession} onResume={vi.fn()} onAbandon={vi.fn()} />);
    expect(screen.queryByText('Vecchia')).not.toBeInTheDocument();
  });

  it('shows turn info when currentTurn and totalTurns are provided', () => {
    const sessionWithTurns = {
      ...baseSession,
      currentTurn: 3,
      totalTurns: 10,
    };
    render(<PausedSessionCard session={sessionWithTurns} onResume={vi.fn()} onAbandon={vi.fn()} />);
    expect(screen.getByText(/Turno 3 di 10/)).toBeInTheDocument();
  });
});
