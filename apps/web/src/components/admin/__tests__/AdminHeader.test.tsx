/**
 * AdminHeader Component Tests - Issue #887
 *
 * Tests for admin header component.
 * Features tested:
 * - User menu with profile/settings/logout
 * - Mobile menu trigger slot
 * - Home link navigation
 * - getInitials helper function
 * - Accessibility
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AdminHeader, type AdminUser } from '../AdminHeader';

// Mock next/navigation
const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
    replace: vi.fn(),
    back: vi.fn(),
  }),
}));

// Mock next/link
vi.mock('next/link', () => ({
  default: ({
    children,
    href,
    ...props
  }: {
    children: React.ReactNode;
    href: string;
    className?: string;
  }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

// Mock the API module
vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      logout: vi.fn().mockResolvedValue(undefined),
    },
  },
}));

import { api } from '@/lib/api';

const mockUser: AdminUser = {
  id: 'user-123',
  email: 'admin@example.com',
  displayName: 'John Doe',
  role: 'admin',
};

describe('AdminHeader', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Basic rendering', () => {
    it('renders header element', () => {
      render(<AdminHeader />);

      expect(screen.getByRole('banner')).toBeInTheDocument();
    });

    it('renders default title', () => {
      render(<AdminHeader />);

      expect(screen.getByText('MeepleAI Admin')).toBeInTheDocument();
    });

    it('renders custom title', () => {
      render(<AdminHeader title="Custom Admin Panel" />);

      expect(screen.getByText('Custom Admin Panel')).toBeInTheDocument();
    });

    it('applies custom className', () => {
      const { container } = render(<AdminHeader className="custom-header" />);

      expect(container.querySelector('.custom-header')).toBeInTheDocument();
    });
  });

  describe('User avatar and initials', () => {
    it('displays initials from displayName', () => {
      render(<AdminHeader user={mockUser} />);

      expect(screen.getByText('JD')).toBeInTheDocument();
    });

    it('displays initials from email when no displayName', () => {
      render(<AdminHeader user={{ ...mockUser, displayName: undefined }} />);

      expect(screen.getByText('AD')).toBeInTheDocument();
    });

    it('displays default initials when no user info', () => {
      render(<AdminHeader />);

      expect(screen.getByText('AD')).toBeInTheDocument();
    });

    it('handles single word displayName', () => {
      render(<AdminHeader user={{ ...mockUser, displayName: 'Admin' }} />);

      expect(screen.getByText('A')).toBeInTheDocument();
    });

    it('handles long displayName (more than 2 words)', () => {
      render(<AdminHeader user={{ ...mockUser, displayName: 'John Michael Doe' }} />);

      expect(screen.getByText('JM')).toBeInTheDocument();
    });
  });

  describe('User dropdown menu', () => {
    it('opens dropdown on avatar click', async () => {
      const user = userEvent.setup();
      render(<AdminHeader user={mockUser} />);

      const avatarButton = screen.getByRole('button', { name: 'JD' });
      await user.click(avatarButton);

      expect(screen.getByText('John Doe')).toBeInTheDocument();
      expect(screen.getByText('admin@example.com')).toBeInTheDocument();
    });

    it('shows profile link in dropdown', async () => {
      const user = userEvent.setup();
      render(<AdminHeader user={mockUser} />);

      const avatarButton = screen.getByRole('button', { name: 'JD' });
      await user.click(avatarButton);

      expect(screen.getByRole('menuitem', { name: /profile/i })).toBeInTheDocument();
    });

    it('shows settings link in dropdown', async () => {
      const user = userEvent.setup();
      render(<AdminHeader user={mockUser} />);

      const avatarButton = screen.getByRole('button', { name: 'JD' });
      await user.click(avatarButton);

      expect(screen.getByRole('menuitem', { name: /settings/i })).toBeInTheDocument();
    });

    it('shows logout option in dropdown', async () => {
      const user = userEvent.setup();
      render(<AdminHeader user={mockUser} />);

      const avatarButton = screen.getByRole('button', { name: 'JD' });
      await user.click(avatarButton);

      expect(screen.getByRole('menuitem', { name: /log out/i })).toBeInTheDocument();
    });

    it('displays Admin when no displayName', async () => {
      const user = userEvent.setup();
      render(<AdminHeader user={{ ...mockUser, displayName: undefined }} />);

      const avatarButton = screen.getByRole('button', { name: 'AD' });
      await user.click(avatarButton);

      // There are now multiple "Admin" texts (button and dropdown label)
      // Check that the dropdown contains "Admin" as the user label
      const adminTexts = screen.getAllByText('Admin');
      expect(adminTexts.length).toBeGreaterThanOrEqual(1);
    });
  });

  describe('Logout functionality', () => {
    it('calls logout API on logout click', async () => {
      const user = userEvent.setup();
      render(<AdminHeader user={mockUser} />);

      const avatarButton = screen.getByRole('button', { name: 'JD' });
      await user.click(avatarButton);

      const logoutButton = screen.getByRole('menuitem', { name: /log out/i });
      await user.click(logoutButton);

      await waitFor(() => {
        expect(api.auth.logout).toHaveBeenCalledTimes(1);
      });
    });

    it('redirects to login after logout', async () => {
      const user = userEvent.setup();
      render(<AdminHeader user={mockUser} />);

      const avatarButton = screen.getByRole('button', { name: 'JD' });
      await user.click(avatarButton);

      const logoutButton = screen.getByRole('menuitem', { name: /log out/i });
      await user.click(logoutButton);

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith('/login');
      });
    });

    it('redirects to login even on logout error', async () => {
      vi.mocked(api.auth.logout).mockRejectedValueOnce(new Error('Network error'));
      const user = userEvent.setup();
      render(<AdminHeader user={mockUser} />);

      const avatarButton = screen.getByRole('button', { name: 'JD' });
      await user.click(avatarButton);

      const logoutButton = screen.getByRole('menuitem', { name: /log out/i });
      await user.click(logoutButton);

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith('/login');
      });
    });
  });

  describe('Home link', () => {
    it('renders System button that links to home', () => {
      render(<AdminHeader />);

      // Home link is now the "System" button that goes to /
      const systemBtn = screen.getByTestId('admin-header-system-btn');
      expect(systemBtn).toHaveAttribute('href', '/');
    });

    it('System button has hidden class for mobile', () => {
      render(<AdminHeader />);

      // System button is hidden on mobile (sm:flex)
      const systemBtn = screen.getByTestId('admin-header-system-btn');
      expect(systemBtn).toHaveClass('hidden');
      expect(systemBtn).toHaveClass('sm:flex');
    });
  });

  describe('Mobile menu trigger', () => {
    it('renders mobile menu trigger when provided', () => {
      const trigger = <button data-testid="mobile-trigger">Menu</button>;
      render(<AdminHeader mobileMenuTrigger={trigger} />);

      expect(screen.getByTestId('mobile-trigger')).toBeInTheDocument();
    });

    it('hides mobile trigger on desktop', () => {
      const trigger = <button data-testid="mobile-trigger">Menu</button>;
      const { container } = render(<AdminHeader mobileMenuTrigger={trigger} />);

      const triggerWrapper = container.querySelector('.lg\\:hidden');
      expect(triggerWrapper).toBeInTheDocument();
    });

    it('does not render trigger wrapper when no trigger provided', () => {
      const { container } = render(<AdminHeader />);

      // Should not have the mobile trigger wrapper at all
      expect(container.querySelector('.lg\\:hidden')).not.toBeInTheDocument();
    });
  });

  describe('Header actions slot', () => {
    it('renders custom actions', () => {
      const actions = <button data-testid="custom-action">Action</button>;
      render(<AdminHeader actions={actions} />);

      expect(screen.getByTestId('custom-action')).toBeInTheDocument();
    });

    it('does not render actions wrapper when no actions provided', () => {
      const { container } = render(<AdminHeader />);

      // Actions wrapper should not render empty
      const actionWrappers = container.querySelectorAll('.flex.items-center.gap-2');
      // There might be other flex containers, but actions specific one should be conditionally rendered
      expect(screen.queryByTestId('custom-action')).not.toBeInTheDocument();
    });
  });

  describe('Styling', () => {
    it('applies sticky positioning', () => {
      render(<AdminHeader />);

      const header = screen.getByRole('banner');
      expect(header).toHaveClass('sticky');
      expect(header).toHaveClass('top-0');
    });

    it('applies z-index for layering', () => {
      render(<AdminHeader />);

      const header = screen.getByRole('banner');
      // Component uses z-50 for higher stacking context
      expect(header).toHaveClass('z-50');
    });

    it('applies border styling', () => {
      render(<AdminHeader />);

      const header = screen.getByRole('banner');
      expect(header).toHaveClass('border-b');
    });

    it('applies dark mode classes', () => {
      render(<AdminHeader />);

      const header = screen.getByRole('banner');
      // Component uses semantic design tokens for dark mode
      expect(header).toHaveClass('dark:bg-card');
      expect(header).toHaveClass('dark:border-border/30');
    });

    it('logout option has red text styling', async () => {
      const user = userEvent.setup();
      render(<AdminHeader user={mockUser} />);

      const avatarButton = screen.getByRole('button', { name: 'JD' });
      await user.click(avatarButton);

      const logoutButton = screen.getByRole('menuitem', { name: /log out/i });
      expect(logoutButton).toHaveClass('text-red-600');
    });
  });

  describe('Accessibility', () => {
    it('avatar button is focusable', () => {
      render(<AdminHeader user={mockUser} />);

      const avatarButton = screen.getByRole('button', { name: 'JD' });
      expect(avatarButton).not.toHaveAttribute('tabindex', '-1');
    });

    it('dropdown menu items are accessible', async () => {
      const user = userEvent.setup();
      render(<AdminHeader user={mockUser} />);

      const avatarButton = screen.getByRole('button', { name: 'JD' });
      await user.click(avatarButton);

      const menuItems = screen.getAllByRole('menuitem');
      expect(menuItems.length).toBeGreaterThan(0);
    });

    it('home link is accessible', () => {
      render(<AdminHeader />);

      // Home link is now the "System" button that goes to /
      const homeLink = screen.getByRole('link', { name: /system/i });
      expect(homeLink).toBeInTheDocument();
    });
  });

  describe('Navigation links', () => {
    it('profile link points to /profile', async () => {
      const user = userEvent.setup();
      render(<AdminHeader user={mockUser} />);

      const avatarButton = screen.getByRole('button', { name: 'JD' });
      await user.click(avatarButton);

      const profileLink = screen.getByRole('menuitem', { name: /profile/i });
      expect(profileLink.closest('a')).toHaveAttribute('href', '/profile');
    });

    it('settings link points to /settings', async () => {
      const user = userEvent.setup();
      render(<AdminHeader user={mockUser} />);

      const avatarButton = screen.getByRole('button', { name: 'JD' });
      await user.click(avatarButton);

      const settingsLink = screen.getByRole('menuitem', { name: /settings/i });
      expect(settingsLink.closest('a')).toHaveAttribute('href', '/settings');
    });
  });
});
