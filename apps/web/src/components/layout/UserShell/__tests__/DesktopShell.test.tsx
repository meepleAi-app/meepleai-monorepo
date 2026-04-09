import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: () => '/',
}));

vi.mock('@/stores/use-card-hand', () => {
  const state = {
    cards: [],
    pinnedIds: new Set<string>(),
    pinCard: vi.fn(),
    unpinCard: vi.fn(),
  };
  return {
    useCardHand: (selector?: (s: typeof state) => unknown) => (selector ? selector(state) : state),
  };
});

vi.mock('@/components/notifications', () => ({
  NotificationBell: () => <button aria-label="Notifications">🔔</button>,
}));

vi.mock('@/components/layout/UserMenuDropdown', () => ({
  UserMenuDropdown: () => <button aria-label="User menu">MR</button>,
}));

import { DesktopShell } from '../DesktopShell';

describe('DesktopShell', () => {
  it('renders top bar, mini-nav slot, hand rail and children', () => {
    render(
      <DesktopShell>
        <div data-testid="content">hello</div>
      </DesktopShell>
    );
    expect(screen.getByTestId('top-bar')).toBeInTheDocument();
    expect(screen.getByTestId('desktop-hand-rail')).toBeInTheDocument();
    expect(screen.getByTestId('content')).toBeInTheDocument();
  });

  it('wraps children in a main landmark', () => {
    render(
      <DesktopShell>
        <div>child</div>
      </DesktopShell>
    );
    expect(screen.getByRole('main')).toBeInTheDocument();
  });
});
