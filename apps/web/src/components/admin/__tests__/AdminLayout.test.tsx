/**
 * AdminLayout Component Tests - Issue #887
 *
 * Tests for admin layout component.
 * Features tested:
 * - Collapsible sidebar with localStorage persistence
 * - Mobile responsive with Sheet drawer
 * - Header and breadcrumbs integration
 * - Badge counts on navigation items
 * - Dark mode support
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AdminLayout, type AdminLayoutProps } from '../AdminLayout';

// Mock localStorage
const localStorageMock = {
  getItem: vi.fn(),
  setItem: vi.fn(),
  removeItem: vi.fn(),
  clear: vi.fn(),
};
Object.defineProperty(window, 'localStorage', { value: localStorageMock });

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn(),
    back: vi.fn(),
  }),
  usePathname: () => '/admin',
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

const defaultProps: AdminLayoutProps = {
  children: <div data-testid="page-content">Page Content</div>,
};

const mockUser = {
  id: 'user-123',
  email: 'admin@example.com',
  displayName: 'Admin User',
  role: 'admin',
};

describe('AdminLayout', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorageMock.getItem.mockReturnValue(null);
  });

  describe('Basic rendering', () => {
    it('renders children content', () => {
      render(<AdminLayout {...defaultProps} />);

      expect(screen.getByTestId('page-content')).toBeInTheDocument();
    });

    it('renders header component', () => {
      render(<AdminLayout {...defaultProps} />);

      expect(screen.getByRole('banner')).toBeInTheDocument();
    });

    it('renders default title in header', () => {
      render(<AdminLayout {...defaultProps} />);

      expect(screen.getByText('MeepleAI Admin')).toBeInTheDocument();
    });

    it('renders breadcrumbs by default', () => {
      render(<AdminLayout {...defaultProps} />);

      expect(screen.getByRole('navigation', { name: 'Breadcrumb' })).toBeInTheDocument();
    });

    it('renders sidebar on desktop', () => {
      render(<AdminLayout {...defaultProps} />);

      // Sidebar should be present but hidden on mobile
      expect(screen.getByText('Dashboard')).toBeInTheDocument();
    });
  });

  describe('Sidebar collapse', () => {
    it('starts expanded by default', () => {
      localStorageMock.getItem.mockReturnValue(null);
      render(<AdminLayout {...defaultProps} />);

      // Full labels should be visible when expanded
      expect(screen.getAllByText('Dashboard').length).toBeGreaterThan(0);
    });

    it('loads collapsed state from localStorage', async () => {
      localStorageMock.getItem.mockReturnValue('true');
      render(<AdminLayout {...defaultProps} />);

      await waitFor(() => {
        expect(localStorageMock.getItem).toHaveBeenCalledWith('admin-sidebar-collapsed');
      });
    });

    it('saves collapsed state to localStorage on toggle', async () => {
      const user = userEvent.setup();
      render(<AdminLayout {...defaultProps} />);

      // Find and click the collapse button in the sidebar
      const collapseButton = screen.getByLabelText(/collapse sidebar/i);
      await user.click(collapseButton);

      expect(localStorageMock.setItem).toHaveBeenCalledWith('admin-sidebar-collapsed', 'true');
    });

    it('saves expanded state to localStorage on toggle', async () => {
      localStorageMock.getItem.mockReturnValue('true');
      const user = userEvent.setup();
      render(<AdminLayout {...defaultProps} />);

      // Find and click the expand button
      const expandButton = screen.getByLabelText(/expand sidebar/i);
      await user.click(expandButton);

      expect(localStorageMock.setItem).toHaveBeenCalledWith('admin-sidebar-collapsed', 'false');
    });
  });

  describe('Mobile menu', () => {
    it('renders mobile menu button', () => {
      render(<AdminLayout {...defaultProps} />);

      expect(screen.getByLabelText('Open navigation menu')).toBeInTheDocument();
    });

    it('opens mobile drawer on button click', async () => {
      const user = userEvent.setup();
      render(<AdminLayout {...defaultProps} />);

      const menuButton = screen.getByLabelText('Open navigation menu');
      await user.click(menuButton);

      // Sheet should open with title
      expect(screen.getByText('Admin Menu')).toBeInTheDocument();
    });

    it('mobile menu button is hidden on desktop', () => {
      render(<AdminLayout {...defaultProps} />);

      const menuButton = screen.getByLabelText('Open navigation menu');
      // Parent container should have lg:hidden
      expect(menuButton.closest('.lg\\:hidden')).toBeInTheDocument();
    });
  });

  describe('User prop integration', () => {
    it('passes user to header', () => {
      render(<AdminLayout {...defaultProps} user={mockUser} />);

      // User initials should be visible
      expect(screen.getByText('AU')).toBeInTheDocument();
    });
  });

  describe('Badges prop integration', () => {
    const badges = {
      users: { count: 5, variant: 'default' as const },
      alerts: { count: 3, variant: 'destructive' as const },
    };

    it('passes badges to sidebar', () => {
      render(<AdminLayout {...defaultProps} badges={badges} />);

      // Badge counts should be visible
      expect(screen.getByText('5')).toBeInTheDocument();
      expect(screen.getByText('3')).toBeInTheDocument();
    });
  });

  describe('Breadcrumbs configuration', () => {
    it('shows breadcrumbs by default', () => {
      render(<AdminLayout {...defaultProps} />);

      expect(screen.getByRole('navigation', { name: 'Breadcrumb' })).toBeInTheDocument();
    });

    it('hides breadcrumbs when showBreadcrumbs is false', () => {
      render(<AdminLayout {...defaultProps} showBreadcrumbs={false} />);

      expect(screen.queryByRole('navigation', { name: 'Breadcrumb' })).not.toBeInTheDocument();
    });

    it('uses custom breadcrumb items when provided', () => {
      const customBreadcrumbs = [{ label: 'Custom', href: '/custom' }, { label: 'Path' }];

      render(<AdminLayout {...defaultProps} breadcrumbs={customBreadcrumbs} />);

      expect(screen.getByText('Custom')).toBeInTheDocument();
      expect(screen.getByText('Path')).toBeInTheDocument();
    });
  });

  describe('Header actions', () => {
    it('renders custom header actions', () => {
      const actions = <button data-testid="header-action">Action</button>;
      render(<AdminLayout {...defaultProps} headerActions={actions} />);

      expect(screen.getByTestId('header-action')).toBeInTheDocument();
    });
  });

  describe('Styling', () => {
    it('applies custom className to main content', () => {
      const { container } = render(<AdminLayout {...defaultProps} className="custom-main" />);

      expect(container.querySelector('.custom-main')).toBeInTheDocument();
    });

    it('applies min-height to layout', () => {
      const { container } = render(<AdminLayout {...defaultProps} />);

      expect(container.querySelector('.min-h-screen')).toBeInTheDocument();
    });

    it('applies dark mode background', () => {
      const { container } = render(<AdminLayout {...defaultProps} />);

      expect(container.querySelector('.dark\\:bg-gray-950')).toBeInTheDocument();
    });

    it('applies transition classes for sidebar width change', () => {
      const { container } = render(<AdminLayout {...defaultProps} />);

      expect(container.querySelector('.transition-all')).toBeInTheDocument();
    });

    it('applies padding to main content area', () => {
      const { container } = render(<AdminLayout {...defaultProps} />);

      expect(container.querySelector('.p-4')).toBeInTheDocument();
    });
  });

  describe('Layout structure', () => {
    it('has flex container for sidebar and main', () => {
      const { container } = render(<AdminLayout {...defaultProps} />);

      // Should have flex layout for sidebar + main
      const flexContainer = container.querySelector('.flex');
      expect(flexContainer).toBeInTheDocument();
    });

    it('main content is flex-1', () => {
      render(<AdminLayout {...defaultProps} />);

      const main = screen.getByRole('main');
      expect(main).toHaveClass('flex-1');
    });

    it('sidebar is hidden on mobile', () => {
      render(<AdminLayout {...defaultProps} />);

      // Desktop sidebar should have hidden lg:flex
      const sidebarNav = screen.getByRole('navigation', { name: 'Admin navigation' });
      expect(sidebarNav).toHaveClass('hidden');
      expect(sidebarNav).toHaveClass('lg:flex');
    });
  });

  describe('Hydration safety', () => {
    it('uses default width before mount for hydration safety', () => {
      // Initial render should use expanded width to avoid hydration mismatch
      localStorageMock.getItem.mockReturnValue('true');
      render(<AdminLayout {...defaultProps} />);

      // After mount, collapsed state is applied
      // This test verifies the component doesn't crash during SSR
      expect(screen.getByTestId('page-content')).toBeInTheDocument();
    });
  });

  describe('TooltipProvider integration', () => {
    it('wraps sidebar in TooltipProvider', () => {
      render(<AdminLayout {...defaultProps} />);

      // Sidebar tooltips should work (no errors thrown)
      expect(screen.getByRole('navigation', { name: 'Admin navigation' })).toBeInTheDocument();
    });
  });

  describe('Responsive behavior', () => {
    it('renders mobile menu trigger in correct position', () => {
      const { container } = render(<AdminLayout {...defaultProps} />);

      // Mobile trigger should be in header area
      const header = screen.getByRole('banner');
      expect(header).toContainElement(screen.getByLabelText('Open navigation menu'));
    });

    it('sidebar is visible on desktop', () => {
      render(<AdminLayout {...defaultProps} />);

      const sidebar = screen.getByRole('navigation', { name: 'Admin navigation' });
      expect(sidebar).toHaveClass('lg:flex');
    });
  });

  describe('Content area', () => {
    it('renders content with proper spacing', () => {
      const { container } = render(<AdminLayout {...defaultProps} />);

      // Should have responsive padding
      expect(container.querySelector('.sm\\:p-6')).toBeInTheDocument();
      expect(container.querySelector('.lg\\:p-8')).toBeInTheDocument();
    });

    it('breadcrumbs have margin-bottom when shown', () => {
      const { container } = render(<AdminLayout {...defaultProps} />);

      const breadcrumbNav = screen.getByRole('navigation', { name: 'Breadcrumb' });
      expect(breadcrumbNav).toHaveClass('mb-4');
    });
  });

  describe('Multiple children', () => {
    it('renders multiple children', () => {
      render(
        <AdminLayout>
          <div data-testid="child-1">Child 1</div>
          <div data-testid="child-2">Child 2</div>
        </AdminLayout>
      );

      expect(screen.getByTestId('child-1')).toBeInTheDocument();
      expect(screen.getByTestId('child-2')).toBeInTheDocument();
    });
  });

  describe('Edge cases', () => {
    it('handles undefined user gracefully', () => {
      render(<AdminLayout {...defaultProps} user={undefined} />);

      // Should render with default initials
      expect(screen.getByText('AD')).toBeInTheDocument();
    });

    it('handles empty badges gracefully', () => {
      render(<AdminLayout {...defaultProps} badges={{}} />);

      expect(screen.getByTestId('page-content')).toBeInTheDocument();
    });

    it('handles undefined badges gracefully', () => {
      render(<AdminLayout {...defaultProps} badges={undefined} />);

      expect(screen.getByTestId('page-content')).toBeInTheDocument();
    });
  });
});
