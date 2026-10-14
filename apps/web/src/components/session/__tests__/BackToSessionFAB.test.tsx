/**
 * BackToSessionFAB — #4156 (D4): the "back to the game" FAB opens the live page of the
 * LiveGameSession correlated to the active GameSession, never the GameSession id.
 */

import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { BackToSessionFAB } from '../BackToSessionFAB';

const usePathnameMock = vi.hoisted(() => vi.fn(() => '/dashboard'));
const useActiveSessionsMock = vi.hoisted(() => vi.fn());

vi.mock('next/navigation', () => ({
  usePathname: () => usePathnameMock(),
}));

vi.mock('@/hooks/queries/useActiveSessions', () => ({
  useActiveSessions: () => useActiveSessionsMock(),
}));

function activeSession(liveSessionId: string | null) {
  return { data: { sessions: [{ id: 'game-session-1', liveSessionId }] } };
}

describe('BackToSessionFAB', () => {
  beforeEach(() => {
    usePathnameMock.mockReturnValue('/dashboard');
  });

  it('opens the live page of the correlated live session', () => {
    useActiveSessionsMock.mockReturnValue(activeSession('live-1'));
    render(<BackToSessionFAB />);
    expect(screen.getByRole('link', { name: 'Torna alla partita in corso' })).toHaveAttribute(
      'href',
      '/sessions/live-1/live'
    );
  });

  it('is hidden when the active session has no live session', () => {
    useActiveSessionsMock.mockReturnValue(activeSession(null));
    const { container } = render(<BackToSessionFAB />);
    expect(container).toBeEmptyDOMElement();
  });

  it('is hidden when there is no active session', () => {
    useActiveSessionsMock.mockReturnValue({ data: { sessions: [] } });
    const { container } = render(<BackToSessionFAB />);
    expect(container).toBeEmptyDOMElement();
  });

  it('is hidden on the live page itself', () => {
    useActiveSessionsMock.mockReturnValue(activeSession('live-1'));
    usePathnameMock.mockReturnValue('/sessions/live-1/live');
    const { container } = render(<BackToSessionFAB />);
    expect(container).toBeEmptyDOMElement();
  });
});
