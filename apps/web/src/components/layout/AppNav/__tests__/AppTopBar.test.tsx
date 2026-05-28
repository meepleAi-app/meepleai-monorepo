import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
}));

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({
    data: { displayName: 'Aaron', email: 'aaron@test.com', role: 'admin' },
  }),
}));

vi.mock('@/components/layout/UserMenuDropdown', () => ({
  UserMenuDropdown: () => <div data-testid="user-menu">Avatar</div>,
}));

// Render dropdown parts inline so overflow items are assertable without opening a portal.
vi.mock('@/components/ui/navigation/dropdown-menu', () => ({
  DropdownMenu: ({ children }: any) => <div>{children}</div>,
  DropdownMenuTrigger: ({ children }: any) => <div>{children}</div>,
  DropdownMenuContent: ({ children }: any) => <div>{children}</div>,
  DropdownMenuItem: ({ children }: any) => <div>{children}</div>,
}));

import { AppTopBar } from '@/components/layout/AppNav/AppTopBar';

describe('AppTopBar', () => {
  it('renders the logo linking to /dashboard', () => {
    render(<AppTopBar />);
    const logo = screen.getByRole('link', { name: /meepleai/i });
    expect(logo.getAttribute('href')).toBe('/dashboard');
  });

  it('renders the sp4 primary top-bar links', () => {
    render(<AppTopBar />);
    expect(screen.getByText('Dashboard')).toBeDefined();
    expect(screen.getByText('Libreria')).toBeDefined();
    expect(screen.getByText('Hub')).toBeDefined();
    expect(screen.getByText('Sessioni')).toBeDefined();
    expect(screen.getByText('Toolkit')).toBeDefined();
  });

  it('marks the active link with aria-current="page"', () => {
    render(<AppTopBar />);
    expect(screen.getByText('Dashboard').closest('a')?.getAttribute('aria-current')).toBe('page');
    expect(screen.getByText('Libreria').closest('a')?.getAttribute('aria-current')).toBeNull();
  });

  it('renders the overflow "Altro" menu with secondary destinations', () => {
    render(<AppTopBar />);
    expect(screen.getByTestId('app-top-bar-overflow')).toBeDefined();
    expect(screen.getByText('Giocatori')).toBeDefined();
  });

  it('renders the user pill', () => {
    render(<AppTopBar />);
    expect(screen.getByTestId('user-menu')).toBeDefined();
  });

  it('hides primary links and shows the Admin badge in adminMode', () => {
    render(<AppTopBar adminMode />);
    expect(screen.getByText('Admin')).toBeDefined();
    expect(screen.queryByText('Dashboard')).toBeNull();
    expect(screen.queryByTestId('app-top-bar-overflow')).toBeNull();
  });

  it('renders an admin menu button that calls onMenuClick', () => {
    const onMenuClick = vi.fn();
    render(<AppTopBar adminMode onMenuClick={onMenuClick} />);
    const btn = screen.getByRole('button', { name: /menu/i });
    fireEvent.click(btn);
    expect(onMenuClick).toHaveBeenCalledTimes(1);
  });
});
