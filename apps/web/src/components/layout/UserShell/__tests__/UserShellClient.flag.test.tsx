import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, afterEach } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: () => '/',
}));

vi.mock('@/stores/use-card-hand', () => {
  const state = {
    cards: [],
    pinnedIds: new Set(),
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

vi.mock('@/components/layout/AppNavbar', () => ({
  AppNavbar: () => <nav data-testid="legacy-navbar">Legacy Navbar</nav>,
}));

vi.mock('@/components/dashboard', () => ({
  DashboardEngineProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

vi.mock('@/components/session/BackToSessionFAB', () => ({
  BackToSessionFAB: () => null,
}));

const originalEnv = process.env.NEXT_PUBLIC_UX_REDESIGN;

describe('UserShellClient feature flag', () => {
  afterEach(() => {
    if (originalEnv === undefined) delete process.env.NEXT_PUBLIC_UX_REDESIGN;
    else process.env.NEXT_PUBLIC_UX_REDESIGN = originalEnv;
    vi.resetModules();
  });

  it('renders legacy AppNavbar when flag is off', async () => {
    process.env.NEXT_PUBLIC_UX_REDESIGN = 'false';
    const { UserShellClient } = await import('../UserShellClient');
    render(
      <UserShellClient>
        <div>child</div>
      </UserShellClient>
    );
    expect(screen.getByTestId('legacy-navbar')).toBeInTheDocument();
    expect(screen.queryByTestId('top-bar-64')).not.toBeInTheDocument();
  });

  it('renders DesktopShell when flag is on', async () => {
    process.env.NEXT_PUBLIC_UX_REDESIGN = 'true';
    vi.resetModules();
    const { UserShellClient } = await import('../UserShellClient');
    render(
      <UserShellClient>
        <div>child</div>
      </UserShellClient>
    );
    expect(screen.getByTestId('top-bar-64')).toBeInTheDocument();
    expect(screen.queryByTestId('legacy-navbar')).not.toBeInTheDocument();
  });
});
