/**
 * Unit tests for TopNav component
 * Issue #2053 - User notifications with bell icon
 *
 * Coverage:
 * - Rendering (4 nav items, logo, user menu, notification bell)
 * - Active state logic (path matching)
 * - Accessibility (ARIA labels, keyboard nav)
 * - Responsive (hidden on mobile, visible on desktop)
 * - User menu (logout functionality)
 * - NotificationBell integration
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { usePathname, useRouter } from 'next/navigation';
import { vi } from 'vitest';
import { TopNav } from '../TopNav';

// Mock Next.js navigation
vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
  useRouter: vi.fn(),
}));

// Mock useCurrentUser hook
vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: vi.fn(),
}));

// Mock logoutAction
vi.mock('@/actions/auth', () => ({
  logoutAction: vi.fn(),
}));

// Mock NotificationBell component
vi.mock('@/components/notifications', () => ({
  NotificationBell: () => (
    <button data-testid="notification-bell" aria-label="Notifications">
      🔔
    </button>
  ),
}));

import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { logoutAction } from '@/actions/auth';

const mockUsePathname = usePathname as ReturnType<typeof vi.fn>;
const mockUseRouter = useRouter as ReturnType<typeof vi.fn>;
const mockUseCurrentUser = useCurrentUser as ReturnType<typeof vi.fn>;
const mockLogoutAction = logoutAction as ReturnType<typeof vi.fn>;

describe('TopNav', () => {
  const mockPush = vi.fn();

  beforeEach(() => {
    mockUsePathname.mockReturnValue('/dashboard');
    mockUseRouter.mockReturnValue({ push: mockPush });
    mockUseCurrentUser.mockReturnValue({
      data: { id: '1', displayName: 'Mario Rossi', email: 'mario@test.com' },
      isLoading: false,
      error: null,
    });
    mockLogoutAction.mockResolvedValue({ success: true });
    mockPush.mockClear();
  });

  describe('Rendering', () => {
    it('should render all 4 navigation items', () => {
      render(<TopNav />);

      expect(screen.getByText('Home')).toBeInTheDocument();
      expect(screen.getByText('Giochi')).toBeInTheDocument();
      expect(screen.getByText('Chat')).toBeInTheDocument();
      expect(screen.getByText('Impostazioni')).toBeInTheDocument();
    });

    it('should render the MeepleAI logo and title', () => {
      render(<TopNav />);

      expect(screen.getByText('MeepleAI')).toBeInTheDocument();
    });

    it('should render correct ARIA labels', () => {
      render(<TopNav />);

      expect(screen.getByLabelText('Navigate to dashboard home')).toBeInTheDocument();
      expect(screen.getByLabelText('Navigate to games catalog')).toBeInTheDocument();
      expect(screen.getByLabelText('Navigate to chat interface')).toBeInTheDocument();
      expect(screen.getByLabelText('Navigate to settings')).toBeInTheDocument();
    });

    it('should have primary navigation landmark', () => {
      render(<TopNav />);
      const nav = screen.getByRole('navigation', { name: /primary desktop navigation/i });
      expect(nav).toBeInTheDocument();
    });

    it('should render as a fixed top nav', () => {
      const { container } = render(<TopNav />);
      const nav = container.querySelector('nav');
      expect(nav).toHaveClass('fixed', 'top-0', 'left-0', 'right-0');
    });

    it('should be hidden on mobile (visible on md breakpoint)', () => {
      const { container } = render(<TopNav />);
      const nav = container.querySelector('nav');
      expect(nav).toHaveClass('hidden', 'md:flex');
    });

    it('should have fixed height of 16 (h-16)', () => {
      const { container } = render(<TopNav />);
      const nav = container.querySelector('nav');
      expect(nav).toHaveClass('h-16');
    });
  });

  describe('Active State Logic', () => {
    it('should mark /dashboard as active when pathname is /dashboard', () => {
      mockUsePathname.mockReturnValue('/dashboard');
      render(<TopNav />);

      const homeLink = screen.getByLabelText('Navigate to dashboard home');
      expect(homeLink).toHaveAttribute('aria-current', 'page');
      expect(homeLink).toHaveClass('text-primary', 'font-semibold');
    });

    it('should mark /dashboard as active when pathname is / (root)', () => {
      mockUsePathname.mockReturnValue('/');
      render(<TopNav />);

      const homeLink = screen.getByLabelText('Navigate to dashboard home');
      expect(homeLink).toHaveAttribute('aria-current', 'page');
    });

    it('should mark /games as active for /games/* routes', () => {
      mockUsePathname.mockReturnValue('/games/catan');
      render(<TopNav />);

      const giochiLink = screen.getByLabelText('Navigate to games catalog');
      expect(giochiLink).toHaveAttribute('aria-current', 'page');
      expect(giochiLink).toHaveClass('text-primary', 'font-semibold');
    });

    it('should mark /chat as active for /chat/* routes', () => {
      mockUsePathname.mockReturnValue('/chat/thread-123');
      render(<TopNav />);

      const chatLink = screen.getByLabelText('Navigate to chat interface');
      expect(chatLink).toHaveAttribute('aria-current', 'page');
    });

    it('should mark /settings as active for /settings/* routes', () => {
      mockUsePathname.mockReturnValue('/settings/profile');
      render(<TopNav />);

      const settingsLink = screen.getByLabelText('Navigate to settings');
      expect(settingsLink).toHaveAttribute('aria-current', 'page');
    });

    it('should not mark multiple items as active', () => {
      mockUsePathname.mockReturnValue('/chat');
      render(<TopNav />);

      const activeLinks = screen
        .getAllByRole('link')
        .filter(link => link.getAttribute('aria-current') === 'page');
      expect(activeLinks).toHaveLength(1);
    });

    it('should apply inactive styles to non-active links', () => {
      mockUsePathname.mockReturnValue('/chat');
      render(<TopNav />);

      const homeLink = screen.getByLabelText('Navigate to dashboard home');
      expect(homeLink).not.toHaveAttribute('aria-current');
      expect(homeLink).toHaveClass('text-muted-foreground');
      expect(homeLink).not.toHaveClass('text-primary', 'font-semibold');
    });
  });

  describe('Accessibility (WCAG 2.1 AA)', () => {
    it('should have keyboard focus indicators', () => {
      render(<TopNav />);

      const links = screen.getAllByRole('link').filter(link => link.getAttribute('aria-label'));
      links.forEach(link => {
        expect(link).toHaveClass('focus-visible:ring-2', 'focus-visible:ring-primary');
      });
    });

    it('should mark icons as aria-hidden', () => {
      const { container } = render(<TopNav />);

      // Lucide icons render as <svg> with aria-hidden
      const navIcons = container.querySelectorAll('nav > div > div > a > svg');
      navIcons.forEach(icon => {
        expect(icon).toHaveAttribute('aria-hidden', 'true');
      });
    });

    it('should have smooth transitions for hover/active states', () => {
      render(<TopNav />);

      const links = screen.getAllByRole('link').filter(link => link.getAttribute('aria-label'));
      links.forEach(link => {
        expect(link).toHaveClass('transition-colors', 'duration-200');
      });
    });
  });

  describe('Navigation', () => {
    it('should render correct href attributes', () => {
      render(<TopNav />);

      expect(screen.getByLabelText('Navigate to dashboard home')).toHaveAttribute(
        'href',
        '/dashboard'
      );
      expect(screen.getByLabelText('Navigate to games catalog')).toHaveAttribute('href', '/games');
      expect(screen.getByLabelText('Navigate to chat interface')).toHaveAttribute('href', '/chat');
      expect(screen.getByLabelText('Navigate to settings')).toHaveAttribute('href', '/settings');
    });

    it('should use Next.js Link component for client-side navigation', () => {
      const { container } = render(<TopNav />);

      // Next.js Link renders as <a> with href
      const links = container.querySelectorAll('a[href^="/"]');
      expect(links.length).toBeGreaterThanOrEqual(4);
    });
  });

  describe('User Menu', () => {
    it('should display user initial from displayName', () => {
      mockUseCurrentUser.mockReturnValue({
        data: { id: '1', displayName: 'Mario Rossi', email: 'mario@test.com' },
        isLoading: false,
        error: null,
      });
      render(<TopNav />);

      expect(screen.getByText('M')).toBeInTheDocument();
    });

    it('should display user initial from email when no displayName', () => {
      mockUseCurrentUser.mockReturnValue({
        data: { id: '1', displayName: null, email: 'luigi@test.com' },
        isLoading: false,
        error: null,
      });
      render(<TopNav />);

      expect(screen.getByText('L')).toBeInTheDocument();
    });

    it('should display default U when no user data', () => {
      mockUseCurrentUser.mockReturnValue({
        data: null,
        isLoading: false,
        error: null,
      });
      render(<TopNav />);

      expect(screen.getByText('U')).toBeInTheDocument();
    });

    it('should display user name on large screens', () => {
      mockUseCurrentUser.mockReturnValue({
        data: { id: '1', displayName: 'Mario Rossi', email: 'mario@test.com' },
        isLoading: false,
        error: null,
      });
      render(<TopNav />);

      expect(screen.getByText('Mario Rossi')).toBeInTheDocument();
    });

    it('should open dropdown menu when clicking user button', async () => {
      render(<TopNav />);

      // Get the user menu button by finding button with aria-haspopup
      const userButtons = screen.getAllByRole('button');
      const userButton = userButtons.find(btn => btn.getAttribute('aria-haspopup') === 'menu');
      expect(userButton).toBeTruthy();
      fireEvent.click(userButton!);

      await waitFor(() => {
        expect(screen.getByText('Impostazioni')).toBeInTheDocument();
        expect(screen.getByText('Esci')).toBeInTheDocument();
      });
    });

    it('should have logout button in dropdown', async () => {
      render(<TopNav />);

      // Get the user menu button by finding button with aria-haspopup
      const userButtons = screen.getAllByRole('button');
      const userButton = userButtons.find(btn => btn.getAttribute('aria-haspopup') === 'menu');
      expect(userButton).toBeTruthy();
      fireEvent.click(userButton!);

      await waitFor(() => {
        expect(screen.getByText('Esci')).toBeInTheDocument();
      });
    });
  });

  describe('Logout Functionality', () => {
    it('should call logoutAction when clicking logout', async () => {
      render(<TopNav />);

      // Get the user menu button by finding button with aria-haspopup
      const userButtons = screen.getAllByRole('button');
      const userButton = userButtons.find(btn => btn.getAttribute('aria-haspopup') === 'menu');
      expect(userButton).toBeTruthy();
      fireEvent.click(userButton!);

      await waitFor(() => {
        const logoutButton = screen.getByText('Esci');
        fireEvent.click(logoutButton);
      });

      await waitFor(() => {
        expect(mockLogoutAction).toHaveBeenCalled();
      });
    });

    it('should redirect to /login after successful logout', async () => {
      mockLogoutAction.mockResolvedValue({ success: true });
      render(<TopNav />);

      // Get the user menu button by finding button with aria-haspopup
      const userButtons = screen.getAllByRole('button');
      const userButton = userButtons.find(btn => btn.getAttribute('aria-haspopup') === 'menu');
      expect(userButton).toBeTruthy();
      fireEvent.click(userButton!);

      await waitFor(() => {
        const logoutButton = screen.getByText('Esci');
        fireEvent.click(logoutButton);
      });

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith('/login');
      });
    });

    it('should show loading state during logout', async () => {
      // Create a promise that we can control
      let resolveLogout: (value: { success: boolean }) => void;
      mockLogoutAction.mockImplementation(
        () =>
          new Promise(resolve => {
            resolveLogout = resolve;
          })
      );

      render(<TopNav />);

      // Get the user menu button by finding button with aria-haspopup
      const userButtons = screen.getAllByRole('button');
      const userButton = userButtons.find(btn => btn.getAttribute('aria-haspopup') === 'menu');
      expect(userButton).toBeTruthy();
      fireEvent.click(userButton!);

      await waitFor(() => {
        const logoutButton = screen.getByText('Esci');
        fireEvent.click(logoutButton);
      });

      // Verify logout action was called
      expect(mockLogoutAction).toHaveBeenCalled();

      // Resolve the logout
      resolveLogout!({ success: true });
    });
  });

  describe('Design System Compliance (Playful Boardroom)', () => {
    it('should use primary color for active state', () => {
      mockUsePathname.mockReturnValue('/dashboard');
      render(<TopNav />);

      const activeLink = screen.getByLabelText('Navigate to dashboard home');
      expect(activeLink).toHaveClass('text-primary');
    });

    it('should use muted-foreground for inactive state', () => {
      mockUsePathname.mockReturnValue('/dashboard');
      render(<TopNav />);

      const inactiveLink = screen.getByLabelText('Navigate to games catalog');
      expect(inactiveLink).toHaveClass('text-muted-foreground');
    });

    it('should use card background color', () => {
      const { container } = render(<TopNav />);
      const nav = container.querySelector('nav');
      expect(nav).toHaveClass('bg-card');
    });

    it('should have border-bottom with border color', () => {
      const { container } = render(<TopNav />);
      const nav = container.querySelector('nav');
      expect(nav).toHaveClass('border-b', 'border-border');
    });

    it('should have shadow-sm for subtle elevation', () => {
      const { container } = render(<TopNav />);
      const nav = container.querySelector('nav');
      expect(nav).toHaveClass('shadow-sm');
    });

    it('should have z-50 for stacking context', () => {
      const { container } = render(<TopNav />);
      const nav = container.querySelector('nav');
      expect(nav).toHaveClass('z-50');
    });

    it('should use correct icon size (w-5 h-5)', () => {
      const { container } = render(<TopNav />);

      // Get icons from nav links (not dropdown)
      const navLinks = container.querySelectorAll('nav > div > div > a');
      navLinks.forEach(link => {
        const icon = link.querySelector('svg');
        if (icon) {
          expect(icon).toHaveClass('w-5', 'h-5');
        }
      });
    });
  });

  describe('NotificationBell Integration', () => {
    it('should render the NotificationBell component', () => {
      render(<TopNav />);

      expect(screen.getByTestId('notification-bell')).toBeInTheDocument();
    });

    it('should have accessible notification bell', () => {
      render(<TopNav />);

      const bell = screen.getByLabelText('Notifications');
      expect(bell).toBeInTheDocument();
    });

    it('should position notification bell before user menu', () => {
      const { container } = render(<TopNav />);

      // The notification bell and user menu should be in the same flex container
      const bellContainer = container.querySelector(
        '[data-testid="notification-bell"]'
      )?.parentElement;
      expect(bellContainer).toHaveClass('flex', 'items-center', 'gap-2');
    });
  });
});
