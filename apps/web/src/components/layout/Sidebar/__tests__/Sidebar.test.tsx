/**
 * Tests for Sidebar component
 * Validates rendering, collapsed mode, navigation, user section, and accessibility.
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { usePathname, useRouter } from 'next/navigation';
import { describe, it, expect, vi, beforeEach, Mock } from 'vitest';

import { useCurrentUser } from '@/hooks/queries/useCurrentUser';

import { Sidebar } from '../Sidebar';

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
    mockUsePathname.mockReturnValue('/dashboard');
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

    it('renders logo link', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByLabelText('MeepleAI Home')).toBeInTheDocument();
    });

    it('renders toggle button', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByTestId('sidebar-toggle')).toBeInTheDocument();
    });

    it('renders user section', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByTestId('sidebar-user')).toBeInTheDocument();
    });

    it('renders navigation items (not profile)', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByTestId('nav-dashboard')).toBeInTheDocument();
      expect(screen.getByTestId('nav-chat')).toBeInTheDocument();
      expect(screen.getByTestId('nav-catalog')).toBeInTheDocument();
      // Profile should NOT be in sidebar nav (hideFromMainNav)
      expect(screen.queryByTestId('nav-profile')).not.toBeInTheDocument();
    });

    it('renders library toggle', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByTestId('sidebar-library-toggle')).toBeInTheDocument();
    });

    it('renders Strumenti group with agents and sessions', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByText('Strumenti')).toBeInTheDocument();
      expect(screen.getByTestId('nav-agents')).toBeInTheDocument();
      expect(screen.getByTestId('nav-sessions')).toBeInTheDocument();
    });
  });

  describe('Expanded Mode', () => {
    it('has 220px width when expanded', () => {
      render(<Sidebar {...defaultProps} />);
      const sidebar = screen.getByTestId('sidebar');
      expect(sidebar).toHaveClass('w-[220px]');
    });

    it('shows text labels next to icons', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByText('Dashboard')).toBeInTheDocument();
      expect(screen.getByText('Chat')).toBeInTheDocument();
      expect(screen.getByText('Scopri')).toBeInTheDocument();
    });

    it('shows MeepleAI wordmark next to icon', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByText('MeepleAI')).toBeInTheDocument();
    });

    it('shows Strumenti group label', () => {
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

    it('hides text labels when collapsed', () => {
      render(<Sidebar {...defaultProps} isCollapsed={true} />);
      // Labels should not be visible (icons only)
      expect(screen.queryByText('Dashboard')).not.toBeInTheDocument();
    });

    it('hides MeepleAI wordmark when collapsed', () => {
      render(<Sidebar {...defaultProps} isCollapsed={true} />);
      expect(screen.queryByText('MeepleAI')).not.toBeInTheDocument();
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
      expect(screen.getByTestId('sidebar-toggle')).toHaveAttribute(
        'aria-label',
        'Expand sidebar'
      );
    });
  });

  describe('Library Section', () => {
    it('expands library children on toggle click', async () => {
      const user = userEvent.setup();
      render(<Sidebar {...defaultProps} />);

      await user.click(screen.getByTestId('sidebar-library-toggle'));

      expect(screen.getByTestId('sidebar-library-collection')).toBeInTheDocument();
      expect(screen.getByTestId('sidebar-library-private')).toBeInTheDocument();
      expect(screen.getByTestId('sidebar-library-proposals')).toBeInTheDocument();
      expect(screen.getByTestId('sidebar-library-wishlist')).toBeInTheDocument();
    });

    it('library auto-expanded when on library route', () => {
      mockUsePathname.mockReturnValue('/library');
      render(<Sidebar {...defaultProps} />);

      expect(screen.getByTestId('sidebar-library-collection')).toBeInTheDocument();
    });
  });

  describe('Active State', () => {
    it('highlights dashboard when on /dashboard', () => {
      mockUsePathname.mockReturnValue('/dashboard');
      render(<Sidebar {...defaultProps} />);

      const dashboardLink = screen.getByTestId('nav-dashboard');
      expect(dashboardLink).toHaveClass('text-[hsl(262_83%_62%)]');
      expect(dashboardLink).toHaveAttribute('aria-current', 'page');
    });

    it('highlights catalog when on /games', () => {
      mockUsePathname.mockReturnValue('/games');
      render(<Sidebar {...defaultProps} />);

      const catalogLink = screen.getByTestId('nav-catalog');
      expect(catalogLink).toHaveAttribute('aria-current', 'page');
    });
  });

  describe('User Section', () => {
    it('shows user avatar with initial', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByText('T')).toBeInTheDocument(); // "Test User" → "T"
    });

    it('shows notification bell', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByTestId('notification-bell')).toBeInTheDocument();
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

  describe('Accessibility', () => {
    it('has sidebar navigation landmark', () => {
      render(<Sidebar {...defaultProps} />);
      expect(screen.getByRole('navigation', { name: /sidebar navigation/i })).toBeInTheDocument();
    });

    it('sidebar is hidden on mobile (hidden md:flex)', () => {
      render(<Sidebar {...defaultProps} />);
      const sidebar = screen.getByTestId('sidebar');
      expect(sidebar).toHaveClass('hidden', 'md:flex');
    });

    it('nav items meet WCAG touch target (min-h-[44px])', () => {
      render(<Sidebar {...defaultProps} />);
      const dashboardLink = screen.getByTestId('nav-dashboard');
      expect(dashboardLink).toHaveClass('min-h-[44px]');
    });
  });
});
