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

vi.mock('@/components/layout/UserShell/SessionBanner', () => ({
  SessionBanner: () => null,
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
});
