/**
 * Unit tests for UnifiedHeader component
 * Issue #3104 - Unify header navigation
 * Updated: Issue #2860 - Responsive navigation with purple active, orange hover
 *
 * Coverage:
 * - Rendering (navigation items, logo, user menu)
 * - Active state logic (path matching)
 * - Admin visibility (admin-only items)
 * - Accessibility (ARIA labels, keyboard nav)
 * - Responsive behavior (mobile/desktop)
 * - User authentication states
 * - Color states (purple active, orange hover)
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { usePathname, useRouter } from 'next/navigation';
import { vi, Mock } from 'vitest';

import { useCurrentUser } from '@/hooks/queries/useCurrentUser';

import { UnifiedHeader } from '../UnifiedHeader';

// Mock Next.js navigation
vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
  useRouter: vi.fn(() => ({
    push: vi.fn(),
  })),
}));

// Mock useCurrentUser hook
vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: vi.fn(),
}));

// Mock NotificationBell component
vi.mock('@/components/notifications', () => ({
  NotificationBell: () => <div data-testid="notification-bell">Bell</div>,
}));

// Mock logoutAction
vi.mock('@/actions/auth', () => ({
  logoutAction: vi.fn(() => Promise.resolve({ success: true })),
}));

const mockUsePathname = usePathname as Mock;
const mockUseRouter = useRouter as Mock;
const mockUseCurrentUser = useCurrentUser as Mock;

describe('UnifiedHeader', () => {
  const mockPush = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    mockUsePathname.mockReturnValue('/');
    mockUseRouter.mockReturnValue({ push: mockPush });
    // Default to authenticated user for most tests
    mockUseCurrentUser.mockReturnValue({
      data: {
        id: 'test-user-id',
        email: 'test@example.com',
        username: 'testuser',
      },
      isLoading: false,
    });
  });

  describe('Rendering', () => {
    it('should render the header element', () => {
      render(<UnifiedHeader />);
      expect(screen.getByTestId('unified-header')).toBeInTheDocument();
    });

    it('should render the logo', () => {
      render(<UnifiedHeader />);
      expect(screen.getByLabelText('MeepleAI Home')).toBeInTheDocument();
    });

    it('should render NotificationBell', () => {
      render(<UnifiedHeader />);
      expect(screen.getByTestId('notification-bell')).toBeInTheDocument();
    });

    it('should render navigation links on desktop', () => {
      render(<UnifiedHeader />);

      // Issue #2860: Nav order is Dashboard, Library, Catalog, Chat, Profile
      expect(screen.getByLabelText('Navigate to dashboard')).toBeInTheDocument();
      expect(screen.getByLabelText('Navigate to your game library')).toBeInTheDocument();
      expect(screen.getByLabelText('Navigate to games catalog')).toBeInTheDocument();
      expect(screen.getByLabelText('Navigate to chat interface')).toBeInTheDocument();
      expect(screen.getByLabelText('Navigate to your profile')).toBeInTheDocument();
    });

    it('should render login button when not authenticated', () => {
      mockUseCurrentUser.mockReturnValue({
        data: null,
        isLoading: false,
      });
      render(<UnifiedHeader />);
      expect(screen.getByText('Accedi')).toBeInTheDocument();
    });
  });

  describe('Navigation Items', () => {
    it('should render correct href attributes', () => {
      render(<UnifiedHeader />);

      // Issue #2860: Updated nav order - Dashboard, Library, Catalog, Chat, Profile
      expect(screen.getByLabelText('Navigate to dashboard')).toHaveAttribute('href', '/dashboard');
      expect(screen.getByLabelText('Navigate to your game library')).toHaveAttribute('href', '/library');
      expect(screen.getByLabelText('Navigate to games catalog')).toHaveAttribute('href', '/games');
      expect(screen.getByLabelText('Navigate to chat interface')).toHaveAttribute('href', '/chat');
      expect(screen.getByLabelText('Navigate to your profile')).toHaveAttribute('href', '/profile');
    });

    it('should render correct labels', () => {
      render(<UnifiedHeader />);

      // Issue #2860: Updated labels - Settings moved to dropdown
      expect(screen.getByText('Dashboard')).toBeInTheDocument();
      expect(screen.getByText('I Miei Giochi')).toBeInTheDocument();
      expect(screen.getByText('Catalogo')).toBeInTheDocument();
      expect(screen.getByText('Chat')).toBeInTheDocument();
      expect(screen.getByText('Profilo')).toBeInTheDocument();
    });
  });

  describe('Active State Logic', () => {
    it('should mark dashboard as active for /dashboard route', () => {
      mockUsePathname.mockReturnValue('/dashboard');
      render(<UnifiedHeader />);

      const dashboardLink = screen.getByLabelText('Navigate to dashboard');
      expect(dashboardLink).toHaveAttribute('aria-current', 'page');
      // Purple active state (Issue #2860)
      expect(dashboardLink).toHaveClass('text-[hsl(262_83%_62%)]');
    });

    it('should mark games as active for /games routes', () => {
      mockUsePathname.mockReturnValue('/games/catan');
      render(<UnifiedHeader />);

      const gamesLink = screen.getByLabelText('Navigate to games catalog');
      expect(gamesLink).toHaveAttribute('aria-current', 'page');
    });

    it('should mark library as active for /library routes', () => {
      mockUsePathname.mockReturnValue('/library');
      render(<UnifiedHeader />);

      const libraryLink = screen.getByLabelText('Navigate to your game library');
      expect(libraryLink).toHaveAttribute('aria-current', 'page');
    });

    it('should mark chat as active for /chat routes', () => {
      mockUsePathname.mockReturnValue('/chat/thread-123');
      render(<UnifiedHeader />);

      const chatLink = screen.getByLabelText('Navigate to chat interface');
      expect(chatLink).toHaveAttribute('aria-current', 'page');
    });

    it('should mark profile as active for /profile routes', () => {
      mockUsePathname.mockReturnValue('/profile');
      render(<UnifiedHeader />);

      const profileLink = screen.getByLabelText('Navigate to your profile');
      expect(profileLink).toHaveAttribute('aria-current', 'page');
      // Purple active state (Issue #2860)
      expect(profileLink).toHaveClass('text-[hsl(262_83%_62%)]');
    });

    it('should not mark multiple items as active', () => {
      mockUsePathname.mockReturnValue('/chat');
      render(<UnifiedHeader />);

      const navLinks = screen.getAllByRole('link').filter(
        link => link.getAttribute('aria-label')?.includes('Navigate to')
      );
      const activeLinks = navLinks.filter(link => link.getAttribute('aria-current') === 'page');
      expect(activeLinks).toHaveLength(1);
    });

    it('should apply orange hover color to inactive links', () => {
      mockUsePathname.mockReturnValue('/dashboard');
      render(<UnifiedHeader />);

      const chatLink = screen.getByLabelText('Navigate to chat interface');
      // Orange hover state (Issue #2860)
      expect(chatLink).toHaveClass('hover:text-primary');
    });
  });

  describe('Admin Visibility', () => {
    it('should NOT show admin link for regular users', () => {
      mockUseCurrentUser.mockReturnValue({
        data: { id: '1', email: 'user@test.com', role: 'User' },
        isLoading: false,
      });
      render(<UnifiedHeader />);

      expect(screen.queryByLabelText('Navigate to admin dashboard')).not.toBeInTheDocument();
    });

    it('should show admin link for admin users in navigation', () => {
      mockUseCurrentUser.mockReturnValue({
        data: { id: '1', email: 'admin@test.com', role: 'Admin' },
        isLoading: false,
      });
      render(<UnifiedHeader />);

      expect(screen.getByLabelText('Navigate to admin dashboard')).toBeInTheDocument();
    });

    it('should show admin link in dropdown for admin users', async () => {
      mockUseCurrentUser.mockReturnValue({
        data: { id: '1', email: 'admin@test.com', displayName: 'Admin User', role: 'Admin' },
        isLoading: false,
      });

      const user = userEvent.setup();
      render(<UnifiedHeader />);

      // Open user menu
      const userMenuTrigger = screen.getByTestId('user-menu-trigger');
      await user.click(userMenuTrigger);

      // Check admin link in dropdown
      await waitFor(() => {
        expect(screen.getByTestId('admin-panel-menu-item')).toBeInTheDocument();
      });
    });

    it('should NOT show admin link in dropdown for regular users', async () => {
      mockUseCurrentUser.mockReturnValue({
        data: { id: '1', email: 'user@test.com', displayName: 'Regular User', role: 'User' },
        isLoading: false,
      });

      const user = userEvent.setup();
      render(<UnifiedHeader />);

      // Open user menu
      const userMenuTrigger = screen.getByTestId('user-menu-trigger');
      await user.click(userMenuTrigger);

      // Check admin link is NOT in dropdown
      await waitFor(() => {
        expect(screen.queryByTestId('admin-panel-menu-item')).not.toBeInTheDocument();
      });
    });

    it('should handle case-insensitive admin role check', () => {
      mockUseCurrentUser.mockReturnValue({
        data: { id: '1', email: 'admin@test.com', role: 'admin' },
        isLoading: false,
      });
      render(<UnifiedHeader />);

      expect(screen.getByLabelText('Navigate to admin dashboard')).toBeInTheDocument();
    });
  });

  describe('User Menu', () => {
    it('should display user email in dropdown', async () => {
      mockUseCurrentUser.mockReturnValue({
        data: { id: '1', email: 'test@example.com', displayName: 'Test User', role: 'User' },
        isLoading: false,
      });

      const user = userEvent.setup();
      render(<UnifiedHeader />);

      await user.click(screen.getByTestId('user-menu-trigger'));

      await waitFor(() => {
        expect(screen.getByText('test@example.com')).toBeInTheDocument();
      });
    });

    it('should display user initial when no avatar', () => {
      mockUseCurrentUser.mockReturnValue({
        data: { id: '1', email: 'test@example.com', displayName: 'Test User', role: 'User' },
        isLoading: false,
      });
      render(<UnifiedHeader />);

      expect(screen.getByText('T')).toBeInTheDocument(); // First letter of "Test User"
    });

    it('should have settings link in dropdown', async () => {
      mockUseCurrentUser.mockReturnValue({
        data: { id: '1', email: 'test@example.com', role: 'User' },
        isLoading: false,
      });

      const user = userEvent.setup();
      render(<UnifiedHeader />);

      await user.click(screen.getByTestId('user-menu-trigger'));

      await waitFor(() => {
        expect(screen.getByTestId('settings-menu-item')).toBeInTheDocument();
      });
    });

    it('should have logout button in dropdown', async () => {
      mockUseCurrentUser.mockReturnValue({
        data: { id: '1', email: 'test@example.com', role: 'User' },
        isLoading: false,
      });

      const user = userEvent.setup();
      render(<UnifiedHeader />);

      await user.click(screen.getByTestId('user-menu-trigger'));

      await waitFor(() => {
        expect(screen.getByTestId('logout-menu-item')).toBeInTheDocument();
      });
    });
  });

  describe('Mobile Layout', () => {
    it('should render settings button for authenticated users', () => {
      render(<UnifiedHeader />);

      // Issue #2860: Settings button is part of the header actions
      const settingsButton = screen.getByLabelText('Navigate to settings');
      expect(settingsButton).toBeInTheDocument();
      expect(settingsButton).toHaveAttribute('href', '/settings');
    });

    it('should hide desktop nav on mobile (md:flex)', () => {
      const { container } = render(<UnifiedHeader />);

      const desktopNav = container.querySelector('nav[aria-label="Main navigation"]');
      expect(desktopNav).toHaveClass('hidden', 'md:flex');
    });
  });

  describe('Accessibility', () => {
    it('should have navigation landmark', () => {
      render(<UnifiedHeader />);
      expect(screen.getByRole('navigation', { name: /main navigation/i })).toBeInTheDocument();
    });

    it('should have keyboard focus indicators with purple ring', () => {
      const { container } = render(<UnifiedHeader />);

      // Check desktop navigation links (inside nav element) which have focus indicators
      const desktopNav = container.querySelector('nav[aria-label="Main navigation"]');
      const desktopLinks = desktopNav?.querySelectorAll('a[aria-label^="Navigate to"]') || [];

      // At least some desktop links should have focus ring class
      // Purple focus ring (Issue #2860)
      const linksWithFocusRing = Array.from(desktopLinks).filter(link =>
        link.classList.contains('focus-visible:ring-2') &&
        link.classList.contains('focus-visible:ring-[hsl(262_83%_62%)]')
      );
      expect(linksWithFocusRing.length).toBeGreaterThan(0);
    });

    it('should mark icons as aria-hidden', () => {
      const { container } = render(<UnifiedHeader />);

      const icons = container.querySelectorAll('nav svg');
      icons.forEach(icon => {
        expect(icon).toHaveAttribute('aria-hidden', 'true');
      });
    });
  });

  describe('Styling', () => {
    it('should be sticky at top', () => {
      render(<UnifiedHeader />);
      const header = screen.getByTestId('unified-header');
      expect(header).toHaveClass('sticky', 'top-0', 'z-50');
    });

    it('should have glass morphism styles', () => {
      render(<UnifiedHeader />);
      const header = screen.getByTestId('unified-header');
      expect(header).toHaveClass('bg-background/95', 'backdrop-blur-[16px]');
    });

    it('should have border-bottom', () => {
      render(<UnifiedHeader />);
      const header = screen.getByTestId('unified-header');
      expect(header).toHaveClass('border-b');
    });
  });
});
