import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { TopBar } from '../TopBar';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
}));

vi.mock('@/components/layout/Breadcrumb/DesktopBreadcrumb', () => ({
  DesktopBreadcrumb: () => <nav aria-label="Percorso di navigazione">breadcrumb</nav>,
}));

vi.mock('@/components/notifications', () => ({
  NotificationBell: () => <button aria-label="Notifications">bell</button>,
}));

vi.mock('@/hooks/useAuthUser', () => ({
  useAuthUser: () => ({ user: { displayName: 'Test User', role: 'user' } }),
}));

vi.mock('@/hooks/useScrollState', () => ({
  useScrollState: () => ({ isScrolled: false }),
}));

vi.mock('@/hooks/useCommandPalette', () => ({
  useCommandPalette: () => ({ toggle: vi.fn() }),
}));

vi.mock('@/actions/auth', () => ({
  logoutAction: vi.fn(),
}));

vi.mock('@/components/layout/MobileNavDrawer', () => ({
  MobileNavDrawer: () => null,
}));

vi.mock('@/components/layout/TopNavbar/Logo', () => ({
  Logo: () => <span>Logo</span>,
}));

vi.mock('@/components/ui/navigation/ThemeToggle', () => ({
  ThemeToggle: () => null,
}));

describe('TopBar', () => {
  it('renders as a header element with data-testid', () => {
    render(<TopBar />);
    const header = screen.getByTestId('top-bar');
    expect(header.tagName).toBe('HEADER');
  });

  it('contains breadcrumb navigation', () => {
    render(<TopBar />);
    expect(screen.getByRole('navigation', { name: /percorso/i })).toBeInTheDocument();
  });

  it('contains search trigger with "Cerca..." text', () => {
    render(<TopBar />);
    expect(screen.getByText('Cerca...')).toBeInTheDocument();
  });

  it('contains notifications button', () => {
    render(<TopBar />);
    expect(screen.getByRole('button', { name: /notifications/i })).toBeInTheDocument();
  });

  it('contains user menu button', () => {
    render(<TopBar />);
    expect(screen.getByRole('button', { name: /user menu/i })).toBeInTheDocument();
  });

  it('has 48px height (h-12 class)', () => {
    render(<TopBar />);
    const header = screen.getByTestId('top-bar');
    expect(header.className).toContain('h-12');
  });

  it('has sticky positioning', () => {
    render(<TopBar />);
    const header = screen.getByTestId('top-bar');
    expect(header.className).toContain('sticky');
    expect(header.className).toContain('top-0');
  });

  it('has backdrop blur for glass effect', () => {
    render(<TopBar />);
    const header = screen.getByTestId('top-bar');
    expect(header.className).toContain('backdrop-blur');
  });

  it('includes skip-to-content link for accessibility', () => {
    render(<TopBar />);
    const skipLink = screen.getByText('Vai al contenuto principale');
    expect(skipLink).toBeInTheDocument();
    expect(skipLink.getAttribute('href')).toBe('#main-content');
  });

  it('displays user initials in avatar', () => {
    render(<TopBar />);
    expect(screen.getByText('TU')).toBeInTheDocument();
  });

  it('accepts className prop', () => {
    render(<TopBar className="custom-class" />);
    const header = screen.getByTestId('top-bar');
    expect(header.className).toContain('custom-class');
  });
});
