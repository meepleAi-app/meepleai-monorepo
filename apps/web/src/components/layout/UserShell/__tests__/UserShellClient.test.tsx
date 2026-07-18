/**
 * UserShellClient tests — #3146 Slice 2 review fix.
 *
 * Immersive routes replace the global navbar chrome with an in-session layout.
 * MobileBottomBar + DesktopShell already honour isImmersiveRoute; this asserts
 * the OTHER global fixed bottom-surface components (BackToSessionFAB, the
 * ContextualHandBottomBar) are suppressed too, so an immersive view (e.g. the
 * GameNight live hub with its own tab-nav) is the sole bottom surface.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { UserShellClient } from '../UserShellClient';

let mockPathname = '/dashboard';
vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname,
}));

vi.mock('../DesktopShell', () => ({
  DesktopShell: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}));
vi.mock('@/components/features/status-banner', () => ({ StatusBanner: () => null }));
vi.mock('@/components/dashboard', () => ({
  DashboardEngineProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));
vi.mock('@/components/session/BackToSessionFAB', () => ({
  BackToSessionFAB: () => <div data-testid="back-to-session-fab" />,
}));
vi.mock('@/components/layout/ContextualHand', () => ({
  ContextualHandBottomBar: () => <div data-testid="contextual-hand-bottom-bar" />,
}));

describe('UserShellClient — global bottom chrome gating', () => {
  it('renders FAB + contextual-hand on a normal (non-immersive) route', () => {
    mockPathname = '/dashboard';
    render(<UserShellClient>content</UserShellClient>);
    expect(screen.getByTestId('back-to-session-fab')).toBeInTheDocument();
    expect(screen.getByTestId('contextual-hand-bottom-bar')).toBeInTheDocument();
  });

  it('suppresses FAB + contextual-hand on the GameNight live hub (immersive)', () => {
    mockPathname = '/game-nights/night-1/live';
    render(<UserShellClient>content</UserShellClient>);
    expect(screen.queryByTestId('back-to-session-fab')).not.toBeInTheDocument();
    expect(screen.queryByTestId('contextual-hand-bottom-bar')).not.toBeInTheDocument();
  });

  it('suppresses them on the session live view (immersive) too', () => {
    mockPathname = '/sessions/abc/live';
    render(<UserShellClient>content</UserShellClient>);
    expect(screen.queryByTestId('back-to-session-fab')).not.toBeInTheDocument();
    expect(screen.queryByTestId('contextual-hand-bottom-bar')).not.toBeInTheDocument();
  });
});
