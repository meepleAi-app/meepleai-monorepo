/**
 * PausedSessionCard (live/private-game-detail) — Tests
 *
 * Game Night Improvvisata — Task 20
 *
 * Tests the PausedSessionCard already located in
 * components/library/private-game-detail/PausedSessionCard.tsx
 * to verify the resume navigation works correctly.
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import {
  PausedSessionCard,
  type PausedSession,
} from '@/components/library/private-game-detail/PausedSessionCard';

const MOCK_SESSION: PausedSession = {
  id: 'session-abc',
  sessionDate: new Date().toISOString(),
  participants: [
    { displayName: 'Mario', score: 42 },
    { displayName: 'Luigi', score: 18 },
  ],
  hasPhotos: false,
  hasNotes: false,
  hasAgentSummary: false,
};

describe('PausedSessionCard', () => {
  it('renders participant names and scores', () => {
    render(<PausedSessionCard session={MOCK_SESSION} onResume={vi.fn()} onAbandon={vi.fn()} />);

    expect(screen.getByText(/Mario: 42/)).toBeInTheDocument();
    expect(screen.getByText(/Luigi: 18/)).toBeInTheDocument();
  });

  it('shows resume button', () => {
    render(<PausedSessionCard session={MOCK_SESSION} onResume={vi.fn()} onAbandon={vi.fn()} />);

    expect(screen.getByRole('button', { name: /riprendi/i })).toBeInTheDocument();
  });

  it('calls onResume with session id when resume clicked', async () => {
    const onResume = vi.fn();
    const user = userEvent.setup();

    render(<PausedSessionCard session={MOCK_SESSION} onResume={onResume} onAbandon={vi.fn()} />);

    await user.click(screen.getByRole('button', { name: /riprendi/i }));
    expect(onResume).toHaveBeenCalledWith('session-abc');
  });

  it('calls onAbandon when abandon button clicked', async () => {
    const onAbandon = vi.fn();
    const user = userEvent.setup();

    render(<PausedSessionCard session={MOCK_SESSION} onResume={vi.fn()} onAbandon={onAbandon} />);

    await user.click(screen.getByRole('button', { name: /abbandona/i }));
    expect(onAbandon).toHaveBeenCalledWith('session-abc');
  });

  it('marks old sessions with a visual indicator', () => {
    const oldSession: PausedSession = {
      ...MOCK_SESSION,
      sessionDate: new Date(Date.now() - 31 * 24 * 60 * 60 * 1000).toISOString(),
    };

    render(<PausedSessionCard session={oldSession} onResume={vi.fn()} onAbandon={vi.fn()} />);

    expect(screen.getByText(/vecchia/i)).toBeInTheDocument();
  });
});
