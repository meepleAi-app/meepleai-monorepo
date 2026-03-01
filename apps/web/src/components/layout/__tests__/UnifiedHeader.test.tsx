/**
 * Unit tests for UnifiedHeader component
 * Now mobile-only compact header (48px).
 * Desktop navigation is handled by the Sidebar component.
 *
 * Coverage:
 * - Rendering (logo, user menu, notification bell)
 * - Mobile-only visibility (md:hidden)
 * - Compact height (h-12 = 48px)
 * - User authentication states
 * - User menu dropdown (profile, admin, settings, logout)
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
  useSearchParams: vi.fn(() => ({
    get: vi.fn().mockReturnValue(null),
    toString: vi.fn().mockReturnValue(''),
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

    it('should render login button when not authenticated', () => {
      mockUseCurrentUser.mockReturnValue({
        data: null,
        isLoading: false,
      });
      render(<UnifiedHeader />);
      expect(screen.getByText('Accedi')).toBeInTheDocument();
    });
  });

  describe('Mobile-Only Layout', () => {
    it('should not have md:hidden (parent layouts control visibility)', () => {
      render(<UnifiedHeader />);
      const header = screen.getByTestId('unified-header');
      expect(header).not.toHaveClass('md:hidden');
    });

    it('should have compact h-12 height (48px)', () => {
      const { container } = render(<UnifiedHeader />);
      const innerDiv = container.querySelector('.h-12');
      expect(innerDiv).toBeInTheDocument();
    });

    it('should not render desktop navigation', () => {
      const { container } = render(<UnifiedHeader />);
      const desktopNav = container.querySelector('nav[aria-label="Main navigation"]');
      expect(desktopNav).not.toBeInTheDocument();
    });

    it('should not render overflow dropdown', () => {
      render(<UnifiedHeader />);
      expect(screen.queryByTestId('nav-overflow-trigger')).not.toBeInTheDocument();
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

      expect(screen.getByText('T')).toBeInTheDocument();
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

  describe('Admin Visibility', () => {
    it('should show admin link in dropdown for admin users', async () => {
      mockUseCurrentUser.mockReturnValue({
        data: { id: '1', email: 'admin@test.com', displayName: 'Admin User', role: 'Admin' },
        isLoading: false,
      });

      const user = userEvent.setup();
      render(<UnifiedHeader />);

      await user.click(screen.getByTestId('user-menu-trigger'));

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

      await user.click(screen.getByTestId('user-menu-trigger'));

      await waitFor(() => {
        expect(screen.queryByTestId('admin-panel-menu-item')).not.toBeInTheDocument();
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
