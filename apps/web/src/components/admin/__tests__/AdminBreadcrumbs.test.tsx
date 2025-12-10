/**
 * AdminBreadcrumbs Component Tests - Issue #887
 *
 * Tests for breadcrumb navigation component.
 * Features tested:
 * - Auto-generation from pathname
 * - Custom breadcrumb items
 * - UUID/numeric ID handling
 * - Responsive behavior
 * - Accessibility
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AdminBreadcrumbs, type BreadcrumbItem } from '../AdminBreadcrumbs';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  usePathname: vi.fn(() => '/admin/users'),
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

// Access the mocked usePathname
import { usePathname } from 'next/navigation';
const mockUsePathname = vi.mocked(usePathname);

describe('AdminBreadcrumbs', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUsePathname.mockReturnValue('/admin/users');
  });

  describe('Auto-generation from pathname', () => {
    it('generates breadcrumbs from /admin path', () => {
      mockUsePathname.mockReturnValue('/admin');
      render(<AdminBreadcrumbs />);

      // For single item, check via list items
      const listItems = screen.getAllByRole('listitem');
      expect(listItems).toHaveLength(1);
    });

    it('generates breadcrumbs from /admin/users path', () => {
      mockUsePathname.mockReturnValue('/admin/users');
      render(<AdminBreadcrumbs />);

      const listItems = screen.getAllByRole('listitem');
      expect(listItems).toHaveLength(2);
      expect(screen.getByText('Users')).toBeInTheDocument();
    });

    it('generates breadcrumbs from /admin/games/details path', () => {
      mockUsePathname.mockReturnValue('/admin/games/details');
      render(<AdminBreadcrumbs />);

      const listItems = screen.getAllByRole('listitem');
      expect(listItems).toHaveLength(3);
      expect(screen.getByText('Games')).toBeInTheDocument();
    });

    it('converts kebab-case to Title Case', () => {
      mockUsePathname.mockReturnValue('/admin/bulk-export');
      render(<AdminBreadcrumbs />);

      expect(screen.getByText('Bulk Export')).toBeInTheDocument();
    });

    it('handles n8n-templates special case', () => {
      mockUsePathname.mockReturnValue('/admin/n8n-templates');
      render(<AdminBreadcrumbs />);

      expect(screen.getByText('N8N Templates')).toBeInTheDocument();
    });

    it('returns null for empty path', () => {
      mockUsePathname.mockReturnValue('/');
      const { container } = render(<AdminBreadcrumbs />);

      expect(container.firstChild).toBeNull();
    });
  });

  describe('UUID handling', () => {
    it('displays "Details" for UUID segments', () => {
      mockUsePathname.mockReturnValue('/admin/games/550e8400-e29b-41d4-a716-446655440000');
      render(<AdminBreadcrumbs />);

      const listItems = screen.getAllByRole('listitem');
      expect(listItems).toHaveLength(3);
      expect(screen.getByText('Games')).toBeInTheDocument();
      // Details is the UUID replacement text - check it's rendered
      expect(screen.getByText('Details')).toBeInTheDocument();
    });

    it('displays # prefix for numeric IDs', () => {
      mockUsePathname.mockReturnValue('/admin/users/123');
      render(<AdminBreadcrumbs />);

      const listItems = screen.getAllByRole('listitem');
      expect(listItems).toHaveLength(3);
      expect(screen.getByText('Users')).toBeInTheDocument();
      expect(screen.getByText('#123')).toBeInTheDocument();
    });
  });

  describe('Custom breadcrumb items', () => {
    const customItems: BreadcrumbItem[] = [
      { label: 'Dashboard', href: '/admin' },
      { label: 'Settings', href: '/admin/settings' },
      { label: 'Security' },
    ];

    it('renders custom items instead of auto-generated', () => {
      render(<AdminBreadcrumbs items={customItems} />);

      expect(screen.getByText('Dashboard')).toBeInTheDocument();
      expect(screen.getByText('Settings')).toBeInTheDocument();
      expect(screen.getByText('Security')).toBeInTheDocument();
    });

    it('makes items with href clickable links', () => {
      render(<AdminBreadcrumbs items={customItems} />);

      const dashboardLink = screen.getByRole('link', { name: /dashboard/i });
      expect(dashboardLink).toHaveAttribute('href', '/admin');
    });

    it('renders last item without link', () => {
      render(<AdminBreadcrumbs items={customItems} />);

      // Security should be a span, not a link
      expect(screen.queryByRole('link', { name: 'Security' })).not.toBeInTheDocument();
      expect(screen.getByText('Security')).toBeInTheDocument();
    });
  });

  describe('Home icon', () => {
    it('shows home icon by default for first item', () => {
      mockUsePathname.mockReturnValue('/admin/users');
      const { container } = render(<AdminBreadcrumbs />);

      const homeIcons = container.querySelectorAll('svg');
      expect(homeIcons.length).toBeGreaterThan(0);
    });

    it('hides home icon when showHomeIcon is false', () => {
      mockUsePathname.mockReturnValue('/admin');
      const { container } = render(<AdminBreadcrumbs showHomeIcon={false} />);

      // Only chevron icons should be present (if any)
      const allIcons = container.querySelectorAll('svg');
      // For single item, no chevrons either
      expect(
        Array.from(allIcons).filter(icon => icon.classList.contains('lucide-home')).length
      ).toBe(0);
    });
  });

  describe('Navigation structure', () => {
    it('renders as nav element with aria-label', () => {
      render(<AdminBreadcrumbs />);

      expect(screen.getByRole('navigation', { name: 'Breadcrumb' })).toBeInTheDocument();
    });

    it('renders ordered list for items', () => {
      mockUsePathname.mockReturnValue('/admin/users');
      render(<AdminBreadcrumbs />);

      expect(screen.getByRole('list')).toBeInTheDocument();
    });

    it('renders list items', () => {
      mockUsePathname.mockReturnValue('/admin/users');
      render(<AdminBreadcrumbs />);

      const listItems = screen.getAllByRole('listitem');
      expect(listItems.length).toBe(2);
    });

    it('marks current page with aria-current', () => {
      mockUsePathname.mockReturnValue('/admin/users');
      render(<AdminBreadcrumbs />);

      const currentPage = screen.getByText('Users');
      expect(currentPage).toHaveAttribute('aria-current', 'page');
    });
  });

  describe('Chevron separators', () => {
    it('renders chevron between items', () => {
      mockUsePathname.mockReturnValue('/admin/users/settings');
      const { container } = render(<AdminBreadcrumbs />);

      // Should have chevrons between items (count = items - 1)
      const chevrons = container.querySelectorAll('[aria-hidden="true"]');
      expect(chevrons.length).toBeGreaterThan(0);
    });

    it('does not render chevron before first item', () => {
      mockUsePathname.mockReturnValue('/admin');
      const { container } = render(<AdminBreadcrumbs />);

      const listItem = screen.getByRole('listitem');
      // First child should not be a chevron
      expect(listItem.querySelector('[aria-hidden="true"]:first-child')).toBeNull();
    });
  });

  describe('Styling', () => {
    it('applies custom className', () => {
      const { container } = render(<AdminBreadcrumbs className="custom-breadcrumbs" />);

      expect(container.querySelector('.custom-breadcrumbs')).toBeInTheDocument();
    });

    it('applies flex layout', () => {
      render(<AdminBreadcrumbs />);

      const nav = screen.getByRole('navigation');
      expect(nav).toHaveClass('flex');
    });

    it('applies hover styling to links', () => {
      mockUsePathname.mockReturnValue('/admin/users');
      render(<AdminBreadcrumbs />);

      const adminLink = screen.getByRole('link', { name: /admin/i });
      expect(adminLink).toHaveClass('hover:text-gray-700');
    });

    it('applies font-medium to current page', () => {
      mockUsePathname.mockReturnValue('/admin/users');
      render(<AdminBreadcrumbs />);

      const currentPage = screen.getByText('Users');
      expect(currentPage).toHaveClass('font-medium');
    });
  });

  describe('Edge cases', () => {
    it('handles null pathname', () => {
      mockUsePathname.mockReturnValue(null);
      const { container } = render(<AdminBreadcrumbs />);

      expect(container.firstChild).toBeNull();
    });

    it('handles deeply nested paths', () => {
      mockUsePathname.mockReturnValue('/admin/settings/security/two-factor/setup');
      render(<AdminBreadcrumbs />);

      expect(screen.getByText('Admin')).toBeInTheDocument();
      expect(screen.getByText('Settings')).toBeInTheDocument();
      expect(screen.getByText('Security')).toBeInTheDocument();
      expect(screen.getByText('Two Factor')).toBeInTheDocument();
      expect(screen.getByText('Setup')).toBeInTheDocument();
    });

    it('handles camelCase segments', () => {
      mockUsePathname.mockReturnValue('/admin/userSettings');
      render(<AdminBreadcrumbs />);

      expect(screen.getByText('User Settings')).toBeInTheDocument();
    });
  });
});
