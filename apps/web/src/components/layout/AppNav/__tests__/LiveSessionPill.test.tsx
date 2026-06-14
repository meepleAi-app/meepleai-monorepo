/**
 * LiveSessionPill — unit tests (#2150 runtime backfill).
 *
 * Covers the 3 visual states (hidden / live / paused), elapsed-format edge
 * cases, the a11y label contract, and the gameName fallback.
 */

import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

// ---------------------------------------------------------------------------
// Mocks (registered before component import)
// ---------------------------------------------------------------------------

vi.mock('next/link', () => ({
  default: ({
    href,
    children,
    ...rest
  }: {
    href: string;
    children: React.ReactNode;
  } & React.AnchorHTMLAttributes<HTMLAnchorElement>) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

interface MockStoreSnapshot {
  sessionId: string | null;
  gameName: string;
  status: 'InProgress' | 'Paused' | 'Completed';
  elapsedSeconds: number;
}

const mockStore: MockStoreSnapshot = {
  sessionId: null,
  gameName: '',
  status: 'InProgress',
  elapsedSeconds: 0,
};

vi.mock('@/lib/stores/live-session-store', () => ({
  useLiveSessionStore: (selector: (s: MockStoreSnapshot) => unknown) => selector(mockStore),
}));

// Import AFTER mocks
import { LiveSessionPill } from '../LiveSessionPill';

function setStore(patch: Partial<MockStoreSnapshot>) {
  Object.assign(mockStore, patch);
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('LiveSessionPill', () => {
  beforeEach(() => {
    setStore({ sessionId: null, gameName: '', status: 'InProgress', elapsedSeconds: 0 });
  });

  it('renders nothing when no session is active', () => {
    const { container } = render(<LiveSessionPill />);
    expect(container).toBeEmptyDOMElement();
  });

  it('renders nothing when the session has completed', () => {
    setStore({
      sessionId: 'sess-1',
      gameName: 'Catan',
      status: 'Completed',
      elapsedSeconds: 3600,
    });
    const { container } = render(<LiveSessionPill />);
    expect(container).toBeEmptyDOMElement();
  });

  it('renders the game name and elapsed time when a session is in progress', () => {
    setStore({
      sessionId: 'sess-1',
      gameName: 'Catan',
      status: 'InProgress',
      elapsedSeconds: 90, // 1:30
    });
    render(<LiveSessionPill />);

    const pill = screen.getByTestId('live-session-pill');
    expect(pill).toHaveAttribute('href', '/sessions/sess-1/live');
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('1:30')).toBeInTheDocument();
    expect(pill).not.toHaveAttribute('data-paused');
    expect(screen.queryByText('Pausa')).not.toBeInTheDocument();
  });

  it('formats elapsed time as h:mm when longer than 1 hour', () => {
    setStore({
      sessionId: 'sess-1',
      gameName: 'Twilight Imperium',
      status: 'InProgress',
      elapsedSeconds: 3 * 3600 + 25 * 60, // 3:25
    });
    render(<LiveSessionPill />);
    expect(screen.getByText('3:25')).toBeInTheDocument();
  });

  it('falls back to "Sessione" when gameName is empty', () => {
    setStore({
      sessionId: 'sess-1',
      gameName: '   ',
      status: 'InProgress',
      elapsedSeconds: 42,
    });
    render(<LiveSessionPill />);
    expect(screen.getByText('Sessione')).toBeInTheDocument();
  });

  it('shows the "Pausa" badge and amber styling when paused', () => {
    setStore({
      sessionId: 'sess-1',
      gameName: 'Catan',
      status: 'Paused',
      elapsedSeconds: 600, // 10:00
    });
    render(<LiveSessionPill />);
    const pill = screen.getByTestId('live-session-pill');
    expect(pill).toHaveAttribute('data-paused', 'true');
    expect(screen.getByText('Pausa')).toBeInTheDocument();
  });

  it('uses a paused-specific aria-label when paused', () => {
    setStore({
      sessionId: 'sess-1',
      gameName: 'Catan',
      status: 'Paused',
      elapsedSeconds: 600,
    });
    render(<LiveSessionPill />);
    const pill = screen.getByTestId('live-session-pill');
    expect(pill).toHaveAccessibleName(/in pausa/i);
    expect(pill).toHaveAccessibleName(/10:00/);
  });

  it('uses an active-specific aria-label when in progress', () => {
    setStore({
      sessionId: 'sess-1',
      gameName: 'Catan',
      status: 'InProgress',
      elapsedSeconds: 75,
    });
    render(<LiveSessionPill />);
    const pill = screen.getByTestId('live-session-pill');
    expect(pill).toHaveAccessibleName(/attiva/i);
    expect(pill).toHaveAccessibleName(/1:15/);
  });

  it('clamps negative / non-finite elapsed seconds to "0:00"', () => {
    setStore({
      sessionId: 'sess-1',
      gameName: 'Catan',
      status: 'InProgress',
      elapsedSeconds: -42,
    });
    render(<LiveSessionPill />);
    expect(screen.getByText('0:00')).toBeInTheDocument();
  });
});
