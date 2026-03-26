/**
 * @vitest-environment jsdom
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { MeeplePausedSessionCard } from '../MeeplePausedSessionCard';

vi.mock('date-fns', () => ({
  formatDistanceToNow: () => '5 giorni fa',
}));

vi.mock('@/components/ui/overlays/confirmation-dialog', () => ({
  ConfirmationDialog: () => null,
}));

describe('MeeplePausedSessionCard', () => {
  const mockSession = {
    id: 'session-1',
    sessionDate: '2026-03-15T10:00:00Z',
    participants: [
      { displayName: 'Alice', score: 10 },
      { displayName: 'Bob', score: 20 },
    ],
    hasPhotos: false,
    hasNotes: false,
    hasAgentSummary: false,
  };

  it('renders with correct entity type', () => {
    render(
      <MeeplePausedSessionCard session={mockSession} onResume={vi.fn()} onAbandon={vi.fn()} />
    );
    const card = screen.getByTestId('meeple-card');
    expect(card).toHaveAttribute('data-entity', 'session');
  });

  it('displays paused session title', () => {
    render(
      <MeeplePausedSessionCard session={mockSession} onResume={vi.fn()} onAbandon={vi.fn()} />
    );
    expect(screen.getByText('Partita in pausa')).toBeInTheDocument();
  });
});
