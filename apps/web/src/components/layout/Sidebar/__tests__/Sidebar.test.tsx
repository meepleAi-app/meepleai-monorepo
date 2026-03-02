/**
 * Tests for Sidebar component - Issue #4936 (updated)
 *
 * Validates rendering, collapsed mode, navigation, user section, and accessibility.
 *
 * Changes from original (Issue #3479):
 * - Logo removed from sidebar (now in UniversalNavbar) → logo tests removed
 * - SidebarNav replaced by SidebarContextNav → context-aware test routes
 * - Default nav tested with non-contextual route (e.g. /sessions)
 * - Dashboard/Library/Games contexts tested with respective routes
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { usePathname, useRouter } from 'next/navigation';
import { describe, it, expect, vi, beforeEach, Mock } from 'vitest';

import { useCurrentUser } from '@/hooks/queries/useCurrentUser';

import { Sidebar } from '../Sidebar';

// Mock framer-motion (AnimatePresence + motion.div)
vi.mock('framer-motion', () => ({
  AnimatePresence: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  motion: {
    div: ({
      children,
      ...rest
    }: React.HTMLAttributes<HTMLDivElement> & { children?: React.ReactNode }) => (
      <div {...rest}>{children}</div>
    ),
  },
}));

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

// Mock NotificationBell
vi.mock('@/components/notifications', () => ({
  NotificationBell: () => <div data-testid="notification-bell">Bell</div>,
}));

// Mock logoutAction
vi.mock('@/actions/auth', () => ({
  logoutAction: vi.fn(() => Promise.resolve({ success: true })),
}));

const mockUsePathname = usePathname as Mock;
const mockUseCurrentUser = useCurrentUser as Mock;

describe('Sidebar', () => {
  const defaultProps = {
    isCollapsed: false,
    onToggle: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    // Default to a non-contextual route so SidebarNav (standard) renders
    mockUsePathname.mockReturnValue('/sessions');
    mockUseCurrentUser.mockReturnValue({
      data: { id: '1', email: 'test@test.com', role: 'User', displayName: 'Test User' },
      isLoading: false,
    });
  });

  describe('Rendering', () => {
    it('renders sidebar element', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByTestId('sidebar')).toBeInTheDocument();
    });

    it('renders toggle button', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByTestId('sidebar-toggle')).toBeInTheDocument();
    });

    it('renders user section', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByTestId('sidebar-user')).toBeInTheDocument();
    });

    it('renders standard navigation items on non-contextual routes (not profile)', () => {
      // /sessions → default context → SidebarNav renders
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByTestId('nav-dashboard')).toBeInTheDocument();
      expect(screen.getByTestId('nav-chat')).toBeInTheDocument();
      expect(screen.getByTestId('nav-catalog')).toBeInTheDocument();
      // Profile should NOT be in sidebar nav (hideFromMainNav)
      expect(screen.queryByTestId('nav-profile')).not.toBeInTheDocument();
    });

    it('renders Strumenti group with agents and sessions on non-contextual routes', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByText('Strumenti')).toBeInTheDocument();
      expect(screen.getByTestId('nav-agents')).toBeInTheDocument();
      expect(screen.getByTestId('nav-sessions')).toBeInTheDocument();
    });
  });

  describe('Context-Sensitive Navigation', () => {
    it('renders DashboardPanel when on /dashboard route', () => {
      mockUsePathname.mockReturnValue('/dashboard');
      render(<Sidebar {...defaultProps} />);
      // DashboardPanel shows context-specific links
      expect(screen.getByText('Overview')).toBeInTheDocument();
      expect(screen.getByText('Sessioni recenti')).toBeInTheDocument();
      expect(screen.getByText('La mia collezione')).toBeInTheDocument();
    });

    it('renders LibraryPanel when on /library route', () => {
      mockUsePathname.mockReturnValue('/library');
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByText('Tutti i giochi')).toBeInTheDocument();
      expect(screen.getByText('Preferiti')).toBeInTheDocument();
      expect(screen.getByText('Wishlist')).toBeInTheDocument();
    });

    it('renders GamesPanel when on /games route', () => {
      mockUsePathname.mockReturnValue('/games');
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByText('Tutti i giochi')).toBeInTheDocument();
      expect(screen.getByText('Top BGG')).toBeInTheDocument();
    });

    it('renders standard SidebarNav on non-contextual routes (/admin)', () => {
      mockUsePathname.mockReturnValue('/admin/overview');
      render(<Sidebar {...defaultProps} />);
      // Standard nav should show
      expect(screen.getByTestId('nav-dashboard')).toBeInTheDocument();
    });
  });

  describe('Expanded Mode', () => {
    it('has 220px width when expanded', () => {
      render(<Sidebar {...defaultProps} />);
      const sidebar = screen.getByTestId('sidebar');
      expect(sidebar).toHaveClass('w-[220px]');
    });

    it('shows text labels next to icons in standard nav', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByText('Dashboard')).toBeInTheDocument();
      expect(screen.getByText('Chat')).toBeInTheDocument();
      expect(screen.getByText('Scopri')).toBeInTheDocument();
    });

    it('shows Strumenti group label in standard nav', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByText('Strumenti')).toBeInTheDocument();
    });
  });

  describe('Collapsed Mode', () => {
    it('has 60px width when collapsed', () => {
      render(<Sidebar {...defaultProps} isCollapsed={true} />);
      const sidebar = screen.getByTestId('sidebar');
      expect(sidebar).toHaveClass('w-[60px]');
    });

    it('hides text labels when collapsed (standard nav)', () => {
      render(<Sidebar {...defaultProps} isCollapsed={true} />);
      expect(screen.queryByText('Dashboard')).not.toBeInTheDocument();
    });

    it('hides Strumenti group label when collapsed', () => {
      render(<Sidebar {...defaultProps} isCollapsed={true} />);
      expect(screen.queryByText('Strumenti')).not.toBeInTheDocument();
    });
  });

  describe('Toggle', () => {
    it('calls onToggle when toggle button clicked', async () => {
      const onToggle = vi.fn();
      const user = userEvent.setup();
      render(<Sidebar {...defaultProps} onToggle={onToggle} />);

      await user.click(screen.getByTestId('sidebar-toggle'));
      expect(onToggle).toHaveBeenCalledTimes(1);
    });

    it('has correct aria-label when expanded', () => {
      render(<Sidebar {...defaultProps} isCollapsed={false} />);
      expect(screen.getByTestId('sidebar-toggle')).toHaveAttribute(
        'aria-label',
        'Collapse sidebar'
      );
    });

    it('has correct aria-label when collapsed', () => {
      render(<Sidebar {...defaultProps} isCollapsed={true} />);
      expect(screen.getByTestId('sidebar-toggle')).toHaveAttribute('aria-label', 'Expand sidebar');
    });
  });

  describe('Library Context', () => {
    it('shows library sub-items when on /library route', () => {
      mockUsePathname.mockReturnValue('/library');
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByText('Tutti i giochi')).toBeInTheDocument();
      expect(screen.getByText('Preferiti')).toBeInTheDocument();
      expect(screen.getByText('Wishlist')).toBeInTheDocument();
      expect(screen.getByText('Archiviati')).toBeInTheDocument();
    });

    it('shows standard library toggle on non-contextual route', () => {
      mockUsePathname.mockReturnValue('/sessions');
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByTestId('sidebar-library-toggle')).toBeInTheDocument();
    });

    it('expands library children on toggle click (standard nav)', async () => {
      const user = userEvent.setup();
      render(<Sidebar {...defaultProps} />);

      await user.click(screen.getByTestId('sidebar-library-toggle'));

      expect(screen.getByTestId('sidebar-library-private')).toBeInTheDocument();
      expect(screen.getByTestId('sidebar-library-collection')).toBeInTheDocument();
      expect(screen.getByTestId('sidebar-library-wishlist')).toBeInTheDocument();
    });
  });

  describe('User Section', () => {
    it('shows notification bell', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByTestId('notification-bell')).toBeInTheDocument();
    });

    it('shows user avatar with initial', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByText('T')).toBeInTheDocument(); // "Test User" → "T"
    });

    it('opens user dropdown on avatar click', async () => {
      const user = userEvent.setup();
      render(<Sidebar {...defaultProps} />);

      await user.click(screen.getByTestId('sidebar-user-trigger'));

      await waitFor(() => {
        expect(screen.getByTestId('sidebar-profile-item')).toBeInTheDocument();
        expect(screen.getByTestId('sidebar-settings-item')).toBeInTheDocument();
        expect(screen.getByTestId('sidebar-logout-item')).toBeInTheDocument();
      });
    });

    it('shows admin link for admin users', async () => {
      mockUseCurrentUser.mockReturnValue({
        data: { id: '1', email: 'admin@test.com', displayName: 'Admin', role: 'Admin' },
        isLoading: false,
      });

      const user = userEvent.setup();
      render(<Sidebar {...defaultProps} />);

      await user.click(screen.getByTestId('sidebar-user-trigger'));

      await waitFor(() => {
        expect(screen.getByTestId('sidebar-admin-item')).toBeInTheDocument();
      });
    });
  });

  describe('Positioning', () => {
    it('is offset from top by 56px for UniversalNavbar (top-14)', () => {
      render(<Sidebar {...defaultProps} />);
      const sidebar = screen.getByTestId('sidebar');
      expect(sidebar).toHaveClass('top-14');
    });

    it('has reduced height accounting for navbar (h-[calc(100vh-56px)])', () => {
      render(<Sidebar {...defaultProps} />);
      const sidebar = screen.getByTestId('sidebar');
      expect(sidebar).toHaveClass('h-[calc(100vh-56px)]');
    });
  });

  describe('Accessibility', () => {
    it('has sidebar navigation landmark', () => {
      render(<Sidebar {...defaultProps} />);
      expect(
        screen.getByRole('complementary', { name: /sidebar navigation/i })
      ).toBeInTheDocument();
    });

    it('sidebar is hidden on mobile (hidden md:flex)', () => {
      render(<Sidebar {...defaultProps} />);
      const sidebar = screen.getByTestId('sidebar');
      expect(sidebar).toHaveClass('hidden', 'md:flex');
    });

    it('nav items meet WCAG touch target (min-h-[44px]) in standard nav', () => {
      render(<Sidebar {...defaultProps} />);
      const dashboardLink = screen.getByTestId('nav-dashboard');
      expect(dashboardLink).toHaveClass('min-h-[44px]');
    });
  });
});
