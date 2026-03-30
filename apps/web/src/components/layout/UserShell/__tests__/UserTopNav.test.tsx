import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(() => '/dashboard'),
  useSearchParams: vi.fn(() => new URLSearchParams()),
  useRouter: vi.fn(() => ({ push: vi.fn(), replace: vi.fn(), back: vi.fn() })),
}));
vi.mock('@/hooks/useNavigation', () => ({
  useNavigation: vi.fn(() => ({
    sectionTitle: 'Dashboard',
    breadcrumbs: [],
    showBreadcrumb: false,
  })),
}));
vi.mock('@/components/dashboard/useDashboardMode', () => ({
  useDashboardMode: vi.fn(() => ({
    isGameMode: false,
    activeSheet: null,
    openSheet: vi.fn(),
    send: vi.fn(),
  })),
}));
vi.mock('@/lib/stores/sessionStore', () => ({
  useSessionStore: vi.fn(() => null),
}));
vi.mock('@/lib/stores/navbar-height-store', () => ({
  useNavbarHeightStore: vi.fn(selector => selector({ height: 52, setHeight: vi.fn() })),
}));
vi.mock('@/components/notifications', () => ({
  NotificationBell: () => <div data-testid="notification-bell" />,
}));
vi.mock('../../UserMenuDropdown', () => ({
  UserMenuDropdown: () => <div data-testid="user-menu" />,
}));
vi.mock('@/config/navigation-emoji', () => ({
  getSectionEmoji: vi.fn(() => '🏠'),
}));

import { usePathname } from 'next/navigation';
import { UserTopNav } from '../UserTopNav';

describe('UserTopNav', () => {
  it('renders section title', () => {
    render(<UserTopNav />);
    expect(screen.getByText('Dashboard')).toBeInTheDocument();
  });

  it('does not render search bar', () => {
    render(<UserTopNav />);
    expect(screen.queryByRole('button', { name: /cerca/i })).not.toBeInTheDocument();
    expect(screen.queryByPlaceholderText(/cerca/i)).not.toBeInTheDocument();
  });

  it('does not render session live badge', () => {
    render(<UserTopNav />);
    expect(screen.queryByText(/sessione live/i)).not.toBeInTheDocument();
  });

  it('renders ContextualCTA on /library', () => {
    vi.mocked(usePathname).mockReturnValue('/library');
    render(<UserTopNav />);
    expect(screen.getByTestId('contextual-cta')).toBeInTheDocument();
  });

  it('does not render ContextualCTA on /dashboard', () => {
    vi.mocked(usePathname).mockReturnValue('/dashboard');
    render(<UserTopNav />);
    expect(screen.queryByTestId('contextual-cta')).not.toBeInTheDocument();
  });

  it('renders NotificationBell', () => {
    render(<UserTopNav />);
    expect(screen.getByTestId('notification-bell')).toBeInTheDocument();
  });

  it('renders UserMenuDropdown', () => {
    render(<UserTopNav />);
    expect(screen.getByTestId('user-menu')).toBeInTheDocument();
  });
});
