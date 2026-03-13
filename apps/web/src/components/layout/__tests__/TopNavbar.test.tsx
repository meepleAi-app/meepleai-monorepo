/**
 * Unit tests for TopNavbar component
 *
 * Coverage:
 * - Rendering (logo, notification bell, user avatar)
 * - UserMenu opens on click
 * - Escape key closes the menu
 * - Menu items rendered with correct role="menuitem"
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';

import { TopNavbar } from '../TopNavbar/TopNavbar';

// Mock Next.js navigation
vi.mock('next/navigation', () => ({
  usePathname: vi.fn(() => '/'),
  useRouter: vi.fn(() => ({ push: vi.fn() })),
  useSearchParams: vi.fn(() => ({
    get: vi.fn().mockReturnValue(null),
    toString: vi.fn().mockReturnValue(''),
  })),
}));

// Mock useAuthUser
vi.mock('@/hooks/useAuthUser', () => ({
  useAuthUser: vi.fn(() => ({
    user: { displayName: 'Mario Rossi', email: 'mario@test.com', role: 'admin' },
    loading: false,
    error: null,
  })),
}));

// Mock useScrollState
vi.mock('@/hooks/useScrollState', () => ({
  useScrollState: vi.fn(() => ({ isScrolled: false })),
}));

// Mock NotificationBell
vi.mock('@/components/notifications', () => ({
  NotificationBell: () => <div data-testid="notification-bell">Bell</div>,
}));

// Mock logoutAction
vi.mock('@/actions/auth', () => ({
  logoutAction: vi.fn(() => Promise.resolve({ success: true })),
}));

// Mock ThemeToggle
vi.mock('@/components/ui/navigation/ThemeToggle', () => ({
  ThemeToggle: () => <div data-testid="theme-toggle">Theme</div>,
}));

// Mock MobileNavDrawer
vi.mock('../MobileNavDrawer', () => ({
  MobileNavDrawer: () => <div data-testid="mobile-nav-drawer">Drawer</div>,
}));

// Mock Logo
vi.mock('../TopNavbar/Logo', () => ({
  Logo: () => <div data-testid="logo">Logo</div>,
}));

describe('TopNavbar', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the navbar with logo, notification bell, and user menu trigger', () => {
    render(<TopNavbar />);

    expect(screen.getByTestId('top-navbar')).toBeInTheDocument();
    expect(screen.getByTestId('logo')).toBeInTheDocument();
    expect(screen.getByTestId('notification-bell')).toBeInTheDocument();
    expect(screen.getByLabelText('User menu')).toBeInTheDocument();
  });

  it('displays user initials in the avatar', () => {
    render(<TopNavbar />);

    expect(screen.getByText('MR')).toBeInTheDocument();
  });

  it('opens the menu on click', async () => {
    const user = userEvent.setup();
    render(<TopNavbar />);

    const trigger = screen.getByLabelText('User menu');
    await user.click(trigger);

    await waitFor(() => {
      expect(screen.getByText('Mario Rossi')).toBeInTheDocument();
      expect(screen.getByText('admin')).toBeInTheDocument();
    });
  });

  it('renders menu items with role="menuitem"', async () => {
    const user = userEvent.setup();
    render(<TopNavbar />);

    await user.click(screen.getByLabelText('User menu'));

    await waitFor(() => {
      const menuItems = screen.getAllByRole('menuitem');
      expect(menuItems.length).toBeGreaterThanOrEqual(3);

      // Check specific menu items exist
      expect(screen.getByText('Il mio profilo')).toBeInTheDocument();
      expect(screen.getByText('Impostazioni')).toBeInTheDocument();
      expect(screen.getByText('Esci')).toBeInTheDocument();
    });
  });

  it('closes the menu when Escape is pressed', async () => {
    const user = userEvent.setup();
    render(<TopNavbar />);

    // Open menu
    await user.click(screen.getByLabelText('User menu'));
    await waitFor(() => {
      expect(screen.getByText('Il mio profilo')).toBeInTheDocument();
    });

    // Press Escape
    await user.keyboard('{Escape}');

    await waitFor(() => {
      expect(screen.queryByText('Il mio profilo')).not.toBeInTheDocument();
    });
  });

  it('includes ThemeToggle inside the menu', async () => {
    const user = userEvent.setup();
    render(<TopNavbar />);

    await user.click(screen.getByLabelText('User menu'));

    await waitFor(() => {
      expect(screen.getByTestId('theme-toggle')).toBeInTheDocument();
    });
  });

  it('renders profile link with correct href', async () => {
    const user = userEvent.setup();
    render(<TopNavbar />);

    await user.click(screen.getByLabelText('User menu'));

    await waitFor(() => {
      const profileLink = screen.getByText('Il mio profilo').closest('a');
      expect(profileLink).toHaveAttribute('href', '/profile');
    });
  });

  it('renders settings link with correct href', async () => {
    const user = userEvent.setup();
    render(<TopNavbar />);

    await user.click(screen.getByLabelText('User menu'));

    await waitFor(() => {
      const settingsLink = screen.getByText('Impostazioni').closest('a');
      expect(settingsLink).toHaveAttribute('href', '/profile?tab=settings');
    });
  });
});
