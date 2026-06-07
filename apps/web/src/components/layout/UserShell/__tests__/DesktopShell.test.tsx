import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('@/components/layout/AppNav/AppTopBar', () => ({
  AppTopBar: () => <div data-testid="app-top-bar" />,
}));

vi.mock('@/components/layout/AppNav/MobileTopBar', () => ({
  MobileTopBar: () => <div data-testid="mobile-top-bar" />,
}));

vi.mock('@/components/layout/AppNav/MobileBottomBar', () => ({
  MobileBottomBar: () => <div data-testid="mobile-bottom-bar" />,
}));

vi.mock('@/components/chat/panel/ChatSlideOverPanel', () => ({
  ChatSlideOverPanel: () => null,
}));

vi.mock('@/components/layout/SideDrawer/SideDrawer', () => ({
  SideDrawer: () => null,
}));

// #1977 (audit follow-up of umbrella #1974, finding F18): MainSidebar mount
// was removed from DesktopShell to dedupe primary navigation with the
// AppTopBar. The component is no longer imported by the shell, so no stub
// is needed here. `MAIN_NAV_ITEMS` + `MainNavList` are still consumed by the
// mobile drawer; their own test suites cover that path.

vi.mock('@/components/layout/UserShell/SessionBanner', () => ({
  SessionBanner: () => null,
}));

vi.mock('@/components/layout/UserShell/MiniNavSlot', () => ({
  MiniNavSlot: () => <div data-testid="mini-nav-slot" />,
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
}));

import { DesktopShell } from '../DesktopShell';

describe('DesktopShell', () => {
  it('renders the desktop and mobile top bars plus children', () => {
    render(
      <DesktopShell>
        <div data-testid="content">hello</div>
      </DesktopShell>
    );
    expect(screen.getByTestId('app-top-bar')).toBeInTheDocument();
    expect(screen.getByTestId('mobile-top-bar')).toBeInTheDocument();
    expect(screen.getByTestId('content')).toBeInTheDocument();
  });

  it('renders the mobile bottom bar', () => {
    render(
      <DesktopShell>
        <div>child</div>
      </DesktopShell>
    );
    expect(screen.getByTestId('mobile-bottom-bar')).toBeInTheDocument();
  });

  it('wraps children in a main landmark', () => {
    render(
      <DesktopShell>
        <div>child</div>
      </DesktopShell>
    );
    expect(screen.getByRole('main')).toBeInTheDocument();
  });

  it('applies bottom-bar clearance padding on non-immersive routes', () => {
    render(
      <DesktopShell>
        <div>child</div>
      </DesktopShell>
    );
    expect(screen.getByRole('main').className).toContain('pb-16');
  });

  it('mounts the MiniNavSlot so per-page useMiniNavConfig renders (F23a #1974)', () => {
    // F23a regression guard: routes like /games + /library register a
    // mini-nav config via `useMiniNavConfig`, but pre-fix the shell never
    // mounted any consumer of that store, so the tab strip was silently
    // dropped. MiniNavSlot is a no-op when no page has registered, so the
    // shell can keep it mounted unconditionally.
    render(
      <DesktopShell>
        <div>child</div>
      </DesktopShell>
    );
    expect(screen.getByTestId('mini-nav-slot')).toBeInTheDocument();
  });
});
