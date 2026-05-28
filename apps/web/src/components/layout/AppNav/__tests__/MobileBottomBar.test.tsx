import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/library',
}));

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({
    data: { displayName: 'Aaron', email: 'aaron@test.com', role: 'admin' },
  }),
}));

import { MobileBottomBar, isImmersiveRoute } from '@/components/layout/AppNav/MobileBottomBar';

describe('MobileBottomBar', () => {
  it('renders the 5 fixed sp4 tabs (Dashboard tab labelled "Home")', () => {
    render(<MobileBottomBar />);
    expect(screen.getByText('Home')).toBeDefined();
    expect(screen.getByText('Libreria')).toBeDefined();
    expect(screen.getByText('Hub')).toBeDefined();
    expect(screen.getByText('Chat')).toBeDefined();
    expect(screen.getByText('Profilo')).toBeDefined();
    // The override means the dashboard tab does NOT show "Dashboard".
    expect(screen.queryByText('Dashboard')).toBeNull();
  });

  it('marks the active tab with aria-current="page"', () => {
    render(<MobileBottomBar />);
    expect(screen.getByText('Libreria').closest('a')?.getAttribute('aria-current')).toBe('page');
    expect(screen.getByText('Home').closest('a')?.getAttribute('aria-current')).toBeNull();
  });

  it('links the Home tab to /dashboard', () => {
    render(<MobileBottomBar />);
    expect(screen.getByText('Home').closest('a')?.getAttribute('href')).toBe('/dashboard');
  });
});

describe('isImmersiveRoute', () => {
  it('is true on in-session routes', () => {
    expect(isImmersiveRoute('/sessions/live/abc-123')).toBe(true);
    expect(isImmersiveRoute('/library/catan/play')).toBe(true);
    expect(isImmersiveRoute('/library/catan/play/4')).toBe(true);
  });

  it('is false on normal routes', () => {
    expect(isImmersiveRoute('/library')).toBe(false);
    expect(isImmersiveRoute('/dashboard')).toBe(false);
    expect(isImmersiveRoute('/sessions')).toBe(false);
  });
});
