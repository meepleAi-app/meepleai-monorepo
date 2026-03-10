/**
 * ResumeSessionCard Tests
 *
 * Task 4 — Session Pause/Resume Flow
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ResumeSessionCard } from '../ResumeSessionCard';

// Mock next/link — renders a plain <a> so href assertions work
vi.mock('next/link', () => ({
  default: ({
    href,
    children,
    className,
  }: {
    href: string;
    children: React.ReactNode;
    className?: string;
  }) => (
    <a href={href} className={className}>
      {children}
    </a>
  ),
}));

// Mock date-fns to get deterministic relative time output.
// addSuffix: true is used in the component, so date-fns returns e.g. "circa 2 ore fa".
vi.mock('date-fns', async () => {
  const actual = await vi.importActual<typeof import('date-fns')>('date-fns');
  return {
    ...actual,
    formatDistanceToNow: () => 'circa 2 ore fa',
  };
});

const BASE_PROPS = {
  sessionId: 's1',
  gameName: 'Azul',
  lastActivityAt: '2026-03-10T22:30:00Z',
  playerCount: 4,
  sessionCode: 'ABC123',
};

describe('ResumeSessionCard', () => {
  it('shows paused session with game name and status', () => {
    render(<ResumeSessionCard {...BASE_PROPS} photoCount={3} />);

    expect(screen.getByText('Azul')).toBeInTheDocument();
    expect(screen.getByText(/in pausa/i)).toBeInTheDocument();
    expect(screen.getByText(/3 foto/i)).toBeInTheDocument();
    expect(screen.getByText(/riprendi/i)).toBeInTheDocument();
  });

  it('hides photo count when not provided', () => {
    render(<ResumeSessionCard {...BASE_PROPS} />);

    expect(screen.queryByText(/foto/i)).not.toBeInTheDocument();
  });

  it('hides photo count when zero', () => {
    render(<ResumeSessionCard {...BASE_PROPS} photoCount={0} />);

    expect(screen.queryByText(/foto/i)).not.toBeInTheDocument();
  });

  it('links to scoreboard page', () => {
    render(<ResumeSessionCard {...BASE_PROPS} />);

    const link = screen.getByText(/riprendi/i).closest('a');
    expect(link).toHaveAttribute('href', '/sessions/s1/scoreboard');
  });

  it('shows player count', () => {
    render(<ResumeSessionCard {...BASE_PROPS} />);

    expect(screen.getByText(/4 giocatori/i)).toBeInTheDocument();
  });

  it('shows session code', () => {
    render(<ResumeSessionCard {...BASE_PROPS} />);

    expect(screen.getByText('ABC123')).toBeInTheDocument();
  });

  it('shows relative last activity time', () => {
    render(<ResumeSessionCard {...BASE_PROPS} />);

    expect(screen.getByText(/circa 2 ore fa/i)).toBeInTheDocument();
  });
});
