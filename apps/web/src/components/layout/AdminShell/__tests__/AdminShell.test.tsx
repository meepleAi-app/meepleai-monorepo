import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { AdminShell } from '../AdminShell';

const mockUseCurrentUser = vi.fn();

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => mockUseCurrentUser(),
}));
vi.mock('next/navigation', () => ({ usePathname: () => '/admin/overview' }));
vi.mock('@/components/layout/AppNav/AppTopBar', () => ({
  AppTopBar: () => <div data-testid="app-top-bar" />,
}));
vi.mock('@/components/layout/AppNav/MobileTopBar', () => ({
  MobileTopBar: () => <div data-testid="mobile-top-bar" />,
}));
vi.mock('@/components/dashboard', () => ({
  DashboardEngineProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

describe('AdminShell', () => {
  beforeEach(() => mockUseCurrentUser.mockReset());

  it('renders the persistent sidebar and the main content', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'admin' } });
    render(
      <AdminShell>
        <p>page body</p>
      </AdminShell>
    );
    expect(screen.getByRole('navigation', { name: /admin sidebar/i })).toBeInTheDocument();
    expect(screen.getByText('page body')).toBeInTheDocument();
  });

  it('renders the new sp4 top bars (desktop + mobile)', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'admin' } });
    render(
      <AdminShell>
        <p>body</p>
      </AdminShell>
    );
    expect(screen.getByTestId('app-top-bar')).toBeInTheDocument();
    expect(screen.getByTestId('mobile-top-bar')).toBeInTheDocument();
  });

  it('scopes the dark theme to the admin shell root', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'admin' } });
    const { container } = render(
      <AdminShell>
        <p>body</p>
      </AdminShell>
    );
    const root = container.querySelector('[data-admin-shell]');
    expect(root).not.toBeNull();
    expect(root).toHaveAttribute('data-theme', 'dark');
  });
});
