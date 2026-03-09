/**
 * Tests for Sidebar component - Issue #4936 + Issue #15 (Sidebar Strategy Rework)
 *
 * Validates the two-zone sidebar layout:
 *   Zone 1: Fixed primary navigation (always visible)
 *   Zone 2: Contextual panel (route-specific, animated)
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { usePathname } from 'next/navigation';
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
    replace: vi.fn(),
  })),
  useSearchParams: vi.fn(() => new URLSearchParams()),
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

// Mock GamesFilterPanel (uses useSearchParams, router, etc.)
vi.mock('@/components/catalog/GamesFilterPanel', () => ({
  GamesFilterPanel: ({ isCollapsed }: { isCollapsed: boolean }) => (
    <div data-testid="games-filter-panel">{!isCollapsed && <span>Filtri giochi</span>}</div>
  ),
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

    it('renders fixed nav zone', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByTestId('sidebar-fixed-nav')).toBeInTheDocument();
    });
  });

  describe('Fixed Nav Zone (always visible)', () => {
    it('shows primary navigation links on any route', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByText('Dashboard')).toBeInTheDocument();
      expect(screen.getByText('Libreria')).toBeInTheDocument();
      expect(screen.getByText('Scopri')).toBeInTheDocument();
      expect(screen.getByText('Chat AI')).toBeInTheDocument();
      expect(screen.getByText('Sessioni')).toBeInTheDocument();
      expect(screen.getByText('Giocatori')).toBeInTheDocument();
    });

    it('shows fixed nav on dashboard route too', () => {
      mockUsePathname.mockReturnValue('/dashboard');
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByTestId('sidebar-fixed-nav')).toBeInTheDocument();
      expect(screen.getByText('Dashboard')).toBeInTheDocument();
      expect(screen.getByText('Libreria')).toBeInTheDocument();
    });

    it('shows fixed nav on library route too', () => {
      mockUsePathname.mockReturnValue('/library');
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByTestId('sidebar-fixed-nav')).toBeInTheDocument();
      expect(screen.getByText('Dashboard')).toBeInTheDocument();
    });

    it('highlights active link based on pathname', () => {
      mockUsePathname.mockReturnValue('/dashboard');
      render(<Sidebar {...defaultProps} />);
      const dashboardLink = screen.getByText('Dashboard').closest('a');
      expect(dashboardLink).toHaveClass('font-semibold');
    });

    it('highlights library link on library sub-routes', () => {
      mockUsePathname.mockReturnValue('/library/wishlist');
      render(<Sidebar {...defaultProps} />);
      // "Libreria" appears in both fixed nav and contextual section label
      const libraryLinks = screen.getAllByText('Libreria');
      const fixedNavLink = libraryLinks[0].closest('a');
      expect(fixedNavLink).toHaveClass('font-semibold');
    });

    it('hides text labels when collapsed', () => {
      render(<Sidebar {...defaultProps} isCollapsed={true} />);
      expect(screen.queryByText('Dashboard')).not.toBeInTheDocument();
      expect(screen.queryByText('Libreria')).not.toBeInTheDocument();
    });
  });

  describe('Contextual Zone', () => {
    it('shows dashboard shortcuts on /dashboard', () => {
      mockUsePathname.mockReturnValue('/dashboard');
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByText('Sessioni recenti')).toBeInTheDocument();
      expect(screen.getByText('Wishlist')).toBeInTheDocument();
    });

    it('shows library filters on /library', () => {
      mockUsePathname.mockReturnValue('/library');
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByText('Tutti i giochi')).toBeInTheDocument();
      expect(screen.getByText('Preferiti')).toBeInTheDocument();
      expect(screen.getByText('Archiviati')).toBeInTheDocument();
    });

    it('shows library filters on /library sub-routes', () => {
      mockUsePathname.mockReturnValue('/library/wishlist');
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByText('Tutti i giochi')).toBeInTheDocument();
      expect(screen.getByText('Giochi privati')).toBeInTheDocument();
    });

    it('shows games filter panel on /games', () => {
      mockUsePathname.mockReturnValue('/games');
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByTestId('games-filter-panel')).toBeInTheDocument();
    });

    it('shows no contextual zone on non-contextual routes', () => {
      mockUsePathname.mockReturnValue('/sessions');
      render(<Sidebar {...defaultProps} />);
      // No contextual section labels
      expect(screen.queryByText('Accesso rapido')).not.toBeInTheDocument();
      expect(screen.queryByText('Preferiti')).not.toBeInTheDocument();
      expect(screen.queryByText('Archiviati')).not.toBeInTheDocument();
    });

    it('shows divider between zones only when contextual zone is active', () => {
      mockUsePathname.mockReturnValue('/dashboard');
      const { container } = render(<Sidebar {...defaultProps} />);
      // There should be an <hr> divider between zones
      const hrs = container.querySelectorAll('hr.border-sidebar-border');
      expect(hrs.length).toBeGreaterThanOrEqual(1);
    });

    it('shows no divider on non-contextual routes', () => {
      mockUsePathname.mockReturnValue('/sessions');
      const { container } = render(<Sidebar {...defaultProps} />);
      // No divider between zones (only SidebarUser border-t exists)
      const contextDividers = container.querySelectorAll('.flex-1.overflow-y-auto > hr');
      expect(contextDividers.length).toBe(0);
    });
  });

  describe('Expanded Mode', () => {
    it('has 220px width when expanded', () => {
      render(<Sidebar {...defaultProps} />);
      const sidebar = screen.getByTestId('sidebar');
      expect(sidebar).toHaveClass('w-[220px]');
    });
  });

  describe('Collapsed Mode', () => {
    it('has 60px width when collapsed', () => {
      render(<Sidebar {...defaultProps} isCollapsed={true} />);
      const sidebar = screen.getByTestId('sidebar');
      expect(sidebar).toHaveClass('w-[60px]');
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

  describe('User Section', () => {
    it('shows notification bell', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByTestId('notification-bell')).toBeInTheDocument();
    });

    it('shows user avatar with initial', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByText('T')).toBeInTheDocument();
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

    it('fixed nav has aria-label', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByRole('navigation', { name: /primary navigation/i })).toBeInTheDocument();
    });

    it('nav items meet WCAG touch target (min-h-[44px])', () => {
      render(<Sidebar {...defaultProps} />);
      const dashboardLink = screen.getByText('Dashboard').closest('a');
      expect(dashboardLink).toHaveClass('min-h-[44px]');
    });
  });
});
