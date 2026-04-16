import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: () => '/',
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/components/notifications', () => ({
  NotificationBell: () => <button aria-label="Notifications">🔔</button>,
}));

vi.mock('@/components/layout/UserMenuDropdown', () => ({
  UserMenuDropdown: () => <button aria-label="User menu">MR</button>,
}));

vi.mock('@/components/chat/panel/ChatSlideOverPanel', () => ({
  ChatSlideOverPanel: () => null,
}));

vi.mock('@/components/layout/SideDrawer/SideDrawer', () => ({
  SideDrawer: () => null,
}));

vi.mock('@/components/layout/SearchOverlay', () => ({
  SearchOverlay: () => null,
}));

vi.mock('@/components/layout/MobileCTAPill', () => ({
  MobileCTAPill: () => null,
}));

import { DesktopShell } from '../DesktopShell';

describe('DesktopShell', () => {
  it('renders top bar and children', () => {
    render(
      <DesktopShell>
        <div data-testid="content">hello</div>
      </DesktopShell>
    );
    expect(screen.getByTestId('top-bar')).toBeInTheDocument();
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
