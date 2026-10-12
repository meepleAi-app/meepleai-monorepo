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

  /**
   * Issue #4255. The scoped dark theme set the background and not the text colour. Colour is
   * inherited as an already computed value: the body resolves it in the light theme of <html>,
   * and every descendant without a colour class of its own got dark text on the dark shell.
   * Declaring the token on the shell root resolves it INSIDE the dark scope.
   *
   * jsdom does not compute Tailwind, so this asserts the declaration, not the rendered colour:
   * the rendered contrast is measured in the browser (see the issue).
   */
  it('sets the text colour inside the dark scope, not only the background', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'admin' } });
    const { container } = render(
      <AdminShell>
        <p>body</p>
      </AdminShell>
    );
    const root = container.querySelector('[data-admin-shell]');
    expect(root).toHaveClass('text-foreground');
  });
});
